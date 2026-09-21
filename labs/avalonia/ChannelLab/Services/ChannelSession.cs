using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;
using Novolis.Chat.Abstractions;
using Novolis.Chat.Directory;
using Novolis.Chat.Hosting.AspNetCore;
using Novolis.Messaging.SecureText;
using Novolis.Security.SecureText;

namespace ChannelLab.Services;

internal sealed class ChannelSession : IAsyncDisposable
{
    const int MaxSecureTextGroupMembers = 8;

    static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    readonly HttpClient _http = new();
    readonly ChannelLabSecureTextStore _secureTextStore = new();
    readonly Dictionary<PeerSessionKey, PeerSession> _peerSessions = [];
    readonly Dictionary<Guid, SecureTextGroup> _secureTextGroups = [];
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
    public event Action<IReadOnlyList<SecureTextGroup>>? GroupsChanged;
    public event Action<IReadOnlyList<ChannelMessage>>? GroupHistoryReceived;
    public event Action<IReadOnlyList<ChatPresence>>? PresenceChanged;
    public event Action<IReadOnlyList<ChatTyping>>? TypingChanged;
    public event Action<ChatReceipt>? ReceiptReceived;

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
        _hub.On<SecureTextGroupRelayEnvelopeDto>("SecureTextGroup", HandleSecureTextGroupAsync);
        _hub.On<SecureTextGroupDto>("SecureTextGroupMembership", HandleSecureTextGroupMembershipAsync);

        _hub.On<ChatRosterDto>("Roster", dto =>
            RosterChanged?.Invoke(dto.Nicks));

        _hub.On<ChatSignalEnvelope>("Signal", dto =>
            SignalReceived?.Invoke(new SignalMessage(dto.Channel, dto.FromNick, dto.Kind, dto.Payload, dto.ToNick)));
        _hub.On<List<ChatPresence>>("Presence", values =>
            PresenceChanged?.Invoke(values));
        _hub.On<List<ChatTyping>>("Typing", values =>
            TypingChanged?.Invoke(values));
        _hub.On<ChatReceipt>("Receipt", receipt =>
            ReceiptReceived?.Invoke(receipt));

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
            await LoadSecureTextGroupsAsync(CancellationToken.None).ConfigureAwait(false);
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
        await LoadSecureTextGroupsAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task JoinAsync(string channel, CancellationToken cancellationToken = default)
    {
        EnsureHub();
        Channel = channel.StartsWith('#') ? channel : "#" + channel;
        await _hub!.InvokeAsync("Join", Channel, cancellationToken).ConfigureAwait(false);
        RaiseStatus($"Joined {Channel}");
    }

    public async Task SwitchChannelAsync(
        string channel,
        CancellationToken cancellationToken = default)
    {
        await JoinAsync(channel, cancellationToken).ConfigureAwait(false);
        await RegisterDeviceAsync(cancellationToken).ConfigureAwait(false);
        await LoadSecureHistoryAsync(cancellationToken).ConfigureAwait(false);
        await LoadSecureTextGroupsAsync(cancellationToken).ConfigureAwait(false);
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
        foreach (var key in _peerSessions.Keys
                     .Where(key => string.Equals(key.Nick, nick, StringComparison.OrdinalIgnoreCase))
                     .ToArray())
        {
            if (_peerSessions.Remove(key, out var session))
                session.Dispose();
        }

        PeerFingerprintAvailable?.Invoke(new PeerFingerprint(nick, trusted.GetFingerprint(), true));
        RaiseStatus($"Trusted {nick}'s secure-text device.");
    }

