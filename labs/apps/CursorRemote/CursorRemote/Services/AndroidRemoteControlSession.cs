using System.Net.Http.Json;
using System.Text.Json;
using CursorRemote.Protocol;

namespace CursorRemote.Services;

public sealed class AndroidRemoteControlSession : IRemoteControlSession
{
    private readonly SemaphoreSlim _ioGate = new(1, 1);
    private HttpClient? _http;
    private RemoteStatusDto? _status;
    private RemoteScreenFrame? _screen;

    public bool IsHost => false;

    public bool IsConnected => _http is not null && _status is not null;

    public string Status { get; private set; } = "Not connected.";

    public RemoteConnectionInfo? Connection { get; private set; }

    public RemoteScreenFrame? Screen => _screen;

    public event EventHandler? Changed;

    public async Task ConnectAsync(
        string endpoint,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);

        var normalized = endpoint.Trim();
        if (!normalized.Contains("://", StringComparison.Ordinal))
            normalized = "http://" + normalized;
        if (!normalized.EndsWith("/", StringComparison.Ordinal))
            normalized += "/";

        if (!Uri.TryCreate(normalized, UriKind.Absolute, out var baseUri)
            || baseUri.Scheme is not ("http" or "https"))
        {
            throw new ArgumentException(
                "Endpoint must be an HTTP or HTTPS URL.",
                nameof(endpoint));
        }

        await DisconnectAsync().ConfigureAwait(false);

        var http = new HttpClient
        {
            BaseAddress = baseUri,
            Timeout = TimeSpan.FromSeconds(15),
        };

        try
        {
            using var helloResponse = await http.GetAsync(
                "api/v1/hello",
                cancellationToken).ConfigureAwait(false);
            helloResponse.EnsureSuccessStatusCode();
            var hello = await DeserializeAsync<RemoteHelloDto>(
                helloResponse,
                cancellationToken).ConfigureAwait(false);
            if (!string.Equals(hello.AppId, RemoteProtocol.AppId, StringComparison.Ordinal))
                throw new InvalidOperationException("That host is not Cursor Remote.");
            if (!RemoteProtocol.IsCompatible(hello.ProtocolVersion))
            {
                throw new InvalidOperationException(
                    $"Incompatible protocol {hello.ProtocolVersion} (need {RemoteProtocol.Version} line).");
            }

            using var response = await http.GetAsync(
                "api/v1/status",
                cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var status = await DeserializeAsync<RemoteStatusDto>(
                response,
                cancellationToken).ConfigureAwait(false);

            _http = http;
            _status = status;
            Connection = new RemoteConnectionInfo(
                baseUri.GetLeftPart(UriPartial.Authority),
                baseUri.Host,
                status.ProtocolVersion,
                status.HostName);
            Status = $"Connected to {status.HostName}.";
            RaiseChanged();
        }
        catch
        {
            http.Dispose();
            throw;
        }

        await RefreshAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        var http = RequireHttp();
        await _ioGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using var statusResponse = await http.GetAsync(
                "api/v1/status",
                cancellationToken).ConfigureAwait(false);
            statusResponse.EnsureSuccessStatusCode();
            var status = await DeserializeAsync<RemoteStatusDto>(
                statusResponse,
                cancellationToken).ConfigureAwait(false);

            using var response = await http.GetAsync(
                "api/v1/screen",
                cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var png = await response.Content.ReadAsByteArrayAsync(cancellationToken)
                .ConfigureAwait(false);

            _status = status;
            _screen = new RemoteScreenFrame(
                png,
                status.ScreenWidth,
                status.ScreenHeight,
                status.OriginX,
                status.OriginY);
            Status = $"Connected · {status.ScreenWidth}×{status.ScreenHeight}.";
            RaiseChanged();
        }
        finally
        {
            _ioGate.Release();
        }
    }

    public Task FocusCursorAsync(CancellationToken cancellationToken = default) =>
        PostOperationAsync("api/v1/focus", cancellationToken);

    public Task ClickAsync(
        double x,
        double y,
        string button = "left",
        int clickCount = 1,
        CancellationToken cancellationToken = default) =>
        PostOperationAsync(
            "api/v1/input/click",
            new RemoteClickRequest(x, y, button, clickCount),
            cancellationToken);

    public Task SendTextAsync(
        string text,
        CancellationToken cancellationToken = default) =>
        PostOperationAsync(
            "api/v1/input/text",
            new RemoteTextRequest(text),
            cancellationToken);

    public Task SendKeyAsync(
        string key,
        CancellationToken cancellationToken = default) =>
        PostOperationAsync(
            "api/v1/input/key",
            new RemoteKeyRequest(key),
            cancellationToken);

    public Task DisconnectAsync()
    {
        _http?.Dispose();
        _http = null;
        _status = null;
        _screen = null;
        Connection = null;
        Status = "Not connected.";
        RaiseChanged();
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync().ConfigureAwait(false);
        _ioGate.Dispose();
    }

    private async Task PostOperationAsync(
        string path,
        CancellationToken cancellationToken) =>
        await PostOperationAsync<object>(path, null, cancellationToken)
            .ConfigureAwait(false);

    private async Task PostOperationAsync(
        string path,
        object payload,
        CancellationToken cancellationToken) =>
        await PostOperationAsync<object>(path, payload, cancellationToken)
            .ConfigureAwait(false);

    private async Task<T> PostOperationAsync<T>(
        string path,
        object? payload,
        CancellationToken cancellationToken)
    {
        var http = RequireHttp();
        await _ioGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using var response = payload is null
                ? await http.PostAsync(path, null, cancellationToken).ConfigureAwait(false)
                : await http.PostAsJsonAsync(
                    path,
                    payload,
                    RemoteProtocol.JsonOptions,
                    cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var operation = await DeserializeAsync<RemoteOperationResponse>(
                response,
                cancellationToken).ConfigureAwait(false);
            if (!operation.Ok)
                throw new InvalidOperationException(operation.Message);
            Status = operation.Message;
            RaiseChanged();
            return default!;
        }
        finally
        {
            _ioGate.Release();
        }
    }

    private HttpClient RequireHttp() =>
        _http ?? throw new InvalidOperationException("Connect to a PC first.");

    private static async Task<T> DeserializeAsync<T>(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(
            cancellationToken).ConfigureAwait(false);
        return await JsonSerializer.DeserializeAsync<T>(
                stream,
                RemoteProtocol.JsonOptions,
                cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("The remote response was empty.");
    }

    private void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
