using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.SignalR.Client;
using Novolis.Messaging.SecureText;
using Novolis.Security.SecureText;

namespace ChannelLab.Services;

internal sealed class ChannelSession : IAsyncDisposable
{
    static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    readonly HttpClient _http = new();
    readonly ChannelLabSecureTextStore _secureTextStore = new();
    readonly Dictionary<string, PeerSession> _peerSessions = new(StringComparer.OrdinalIgnoreCase);
    HubConnection? _hub;
    SecureTextDeviceIdentity? _identity;
    SecureTextPublicBundle? _publicBundle;
    string? _token;

    public string? Nick { get; private set; }
    public string Channel { get; private set; } = "#lobby";
    public bool IsConnected => _hub?.State == HubConnectionState.Connected;

    public event Action<ChannelMessage>? MessageReceived;
    public event Action<IReadOnlyList<ChannelMessage>>? HistoryReceived;
    public event Action<IReadOnlyList<string>>? RosterChanged;
    public event Action<SignalMessage>? SignalReceived;
    public event Action<string>? StatusChanged;
    public event Action<string>? DeviceFingerprintAvailable;
    public event Action<PeerFingerprint>? PeerFingerprintAvailable;

    public async Task ConnectAsync(string nick, CancellationToken cancellationToken = default)
    {
        await DisposeHubAsync().ConfigureAwait(false);

        using var response = await _http.PostAsJsonAsync(
            HostEndpoints.GuestUri,
            new { nick },
            cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var guest = await response.Content.ReadFromJsonAsync<GuestLoginResponse>(JsonOptions, cancellationToken)
                    .ConfigureAwait(false)
                    ?? throw new InvalidOperationException("Empty guest response.");

        Nick = guest.Nick;
        _token = guest.AccessToken;
        _identity = await _secureTextStore.LoadOrCreateIdentityAsync(Nick, cancellationToken).ConfigureAwait(false);
        _publicBundle = SecureTextPublicBundle.Create(_identity);
        RaiseStatus($"Signed in as {Nick}");

        _hub = new HubConnectionBuilder()
            .WithUrl(HostEndpoints.HubUri, options =>
            {
                options.AccessTokenProvider = () => Task.FromResult(_token)!;
            })
            .WithAutomaticReconnect()
            .Build();

        _hub.On<SecureTextRelayEnvelopeDto>("SecureText", HandleSecureTextAsync);

        _hub.On<RosterDto>("Roster", dto =>
            RosterChanged?.Invoke(dto.Nicks));

        _hub.On<SignalEnvelopeDto>("Signal", dto =>
            SignalReceived?.Invoke(new SignalMessage(dto.Channel, dto.FromNick, dto.Kind, dto.Payload ?? string.Empty, dto.ToNick)));

        _hub.Reconnecting += _ =>
        {
            RaiseStatus("Reconnecting…");
            return Task.CompletedTask;
        };
        _hub.Reconnected += async _ =>
        {
            RaiseStatus("Reconnected");
            await JoinAsync(Channel, CancellationToken.None).ConfigureAwait(false);
            await RegisterDeviceAsync(CancellationToken.None).ConfigureAwait(false);
            await LoadSecureHistoryAsync(CancellationToken.None).ConfigureAwait(false);
        };
        _hub.Closed += error =>
        {
            RaiseStatus(error is null ? "Disconnected" : $"Disconnected: {error.Message}");
            return Task.CompletedTask;
        };

        await _hub.StartAsync(cancellationToken).ConfigureAwait(false);
        RaiseStatus("Connected");
        await JoinAsync("#lobby", cancellationToken).ConfigureAwait(false);
        await RegisterDeviceAsync(cancellationToken).ConfigureAwait(false);
        DeviceFingerprintAvailable?.Invoke(_publicBundle!.GetFingerprint());
        await LoadSecureHistoryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task JoinAsync(string channel, CancellationToken cancellationToken = default)
    {
        EnsureHub();
        Channel = channel.StartsWith('#') ? channel : "#" + channel;
        await _hub!.InvokeAsync("Join", Channel, cancellationToken).ConfigureAwait(false);
        RaiseStatus($"Joined {Channel}");
    }

    public async Task PreparePeerTrustAsync(string nick, CancellationToken cancellationToken = default)
    {
        var bundle = await GetPeerBundleAsync(nick, cancellationToken).ConfigureAwait(false);
        var trusted = await _secureTextStore.LoadTrustedPeerAsync(RequireNick(), nick, cancellationToken).ConfigureAwait(false);
        var isTrusted = false;
        if (trusted is not null)
        {
            try
            {
                trusted.VerifyBundle(bundle);
                isTrusted = true;
            }
            catch (CryptographicException)
            {
                isTrusted = false;
            }
        }

        PeerFingerprintAvailable?.Invoke(new PeerFingerprint(nick, bundle.GetFingerprint(), isTrusted));
    }

    public async Task ConfirmPeerTrustAsync(string nick, CancellationToken cancellationToken = default)
    {
        var bundle = await GetPeerBundleAsync(nick, cancellationToken).ConfigureAwait(false);
        var trusted = new SecureTextTrustedPeer(bundle);
        await _secureTextStore.StoreTrustedPeerAsync(RequireNick(), nick, trusted, cancellationToken).ConfigureAwait(false);
        if (_peerSessions.Remove(nick, out var old))
            old.Dispose();

        PeerFingerprintAvailable?.Invoke(new PeerFingerprint(nick, trusted.GetFingerprint(), true));
        RaiseStatus($"Trusted {nick}'s secure-text device.");
    }

    public async Task SayAsync(string toNick, string body, CancellationToken cancellationToken = default)
    {
        EnsureHub();
        body = body?.Trim() ?? string.Empty;
        if (body.Length == 0)
            throw new ArgumentException("Protected text cannot be empty.", nameof(body));
        if (string.Equals(toNick, RequireNick(), StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Choose a different peer.", nameof(toNick));

        var peer = await GetPeerSessionAsync(toNick, cancellationToken).ConfigureAwait(false);
        var envelope = peer.Session.Protect(body);
        await _secureTextStore.StoreConversationStateAsync(
                RequireNick(),
                peer.Session.LocalDeviceId,
                peer.Session.PeerDeviceId,
                peer.Session.State,
                cancellationToken)
            .ConfigureAwait(false);
        var encoded = SecureTextEnvelopeCodec.Serialize(envelope);
        await _hub!.InvokeAsync("SendSecureText", Channel, toNick, encoded, cancellationToken).ConfigureAwait(false);
        MessageReceived?.Invoke(new ChannelMessage(Channel, RequireNick(), body, envelope.Header.SentAtUtc));
    }

    public async Task SendSignalAsync(
        string kind,
        string payload,
        string? toNick = null,
        CancellationToken cancellationToken = default)
    {
        EnsureHub();
        await _hub!.InvokeAsync("Signal", Channel, kind, payload, toNick, cancellationToken).ConfigureAwait(false);
    }

    public async Task PartAsync(CancellationToken cancellationToken = default)
    {
        if (_hub is null)
            return;
        await _hub.InvokeAsync("Part", Channel, cancellationToken).ConfigureAwait(false);
    }

    void EnsureHub()
    {
        if (_hub is null || _hub.State != HubConnectionState.Connected)
            throw new InvalidOperationException("Not connected.");
    }

    void RaiseStatus(string status) => StatusChanged?.Invoke(status);

    async Task DisposeHubAsync()
    {
        if (_hub is null)
            return;
        try
        {
            await _hub.DisposeAsync().ConfigureAwait(false);
        }
        catch
        {
            // ignore
        }
        _hub = null;
        foreach (var peer in _peerSessions.Values)
            peer.Dispose();
        _peerSessions.Clear();
    }

    public async ValueTask DisposeAsync()
    {
        await DisposeHubAsync().ConfigureAwait(false);
        _http.Dispose();
    }

    sealed record GuestLoginResponse(string AccessToken, string Nick, Guid PlayerId, DateTimeOffset ExpiresAtUtc);
    sealed record DeviceBundleDto(
        int ProtocolVersion,
        Guid DeviceId,
        byte[] SigningPublicKey,
        byte[] AgreementPublicKey,
        DateTimeOffset IssuedAtUtc,
        DateTimeOffset ExpiresAtUtc,
        byte[] Signature);
    sealed record SecureTextRelayEnvelopeDto(string Channel, string FromNick, string ToNick, byte[] Envelope);
    sealed record RosterDto(string Channel, [property: JsonPropertyName("nicks")] IReadOnlyList<string> Nicks);
    sealed record SignalEnvelopeDto(string Channel, string FromNick, string Kind, string? Payload, string? ToNick);

    async Task RegisterDeviceAsync(CancellationToken cancellationToken)
    {
        EnsureHub();
        if (_publicBundle is null)
            throw new InvalidOperationException("A local secure-text device is not available.");

        await _hub!.InvokeAsync("RegisterDevice", Channel, ToDto(_publicBundle), cancellationToken).ConfigureAwait(false);
    }

    async Task LoadSecureHistoryAsync(CancellationToken cancellationToken)
    {
        EnsureHub();
        var history = await _hub!.InvokeAsync<List<SecureTextRelayEnvelopeDto>>(
                "GetSecureHistory",
                Channel,
                cancellationToken)
            .ConfigureAwait(false);
        if (history is null || history.Count == 0)
        {
            HistoryReceived?.Invoke([]);
            return;
        }

        var messages = new List<ChannelMessage>();
        foreach (var relay in history)
        {
            try
            {
                var envelope = SecureTextEnvelopeCodec.Deserialize(relay.Envelope);
                var ownDeviceId = SecureTextDeviceId.FromGuid(_identity?.DeviceId
                    ?? throw new InvalidOperationException("A local secure-text device is not available."));
                var peerNick = envelope.Header.SenderDeviceId == ownDeviceId ? relay.ToNick : relay.FromNick;
                var peer = await GetPeerSessionAsync(peerNick, cancellationToken).ConfigureAwait(false);
                var received = peer.Session.OpenHistory(envelope);
                var nick = received.SenderDeviceId == ownDeviceId ? RequireNick() : relay.FromNick;
                messages.Add(new ChannelMessage(relay.Channel, nick, received.Text, received.SentAtUtc));
            }
            catch (Exception exception) when (exception is CryptographicException or InvalidDataException or InvalidOperationException)
            {
                RaiseStatus("Skipped an untrusted or invalid protected history entry.");
            }
        }

        HistoryReceived?.Invoke(messages);
    }

    async Task HandleSecureTextAsync(SecureTextRelayEnvelopeDto relay)
    {
        if (!string.Equals(relay.ToNick, Nick, StringComparison.OrdinalIgnoreCase))
            return;

        try
        {
            var envelope = SecureTextEnvelopeCodec.Deserialize(relay.Envelope);
            var peer = await GetPeerSessionAsync(relay.FromNick, CancellationToken.None).ConfigureAwait(false);
            var received = peer.Session.Open(envelope);
            await _secureTextStore.StoreConversationStateAsync(
                    RequireNick(),
                    peer.Session.LocalDeviceId,
                    peer.Session.PeerDeviceId,
                    peer.Session.State,
                    CancellationToken.None)
                .ConfigureAwait(false);
            MessageReceived?.Invoke(new ChannelMessage(relay.Channel, relay.FromNick, received.Text, received.SentAtUtc));
        }
        catch (Exception exception) when (exception is CryptographicException or InvalidDataException or InvalidOperationException)
        {
            RaiseStatus($"Protected text from {relay.FromNick} needs verified peer trust.");
        }
    }

    async Task<PeerSession> GetPeerSessionAsync(string nick, CancellationToken cancellationToken)
    {
        var bundle = await GetPeerBundleAsync(nick, cancellationToken).ConfigureAwait(false);
        if (_peerSessions.TryGetValue(nick, out var current)
            && string.Equals(current.Fingerprint, bundle.GetFingerprint(), StringComparison.Ordinal))
        {
            return current;
        }

        if (_peerSessions.Remove(nick, out var prior))
            prior.Dispose();

        var trusted = await _secureTextStore.LoadTrustedPeerAsync(RequireNick(), nick, cancellationToken).ConfigureAwait(false);
        if (trusted is null)
        {
            PeerFingerprintAvailable?.Invoke(new PeerFingerprint(nick, bundle.GetFingerprint(), false));
            throw new CryptographicException($"Peer '{nick}' has not been confirmed.");
        }

        try
        {
            trusted.VerifyBundle(bundle);
        }
        catch (CryptographicException)
        {
            PeerFingerprintAvailable?.Invoke(new PeerFingerprint(nick, bundle.GetFingerprint(), false));
            throw;
        }

        var localIdentity = _identity ?? throw new InvalidOperationException("A local secure-text device is not available.");
        var localBundle = _publicBundle ?? throw new InvalidOperationException("A local public bundle is not available.");
        var localDeviceId = SecureTextDeviceId.FromGuid(localIdentity.DeviceId);
        var peerDeviceId = SecureTextDeviceId.FromGuid(bundle.DeviceId);
        var state = await _secureTextStore.LoadConversationStateAsync(
                RequireNick(),
                localDeviceId,
                peerDeviceId,
                cancellationToken)
            .ConfigureAwait(false);
        var session = new SecureTextSession(
            localIdentity,
            localBundle,
            bundle,
            trusted,
            SecureTextConversationId.DeriveForPair(localDeviceId, peerDeviceId),
            state);
        var created = new PeerSession(bundle.GetFingerprint(), session);
        _peerSessions[nick] = created;
        return created;
    }

    async Task<SecureTextPublicBundle> GetPeerBundleAsync(string nick, CancellationToken cancellationToken)
    {
        EnsureHub();
        if (string.Equals(nick, RequireNick(), StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("A peer must be a different nick.", nameof(nick));

        var dto = await _hub!.InvokeAsync<DeviceBundleDto?>("GetDeviceBundle", Channel, nick, cancellationToken)
            .ConfigureAwait(false);
        if (dto is null)
            throw new InvalidOperationException($"No secure-text device is registered for '{nick}'.");

        return ToPublicBundle(dto);
    }

    string RequireNick() => Nick ?? throw new InvalidOperationException("No signed-in nick is available.");

    static DeviceBundleDto ToDto(SecureTextPublicBundle bundle) =>
        new(
            bundle.ProtocolVersion,
            bundle.DeviceId,
            bundle.ExportSigningPublicKey(),
            bundle.ExportAgreementPublicKey(),
            bundle.IssuedAtUtc,
            bundle.ExpiresAtUtc,
            bundle.ExportSignature());

    static SecureTextPublicBundle ToPublicBundle(DeviceBundleDto dto) =>
        new(
            dto.ProtocolVersion,
            dto.DeviceId,
            dto.SigningPublicKey,
            dto.AgreementPublicKey,
            dto.IssuedAtUtc,
            dto.ExpiresAtUtc,
            dto.Signature);

    sealed class PeerSession(string fingerprint, SecureTextSession session) : IDisposable
    {
        public string Fingerprint { get; } = fingerprint;
        public SecureTextSession Session { get; } = session;

        public void Dispose() => Session.Dispose();
    }
}

internal sealed record ChannelMessage(string Channel, string Nick, string Body, DateTimeOffset At);

internal sealed record SignalMessage(string Channel, string FromNick, string Kind, string Payload, string? ToNick);

internal sealed record PeerFingerprint(string Nick, string Fingerprint, bool IsTrusted);