    public async Task SayAsync(
        string toNick,
        string body,
        ChatFrameAnnotations? annotations = null,
        CancellationToken cancellationToken = default)
    {
        body ??= string.Empty;
        await SayAsync(
                toNick,
                MarkdownBody.FromRaw(body),
                annotations,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task SayAsync(
        string toNick,
        MarkdownBody body,
        ChatFrameAnnotations? annotations = null,
        CancellationToken cancellationToken = default)
    {
        EnsureHub();
        if (body.IsEmpty)
            throw new ArgumentException("Protected text cannot be empty.", nameof(body));
        if (string.Equals(toNick, RequireNick(), StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Choose a different peer.", nameof(toNick));

        var peer = await GetPeerSessionAsync(toNick, cancellationToken).ConfigureAwait(false);
        var envelope = peer.Session.Protect(body.Source);
        await _secureTextStore.StoreConversationStateAsync(
                RequireNick(),
                peer.Session.LocalDeviceId,
                peer.Session.PeerDeviceId,
                peer.Session.State,
                cancellationToken)
            .ConfigureAwait(false);
        var encoded = SecureTextEnvelopeCodec.Serialize(envelope);
        if (annotations is null)
        {
            await _hub!.InvokeAsync(
                    "SendSecureText",
                    Channel,
                    toNick,
                    encoded,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            await _hub!.InvokeAsync(
                    "SendSecureTextWithAnnotations",
                    Channel,
                    toNick,
                    encoded,
                    annotations,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        MessageReceived?.Invoke(new ChannelMessage(
            Channel,
            RequireNick(),
            body,
            envelope.Header.SentAtUtc,
            ChatFrame.Create(
                Channel,
                envelope.Header.MessageId,
                RequireNick(),
                toNick,
                envelope.Header.SentAtUtc,
                annotations?.Thread,
                annotations?.Parent,
                annotations?.Reaction)));
    }

    public async Task<SecureTextGroupId> CreateSecureTextGroupAsync(
        string name,
        IReadOnlyList<string> peerNicks,
        CancellationToken cancellationToken = default)
    {
        EnsureHub();
        name = name?.Trim() ?? string.Empty;
        if (name.Length is < 1 or > 48)
            throw new ArgumentException("A protected group name of 1–48 characters is required.", nameof(name));

        var peers = peerNicks
            .Where(peer => !string.IsNullOrWhiteSpace(peer)
                           && !string.Equals(peer, RequireNick(), StringComparison.OrdinalIgnoreCase))
            .Select(peer => peer.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (peers.Length is < 1 or >= MaxSecureTextGroupMembers)
            throw new ArgumentException("Choose between one and seven distinct trusted peers.", nameof(peerNicks));

        var members = new List<SecureTextGroupMemberDto>
        {
            new(RequireNick(), _identity?.DeviceId ?? throw new InvalidOperationException("A local secure-text device is not available.")),
        };
        foreach (var peerNick in peers)
        {
            var peer = await GetPeerSessionAsync(peerNick, cancellationToken).ConfigureAwait(false);
            members.Add(new SecureTextGroupMemberDto(peerNick, peer.Session.PeerDeviceId.Value));
        }

        var groupId = SecureTextGroupId.New();
        await _secureTextStore.StoreGroupApprovalAsync(
                RequireNick(),
                groupId,
                members.Select(member => SecureTextDeviceId.FromGuid(member.DeviceId)).ToArray(),
                cancellationToken)
            .ConfigureAwait(false);
        await _hub!.InvokeAsync(
                "CreateSecureTextGroup",
                Channel,
                groupId.Value,
                name,
                members,
                cancellationToken)
            .ConfigureAwait(false);
        return groupId;
    }

    public async Task ApproveSecureTextGroupAsync(
        SecureTextGroupId groupId,
        CancellationToken cancellationToken = default)
    {
        EnsureHub();
        if (!_secureTextGroups.TryGetValue(groupId.Value, out var group))
            throw new InvalidOperationException("The protected group proposal is not available.");
        var localGroupDeviceId = _identity?.DeviceId
            ?? throw new InvalidOperationException("A local secure-text device is not available.");
        if (!group.Members.Any(member => member.DeviceId.Value == localGroupDeviceId))
            throw new InvalidOperationException("The local device is not a protected group member.");

        foreach (var member in group.Members.Where(member => member.DeviceId.Value != localGroupDeviceId))
        {
            await GetPeerSessionAsync(
                    member.Nick,
                    cancellationToken,
                    groupId,
                    member.DeviceId)
                .ConfigureAwait(false);
        }

        await _secureTextStore.StoreGroupApprovalAsync(
                RequireNick(),
                groupId,
                group.Members.Select(member => member.DeviceId).ToArray(),
                cancellationToken)
            .ConfigureAwait(false);
        await _hub!.InvokeAsync("ApproveSecureTextGroup", Channel, groupId.Value, cancellationToken).ConfigureAwait(false);
    }

    public async Task SayToSecureTextGroupAsync(
        SecureTextGroupId groupId,
        string body,
        CancellationToken cancellationToken = default)
    {
        body ??= string.Empty;
        await SayToSecureTextGroupAsync(
                groupId,
                MarkdownBody.FromRaw(body),
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task SayToSecureTextGroupAsync(
        SecureTextGroupId groupId,
        MarkdownBody body,
        CancellationToken cancellationToken = default)
    {
        EnsureHub();
        if (body.IsEmpty)
            throw new ArgumentException("Protected text cannot be empty.", nameof(body));
        if (!_secureTextGroups.TryGetValue(groupId.Value, out var group) || !group.IsActive)
            throw new InvalidOperationException("Every group device must approve the current membership before messaging.");

        var localDeviceId = SecureTextDeviceId.FromGuid(_identity?.DeviceId
            ?? throw new InvalidOperationException("A local secure-text device is not available."));
        var envelopes = new List<SecureTextGroupEnvelopeInputDto>();
        DateTimeOffset? sentAtUtc = null;
        foreach (var member in group.Members.Where(member => member.DeviceId != localDeviceId))
        {
            var peer = await GetPeerSessionAsync(
                    member.Nick,
                    cancellationToken,
                    groupId,
                    member.DeviceId)
                .ConfigureAwait(false);
            var envelope = peer.Session.Protect(body.Source);
            sentAtUtc ??= envelope.Header.SentAtUtc;
            await StoreConversationStateAsync(peer, cancellationToken).ConfigureAwait(false);
            envelopes.Add(new SecureTextGroupEnvelopeInputDto(
                member.DeviceId.Value,
                SecureTextEnvelopeCodec.Serialize(envelope)));
        }

        await _hub!.InvokeAsync(
                "SendSecureTextGroup",
                Channel,
                groupId.Value,
                envelopes,
                cancellationToken)
            .ConfigureAwait(false);
        MessageReceived?.Invoke(new ChannelMessage(
            GroupContext(group),
            RequireNick(),
            body,
            sentAtUtc ?? DateTimeOffset.UtcNow));
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

    public async Task SetTypingAsync(
        bool isTyping,
        string? conversation = null,
        CancellationToken cancellationToken = default)
    {
        EnsureHub();
        await _hub!.InvokeAsync(
                "Typing",
                Channel,
                isTyping,
                conversation,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task AcknowledgeAsync(
        Guid messageId,
        CancellationToken cancellationToken = default)
    {
        EnsureHub();
        await _hub!.InvokeAsync("Receipt", Channel, messageId, cancellationToken).ConfigureAwait(false);
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
        _secureTextGroups.Clear();
    }

    public async ValueTask DisposeAsync()
    {
        await DisposeHubAsync().ConfigureAwait(false);
        _http.Dispose();
    }

    sealed record GuestLoginResponse(string AccessToken, string Nick, Guid PlayerId, DateTimeOffset ExpiresAtUtc);

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
                messages.Add(new ChannelMessage(
                    relay.Channel,
                    nick,
                    MarkdownBody.FromRaw(received.Text),
                    received.SentAtUtc,
                    relay.Frame));
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
            MessageReceived?.Invoke(new ChannelMessage(
                relay.Channel,
                relay.FromNick,
                MarkdownBody.FromRaw(received.Text),
                received.SentAtUtc,
                relay.Frame));
        }
        catch (Exception exception) when (exception is CryptographicException or InvalidDataException or InvalidOperationException)
        {
            RaiseStatus($"Protected text from {relay.FromNick} needs verified peer trust.");
        }
    }

    async Task LoadSecureTextGroupsAsync(CancellationToken cancellationToken)
    {
        EnsureHub();
        var groups = await _hub!.InvokeAsync<List<SecureTextGroupDto>>(
                "GetSecureTextGroups",
                Channel,
                cancellationToken)
            .ConfigureAwait(false);
        if (groups is null)
            return;

        foreach (var group in groups)
            await HandleSecureTextGroupMembershipAsync(group).ConfigureAwait(false);
    }

    async Task HandleSecureTextGroupMembershipAsync(SecureTextGroupDto dto)
    {
        SecureTextGroup group;
        try
        {
            group = ToSecureTextGroup(dto);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidDataException)
        {
            RaiseStatus("Ignored an invalid protected group membership update.");
            return;
        }

        var localDeviceId = _identity?.DeviceId;
        if (localDeviceId is null || !group.Members.Any(member => member.DeviceId.Value == localDeviceId.Value))
            return;

        if (_secureTextGroups.TryGetValue(group.GroupId.Value, out var previous)
            && !HasSameRoster(previous, group))
        {
            RaiseStatus("Ignored a protected group update that attempted to change its approved roster.");
            return;
        }

        group = group with
        {
            IsLocallyApproved = await _secureTextStore.HasGroupApprovalAsync(
                    RequireNick(),
                    group.GroupId,
                    group.Members.Select(member => member.DeviceId).ToArray(),
                    CancellationToken.None)
                .ConfigureAwait(false),
        };
        var wasActive = previous is { IsActive: true };
        _secureTextGroups[group.GroupId.Value] = group;
        GroupsChanged?.Invoke(_secureTextGroups.Values.OrderBy(value => value.Name, StringComparer.OrdinalIgnoreCase).ToArray());

        if (group.IsActive && !wasActive)
        {
            try
            {
                await LoadSecureTextGroupHistoryAsync(group, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is CryptographicException or InvalidDataException or InvalidOperationException)
            {
                RaiseStatus("Protected group history needs verified peer trust.");
            }
        }
    }

    async Task HandleSecureTextGroupAsync(SecureTextGroupRelayEnvelopeDto relay)
    {
        if (!string.Equals(relay.ToNick, Nick, StringComparison.OrdinalIgnoreCase)
            || !_secureTextGroups.TryGetValue(relay.GroupId, out var group)
            || !group.IsActive)
        {
            return;
        }

        try
        {
            var sender = group.Members.SingleOrDefault(member =>
                string.Equals(member.Nick, relay.FromNick, StringComparison.OrdinalIgnoreCase));
            if (sender is null)
                throw new InvalidDataException("The sender is not in the protected group membership.");

            var envelope = SecureTextEnvelopeCodec.Deserialize(relay.Envelope);
            var peer = await GetPeerSessionAsync(
                    sender.Nick,
                    CancellationToken.None,
                    group.GroupId,
                    sender.DeviceId)
                .ConfigureAwait(false);
            var received = peer.Session.Open(envelope);
            await StoreConversationStateAsync(peer, CancellationToken.None).ConfigureAwait(false);
            MessageReceived?.Invoke(new ChannelMessage(
                GroupContext(group),
                relay.FromNick,
                MarkdownBody.FromRaw(received.Text),
                received.SentAtUtc,
                relay.Frame));
        }
        catch (Exception exception) when (exception is CryptographicException or InvalidDataException or InvalidOperationException)
        {
            RaiseStatus($"Protected group text from {relay.FromNick} needs verified group membership.");
        }
    }

    async Task LoadSecureTextGroupHistoryAsync(SecureTextGroup group, CancellationToken cancellationToken)
    {
        EnsureHub();
        var history = await _hub!.InvokeAsync<List<SecureTextGroupRelayEnvelopeDto>>(
                "GetSecureTextGroupHistory",
                Channel,
                group.GroupId.Value,
                cancellationToken)
            .ConfigureAwait(false);
        if (history is null || history.Count == 0)
            return;

        var localDeviceId = SecureTextDeviceId.FromGuid(_identity?.DeviceId
            ?? throw new InvalidOperationException("A local secure-text device is not available."));
        var messages = new List<ChannelMessage>();
        var displayedOwnMessages = new HashSet<string>(StringComparer.Ordinal);
        foreach (var relay in history)
        {
            try
            {
                var envelope = SecureTextEnvelopeCodec.Deserialize(relay.Envelope);
                var senderIsLocal = envelope.Header.SenderDeviceId == localDeviceId;
                var peerNick = senderIsLocal ? relay.ToNick : relay.FromNick;
                var peerMember = group.Members.SingleOrDefault(member =>
                    string.Equals(member.Nick, peerNick, StringComparison.OrdinalIgnoreCase))
                    ?? throw new InvalidDataException("The history peer is not in the protected group membership.");
                var peer = await GetPeerSessionAsync(
                        peerMember.Nick,
                        cancellationToken,
                        group.GroupId,
                        peerMember.DeviceId)
                    .ConfigureAwait(false);
                var received = peer.Session.OpenHistory(envelope);
                var senderNick = senderIsLocal ? RequireNick() : relay.FromNick;
                var ownMessageKey = $"{received.SenderDeviceId.Value:N}:{received.SentAtUtc.UtcDateTime.Ticks}:{received.Text}";
                if (senderIsLocal && !displayedOwnMessages.Add(ownMessageKey))
                    continue;

                messages.Add(new ChannelMessage(
                    GroupContext(group),
                    senderNick,
                    MarkdownBody.FromRaw(received.Text),
                    received.SentAtUtc,
                    relay.Frame));
            }
            catch (Exception exception) when (exception is CryptographicException or InvalidDataException or InvalidOperationException)
            {
                RaiseStatus("Skipped an untrusted or invalid protected group history entry.");
            }
        }

        if (messages.Count > 0)
            GroupHistoryReceived?.Invoke(messages);
    }

    async Task<PeerSession> GetPeerSessionAsync(
        string nick,
        CancellationToken cancellationToken,
        SecureTextGroupId? groupId = null,
        SecureTextDeviceId? expectedPeerDeviceId = null)
    {
        var bundle = await GetPeerBundleAsync(nick, cancellationToken).ConfigureAwait(false);
        if (expectedPeerDeviceId is { } expected && bundle.DeviceId != expected.Value)
            throw new CryptographicException($"Peer '{nick}' no longer has the device approved for this protected group.");

        var key = PeerSessionKey.Create(nick, groupId);
        if (_peerSessions.TryGetValue(key, out var current)
            && string.Equals(current.Fingerprint, bundle.GetFingerprint(), StringComparison.Ordinal))
        {
            return current;
        }

        if (_peerSessions.Remove(key, out var prior))
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
        var state = groupId is { } group
            ? await _secureTextStore.LoadGroupConversationStateAsync(
                    RequireNick(),
                    group,
                    localDeviceId,
                    peerDeviceId,
                    cancellationToken)
                .ConfigureAwait(false)
            : await _secureTextStore.LoadConversationStateAsync(
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
            groupId is { } groupConversation
                ? SecureTextConversationId.DeriveForGroupMember(groupConversation, localDeviceId, peerDeviceId)
                : SecureTextConversationId.DeriveForPair(localDeviceId, peerDeviceId),
            state);
        var created = new PeerSession(bundle.GetFingerprint(), session, groupId);
        _peerSessions[key] = created;
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

    async Task StoreConversationStateAsync(PeerSession peer, CancellationToken cancellationToken)
    {
        if (peer.GroupId is { } groupId)
        {
            await _secureTextStore.StoreGroupConversationStateAsync(
                    RequireNick(),
                    groupId,
                    peer.Session.LocalDeviceId,
                    peer.Session.PeerDeviceId,
                    peer.Session.State,
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        await _secureTextStore.StoreConversationStateAsync(
                RequireNick(),
                peer.Session.LocalDeviceId,
                peer.Session.PeerDeviceId,
                peer.Session.State,
                cancellationToken)
            .ConfigureAwait(false);
    }

    static SecureTextGroup ToSecureTextGroup(SecureTextGroupDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var groupId = SecureTextGroupId.FromGuid(dto.GroupId);
        var name = dto.Name?.Trim() ?? string.Empty;
        if (name.Length is < 1 or > 48
            || string.IsNullOrWhiteSpace(dto.InitiatorNick)
            || dto.Members is null
            || dto.Members.Count is < 2 or > MaxSecureTextGroupMembers)
        {
            throw new InvalidDataException("The protected group membership is invalid.");
        }

        var members = dto.Members
            .Select(member => new SecureTextGroupMember(
                member.Nick?.Trim() ?? string.Empty,
                SecureTextDeviceId.FromGuid(member.DeviceId)))
            .ToArray();
        if (members.Any(member => string.IsNullOrWhiteSpace(member.Nick))
            || members.Select(member => member.DeviceId).Distinct().Count() != members.Length
            || members.Select(member => member.Nick).Distinct(StringComparer.OrdinalIgnoreCase).Count() != members.Length
            || dto.ApprovedDeviceIds is null
            || dto.ApprovedDeviceIds.Distinct().Count() != dto.ApprovedDeviceIds.Count
            || dto.ApprovedDeviceIds.Any(deviceId => members.All(member => member.DeviceId.Value != deviceId)))
        {
            throw new InvalidDataException("The protected group membership is invalid.");
        }

        return new SecureTextGroup(groupId, name, dto.InitiatorNick.Trim(), members, dto.ApprovedDeviceIds.ToArray(), false);
    }

    static bool HasSameRoster(SecureTextGroup first, SecureTextGroup second) =>
        first.Members.Count == second.Members.Count
        && first.Members.All(member => second.Members.Any(candidate =>
            candidate.DeviceId == member.DeviceId
            && string.Equals(candidate.Nick, member.Nick, StringComparison.OrdinalIgnoreCase)));

    static string GroupContext(SecureTextGroup group) => $"@{group.Name}";

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

    readonly record struct PeerSessionKey(string Nick, Guid? GroupId)
    {
        public static PeerSessionKey Create(string nick, SecureTextGroupId? groupId) =>
            new(nick.Trim().ToUpperInvariant(), groupId?.Value);
    }

    sealed class PeerSession(string fingerprint, SecureTextSession session, SecureTextGroupId? groupId) : IDisposable
    {
        public string Fingerprint { get; } = fingerprint;
        public SecureTextSession Session { get; } = session;
        public SecureTextGroupId? GroupId { get; } = groupId;

        public void Dispose() => Session.Dispose();
    }
}

internal sealed record ChannelMessage(
    string Channel,
    string Nick,
    MarkdownBody Body,
    DateTimeOffset At,
    ChatFrame? Frame = null);

internal sealed record SignalMessage(string Channel, string FromNick, string Kind, string Payload, string? ToNick);

internal sealed record PeerFingerprint(string Nick, string Fingerprint, bool IsTrusted);

internal sealed record SecureTextGroupMember(string Nick, SecureTextDeviceId DeviceId);

internal sealed record SecureTextGroup(
    SecureTextGroupId GroupId,
    string Name,
    string InitiatorNick,
    IReadOnlyList<SecureTextGroupMember> Members,
    IReadOnlyList<Guid> ApprovedDeviceIds,
    bool IsLocallyApproved)
{
    public bool IsActive =>
        IsLocallyApproved
        && Members.Count == ApprovedDeviceIds.Count
        && Members.All(member => ApprovedDeviceIds.Contains(member.DeviceId.Value));

    public override string ToString()
    {
        var roster = string.Join(", ", Members.Select(member => member.Nick));
        return IsActive ? $"{Name} ({roster}) · active" : $"{Name} ({roster}) · approval pending";
    }
}
