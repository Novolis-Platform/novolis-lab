using System.Net.Http.Json;
using System.Text.Json;

namespace AvaloniaAgentMcp;

/// <summary>HTTP client runtime for lightweight scene modeling session (:18785).</summary>
internal static class SceneAgentRuntime
{
    public const int DefaultHttpPort = 18785;
    public const string MarkerFileName = "novolis-scene-session.http";
    public const string HttpUrlEnv = "NOVOLIS_SCENE_SESSION_HTTP_URL";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static HttpClient? _http;
    private static string? _httpUrlOverride;

    public static string? HttpUrlOverride => _httpUrlOverride;

    public static async Task SetHttpUrlAsync(string? url)
    {
        _httpUrlOverride = string.IsNullOrWhiteSpace(url) ? null : url.Trim().TrimEnd('/');
        await ResetAsync().ConfigureAwait(false);
    }

    public static object DiscoverHosts()
    {
        string? markerUrl = null;
        try
        {
            var path = Path.Combine(Path.GetTempPath(), MarkerFileName);
            if (File.Exists(path))
            {
                var lines = File.ReadAllLines(path);
                if (lines.Length >= 2)
                    markerUrl = lines[1].Trim();
            }
        }
        catch
        {
        }

        return new
        {
            httpMarker = markerUrl,
            defaultHttp = $"http://127.0.0.1:{DefaultHttpPort}",
            envOverride = Environment.GetEnvironmentVariable(HttpUrlEnv),
            activeOverride = _httpUrlOverride,
        };
    }

    public static Task<object> HelloAsync(CancellationToken cancellationToken) =>
        GetAsync("session/hello", cancellationToken);

    public static Task<object> SnapshotAsync(CancellationToken cancellationToken) =>
        GetAsync("session/snapshot", cancellationToken);

    public static Task<object> ActionsAsync(CancellationToken cancellationToken) =>
        GetAsync("session/actions", cancellationToken);

    public static Task<object> DefinitionAsync(CancellationToken cancellationToken) =>
        GetAsync("session/definition", cancellationToken);

    public static async Task<object> CommandAsync(object command, CancellationToken cancellationToken)
    {
        await Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var http = await EnsureHttpAsync(cancellationToken).ConfigureAwait(false);
            using var response = await http.PostAsJsonAsync("session/command", command, JsonOptions, cancellationToken)
                .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            return await UnwrapAsync(response, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            Gate.Release();
        }
    }

    private static async Task<object> GetAsync(string path, CancellationToken cancellationToken)
    {
        await Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var http = await EnsureHttpAsync(cancellationToken).ConfigureAwait(false);
            using var response = await http.GetAsync(path, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            return await UnwrapAsync(response, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            Gate.Release();
        }
    }

    private static async Task<HttpClient> EnsureHttpAsync(CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        if (_http is not null)
            return _http;

        var env = Environment.GetEnvironmentVariable(HttpUrlEnv);
        var url = _httpUrlOverride
                  ?? (!string.IsNullOrWhiteSpace(env) ? env.Trim() : null)
                  ?? TryReadMarker()
                  ?? $"http://127.0.0.1:{DefaultHttpPort}";
        _http = new HttpClient { BaseAddress = new Uri(url.TrimEnd('/') + "/") };
        return _http;
    }

    private static string? TryReadMarker()
    {
        try
        {
            var path = Path.Combine(Path.GetTempPath(), MarkerFileName);
            if (!File.Exists(path))
                return null;
            var lines = File.ReadAllLines(path);
            return lines.Length >= 2 ? lines[1].Trim() : null;
        }
        catch
        {
            return null;
        }
    }

    private static async Task<object> UnwrapAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (doc.RootElement.TryGetProperty("ok", out var ok) && ok.ValueKind == JsonValueKind.False)
        {
            var err = doc.RootElement.TryGetProperty("error", out var e) ? e.GetString() : "request failed";
            throw new InvalidOperationException(err);
        }

        if (!doc.RootElement.TryGetProperty("result", out var result))
            throw new InvalidOperationException("Response missing result.");

        return JsonSerializer.Deserialize<object>(result.GetRawText(), JsonOptions)
               ?? result.Clone();
    }

    private static async Task ResetAsync()
    {
        await Gate.WaitAsync().ConfigureAwait(false);
        try
        {
            _http?.Dispose();
            _http = null;
        }
        finally
        {
            Gate.Release();
        }
    }
}
