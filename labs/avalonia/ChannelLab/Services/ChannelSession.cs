using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.SignalR.Client;

namespace ChannelLab.Services;

internal sealed class ChannelSession : IAsyncDisposable
{
    static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    readonly HttpClient _http = new();
    HubConnection? _hub;
    string? _token;

    public string? Nick { get; private set; }
    public string Channel { get; private set; } = "#lobby";
    public bool IsConnected => _hub?.State == HubConnectionState.Connected;

    public event Action<ChannelMessage>? MessageReceived;
    public event Action<IReadOnlyList<ChannelMessage>>? HistoryReceived;
    public event Action<IReadOnlyList<string>>? RosterChanged;
    public event Action<SignalMessage>? SignalReceived;
    public event Action<string>? StatusChanged;

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
        RaiseStatus($"Signed in as {Nick}");

        _hub = new HubConnectionBuilder()
            .WithUrl(HostEndpoints.HubUri, options =>
            {
                options.AccessTokenProvider = () => Task.FromResult(_token)!;
            })
            .WithAutomaticReconnect()
            .Build();

        _hub.On<ChannelMessageDto>("Message", dto =>
            MessageReceived?.Invoke(new ChannelMessage(dto.Channel, dto.Nick, dto.Body, dto.At)));

        _hub.On<List<ChannelMessageDto>>("History", list =>
            HistoryReceived?.Invoke(list.Select(d => new ChannelMessage(d.Channel, d.Nick, d.Body, d.At)).ToList()));

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
        };
        _hub.Closed += error =>
        {
            RaiseStatus(error is null ? "Disconnected" : $"Disconnected: {error.Message}");
            return Task.CompletedTask;
        };

        await _hub.StartAsync(cancellationToken).ConfigureAwait(false);
        RaiseStatus("Connected");
        await JoinAsync("#lobby", cancellationToken).ConfigureAwait(false);
    }

    public async Task JoinAsync(string channel, CancellationToken cancellationToken = default)
    {
        EnsureHub();
        Channel = channel.StartsWith('#') ? channel : "#" + channel;
        await _hub!.InvokeAsync("Join", Channel, cancellationToken).ConfigureAwait(false);
        RaiseStatus($"Joined {Channel}");
    }

    public async Task SayAsync(string body, CancellationToken cancellationToken = default)
    {
        EnsureHub();
        await _hub!.InvokeAsync("Say", Channel, body, cancellationToken).ConfigureAwait(false);
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
    }

    public async ValueTask DisposeAsync()
    {
        await DisposeHubAsync().ConfigureAwait(false);
        _http.Dispose();
    }

    sealed record GuestLoginResponse(string AccessToken, string Nick, Guid PlayerId, DateTimeOffset ExpiresAtUtc);
    sealed record ChannelMessageDto(string Channel, string Nick, string Body, DateTimeOffset At);
    sealed record RosterDto(string Channel, [property: JsonPropertyName("nicks")] IReadOnlyList<string> Nicks);
    sealed record SignalEnvelopeDto(string Channel, string FromNick, string Kind, string? Payload, string? ToNick);
}

internal sealed record ChannelMessage(string Channel, string Nick, string Body, DateTimeOffset At);

internal sealed record SignalMessage(string Channel, string FromNick, string Kind, string Payload, string? ToNick);
