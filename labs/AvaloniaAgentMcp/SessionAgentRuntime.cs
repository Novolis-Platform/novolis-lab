using Novolis.Agent.Core;
using Novolis.Agent.Surface;
using Novolis.Transports.LocalIpc;

namespace AvaloniaAgentMcp;

internal static class SessionAgentRuntime
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static AgentLocalIpcClient? _ipcClient;
    private static AgentHttpClient? _httpClient;
    private static string? _endpointOverride;
    private static string? _httpUrlOverride;
    private static readonly List<object> RecentEvents = new();

    private static AgentSurfaceDefinition Definition => SinsAgentSurfaceContract.Definition;

    public static string? EndpointOverride => _endpointOverride;

    public static string? HttpUrlOverride => _httpUrlOverride;

    public static async Task SetEndpointAsync(string? endpoint)
    {
        _endpointOverride = string.IsNullOrWhiteSpace(endpoint) ? null : endpoint.Trim();
        _httpUrlOverride = null;
        await ResetClientAsync().ConfigureAwait(false);
    }

    public static async Task SetHttpUrlAsync(string? url)
    {
        _httpUrlOverride = string.IsNullOrWhiteSpace(url) ? null : url.Trim().TrimEnd('/');
        _endpointOverride = null;
        await ResetClientAsync().ConfigureAwait(false);
    }

    public static Task ForceReconnectAsync() => ResetClientAsync();

    public static async Task<object> HelloAsync(CancellationToken cancellationToken)
    {
        await Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (await EnsureHttpAsync(cancellationToken).ConfigureAwait(false) is { } http)
                return await http.HelloAsync(cancellationToken).ConfigureAwait(false);
            var ipc = await EnsureIpcAsync(cancellationToken).ConfigureAwait(false);
            return await ipc.HelloAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            Gate.Release();
        }
    }

    public static async Task<object> SnapshotAsync(CancellationToken cancellationToken)
    {
        await Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (await EnsureHttpAsync(cancellationToken).ConfigureAwait(false) is { } http)
                return await http.SnapshotAsync(cancellationToken).ConfigureAwait(false);
            var ipc = await EnsureIpcAsync(cancellationToken).ConfigureAwait(false);
            return await ipc.SnapshotAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            Gate.Release();
        }
    }

    public static async Task<object> ActionsAsync(CancellationToken cancellationToken)
    {
        await Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (await EnsureHttpAsync(cancellationToken).ConfigureAwait(false) is { } http)
                return await http.ActionsAsync(cancellationToken).ConfigureAwait(false);
            var ipc = await EnsureIpcAsync(cancellationToken).ConfigureAwait(false);
            return await ipc.ActionsAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            Gate.Release();
        }
    }

    public static async Task<object> ContinueAsync(CancellationToken cancellationToken)
    {
        await Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (await EnsureHttpAsync(cancellationToken).ConfigureAwait(false) is { } http)
                return await http.ContinueAsync(cancellationToken).ConfigureAwait(false);
            var ipc = await EnsureIpcAsync(cancellationToken).ConfigureAwait(false);
            return await ipc.ContinueAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            Gate.Release();
        }
    }

    public static async Task<object> SubscribeAsync(CancellationToken cancellationToken)
    {
        await Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (await EnsureHttpAsync(cancellationToken).ConfigureAwait(false) is { } http)
                return await http.SubscribeAsync(cancellationToken).ConfigureAwait(false);
            var ipc = await EnsureIpcAsync(cancellationToken).ConfigureAwait(false);
            return await ipc.SubscribeAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            Gate.Release();
        }
    }

    public static async Task<object> CommandAsync(AgentCommand command, CancellationToken cancellationToken)
    {
        await Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (await EnsureHttpAsync(cancellationToken).ConfigureAwait(false) is { } http)
                return await http.CommandAsync(command, cancellationToken).ConfigureAwait(false);
            var ipc = await EnsureIpcAsync(cancellationToken).ConfigureAwait(false);
            return await ipc.CommandAsync(command, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            Gate.Release();
        }
    }

    /// <summary>Legacy LocalIpc-only helper used by older call sites.</summary>
    public static async Task<T> WithClientAsync<T>(Func<AgentLocalIpcClient, Task<T>> action, CancellationToken cancellationToken)
    {
        await Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var ipc = await EnsureIpcAsync(cancellationToken).ConfigureAwait(false);
            return await action(ipc).ConfigureAwait(false);
        }
        finally
        {
            Gate.Release();
        }
    }

    public static IReadOnlyList<object> DrainEvents()
    {
        lock (RecentEvents)
        {
            var copy = RecentEvents.ToList();
            RecentEvents.Clear();
            return copy;
        }
    }

    public static IReadOnlyList<object> DiscoverHosts()
    {
        var results = new List<object>();
        var httpUrl = Definition.TryReadHttpBaseUrl();
        if (!string.IsNullOrWhiteSpace(httpUrl))
            results.Add(new { source = "http-marker", transport = "http", endpoint = httpUrl });

        var marker = Definition.IpcMarkerPath;
        if (File.Exists(marker))
        {
            try
            {
                var lines = File.ReadAllLines(marker);
                results.Add(new
                {
                    source = "ipc-marker",
                    transport = "local-ipc",
                    pid = lines.ElementAtOrDefault(0),
                    kind = lines.ElementAtOrDefault(1),
                    endpoint = lines.ElementAtOrDefault(2),
                });
            }
            catch
            {
                // ignore
            }
        }

        var tcpMarker = Definition.TcpMarkerPath;
        if (File.Exists(tcpMarker))
        {
            try
            {
                var lines = File.ReadAllLines(tcpMarker);
                results.Add(new
                {
                    source = "tcp-marker",
                    transport = "tcp-jsonl",
                    pid = lines.ElementAtOrDefault(0),
                    port = lines.ElementAtOrDefault(1),
                });
            }
            catch
            {
                // ignore
            }
        }

        results.Add(new { source = "known", transport = "http", endpoint = $"http://127.0.0.1:{Definition.DefaultHttpPort}" });
        results.Add(new { source = "known", transport = "local-ipc", endpoint = "novolis-game-session-sins" });
        return results;
    }

    private static async Task<AgentHttpClient?> EnsureHttpAsync(CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        if (_httpClient is not null)
            return _httpClient;

        var url = _httpUrlOverride;
        if (string.IsNullOrWhiteSpace(url))
            url = Environment.GetEnvironmentVariable("NOVOLIS_GAME_SESSION_HTTP_URL");
        if (string.IsNullOrWhiteSpace(url))
            url = Definition.TryReadHttpBaseUrl();

        // Prefer HTTP when available (wide agent surface). Skip if caller forced LocalIpc pipe.
        if (string.IsNullOrWhiteSpace(url) || !string.IsNullOrWhiteSpace(_endpointOverride))
            return null;

        _httpClient = new AgentHttpClient(url);
        return _httpClient;
    }

    private static async Task<AgentLocalIpcClient> EnsureIpcAsync(CancellationToken cancellationToken)
    {
        if (_ipcClient is not null)
            return _ipcClient;

        Exception? last = null;
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                _ipcClient = await ConnectIpcAsync(cancellationToken).ConfigureAwait(false);
                return _ipcClient;
            }
            catch (Exception ex) when (attempt < 2)
            {
                last = ex;
                await ResetClientAsync().ConfigureAwait(false);
                await Task.Delay(200 * (attempt + 1), cancellationToken).ConfigureAwait(false);
            }
        }

        throw last ?? new InvalidOperationException("Game session LocalIpc connect failed.");
    }

    private static async Task<AgentLocalIpcClient> ConnectIpcAsync(CancellationToken cancellationToken)
    {
        var address = _endpointOverride;
        if (string.IsNullOrWhiteSpace(address))
        {
            var marker = Definition.IpcMarkerPath;
            if (File.Exists(marker))
            {
                var lines = await File.ReadAllLinesAsync(marker, cancellationToken).ConfigureAwait(false);
                address = lines.ElementAtOrDefault(2)?.Trim();
            }
        }

        address = string.IsNullOrWhiteSpace(address) ? "novolis-game-session-sins" : address;
        var endpoint = OperatingSystem.IsWindows()
            ? new LocalIpcEndpoint(address, LocalIpcTransportKind.NamedPipe)
            : new LocalIpcEndpoint(
                Path.IsPathRooted(address) ? address : Path.Combine(Path.GetTempPath(), address + ".sock"),
                LocalIpcTransportKind.UnixDomainSocket);

        var client = await AgentLocalIpcClient.ConnectAsync(endpoint, cancellationToken).ConfigureAwait(false);
        client.EventReceived += (name, payload) =>
        {
            object decoded = name switch
            {
                AgentMethodNames.Decision => AgentProtocolCodec.Deserialize<AgentDecisionEvent>(payload),
                AgentMethodNames.Changed => AgentProtocolCodec.Deserialize<AgentChangedEvent>(payload),
                AgentMethodNames.ActionResult => AgentProtocolCodec.Deserialize<AgentActionResultEvent>(payload),
                _ => new { name, bytes = payload.Length },
            };
            lock (RecentEvents)
            {
                RecentEvents.Add(new { name, decoded, at = DateTime.UtcNow });
                while (RecentEvents.Count > 32)
                    RecentEvents.RemoveAt(0);
            }
        };
        return client;
    }

    private static async Task ResetClientAsync()
    {
        if (_ipcClient is not null)
        {
            await _ipcClient.DisposeAsync().ConfigureAwait(false);
            _ipcClient = null;
        }

        if (_httpClient is not null)
        {
            await _httpClient.DisposeAsync().ConfigureAwait(false);
            _httpClient = null;
        }
    }
}
