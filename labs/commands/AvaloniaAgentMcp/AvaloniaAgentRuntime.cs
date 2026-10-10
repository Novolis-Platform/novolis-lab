using System.Text.Json;
using Novolis.Avalonia.Agent.Protocol;
using Novolis.Avalonia.Agent.Protocol.Dto;
using Novolis.Transports.LocalIpc;

namespace AvaloniaAgentMcp;

internal static class AvaloniaAgentRuntime
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static UiAgentClient? _client;
    private static string? _endpointOverride;

    public static string? EndpointOverride => _endpointOverride;

    public static async Task<T> WithClientAsync<T>(Func<UiAgentClient, Task<T>> action, CancellationToken cancellationToken)
    {
        await Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Exception? last = null;
            for (var attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    if (_client is not null && !_client.IsConnected)
                        await ResetClientAsync().ConfigureAwait(false);

                    _client ??= new UiAgentClient();
                    if (!_client.IsConnected)
                        await ConnectClientAsync(_client, cancellationToken).ConfigureAwait(false);

                    return await action(_client).ConfigureAwait(false);
                }
                catch (Exception ex) when (attempt < 2)
                {
                    last = ex;
                    await ResetClientAsync().ConfigureAwait(false);
                    await Task.Delay(200 * (attempt + 1), cancellationToken).ConfigureAwait(false);
                }
            }

            throw last ?? new InvalidOperationException("Avalonia agent call failed.");
        }
        finally
        {
            Gate.Release();
        }
    }

    public static Task ForceReconnectAsync() => ResetClientAsync();

    public static async Task SetEndpointAsync(string? endpoint)
    {
        _endpointOverride = string.IsNullOrWhiteSpace(endpoint) ? null : endpoint.Trim();
        await ResetClientAsync().ConfigureAwait(false);
    }

    public static async Task ResetClientAsync()
    {
        if (_client is not null)
        {
            await _client.DisposeAsync().ConfigureAwait(false);
            _client = null;
        }
    }

    public static IReadOnlyList<object> DiscoverHosts()
    {
        var results = new List<object>();
        var marker = Path.Combine(Path.GetTempPath(), "novolis-avalonia-agent.host");
        var knownSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (File.Exists(marker))
        {
            try
            {
                var lines = File.ReadAllLines(marker);
                var endpoint = lines.ElementAtOrDefault(2)?.Trim();
                if (!string.IsNullOrWhiteSpace(endpoint))
                    knownSeen.Add(endpoint);
                results.Add(new
                {
                    path = marker,
                    processId = lines.ElementAtOrDefault(0),
                    transport = lines.ElementAtOrDefault(1),
                    endpoint,
                    activeOverride = _endpointOverride,
                    envDefault = Environment.GetEnvironmentVariable(UiTransportEndpoints.EndpointEnvVar),
                });
            }
            catch (Exception ex)
            {
                results.Add(new { path = marker, error = ex.Message });
            }
        }

        foreach (var known in new[] { "novolis-avalonia-agent", "novolis-avalonia-agent-sins", "novolis-avalonia-agent-draft" })
        {
            if (knownSeen.Contains(known))
                continue;
            results.Add(new { endpoint = known, note = "known-pipe (try ui_connect)" });
        }

        return results;
    }

    static async Task ConnectClientAsync(UiAgentClient client, CancellationToken cancellationToken)
    {
        var endpoint = ResolveEndpoint();
        await client.ConnectAsync(endpoint, cancellationToken).ConfigureAwait(false);
    }

    static LocalIpcEndpoint ResolveEndpoint()
    {
        var address = _endpointOverride
                      ?? Environment.GetEnvironmentVariable(UiTransportEndpoints.EndpointEnvVar);
        if (string.IsNullOrWhiteSpace(address))
        {
            // Prefer live host marker when present (last writer wins).
            var marker = Path.Combine(Path.GetTempPath(), "novolis-avalonia-agent.host");
            if (File.Exists(marker))
            {
                try
                {
                    var lines = File.ReadAllLines(marker);
                    if (lines.Length >= 3 && !string.IsNullOrWhiteSpace(lines[2]))
                        address = lines[2].Trim();
                }
                catch
                {
                    // fall through
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(address))
        {
            return OperatingSystem.IsWindows()
                ? new LocalIpcEndpoint(address, LocalIpcTransportKind.NamedPipe)
                : new LocalIpcEndpoint(address, LocalIpcTransportKind.UnixDomainSocket);
        }

        return UiTransportEndpoints.CreateDefault();
    }

    public static string ToJson<T>(T value) =>
        JsonSerializer.Serialize(value, JsonOptions);

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static string ScreenshotDir
    {
        get
        {
            var dir = Path.Combine(Path.GetTempPath(), "novolis-avalonia-agent");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    public static string WriteScreenshot(UiScreenshotResponseDto response)
    {
        if (!response.Success || response.Png is null || response.Png.Length == 0)
            throw new InvalidOperationException(response.Error ?? "Screenshot failed.");

        var path = Path.Combine(ScreenshotDir, $"shot-{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}.png");
        File.WriteAllBytes(path, response.Png);
        return path;
    }
}
