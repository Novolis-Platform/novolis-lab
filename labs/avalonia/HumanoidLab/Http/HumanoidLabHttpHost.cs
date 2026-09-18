using System.Net;
using System.Text;
using System.Text.Json;
using System.Numerics;
using Avalonia.Threading;
using HumanoidLab.Demo;

namespace HumanoidLab.Http;

/// <summary>
/// Localhost HTTP control surface so agents can inspect / reset / tip the ragdoll without UI clicks.
/// Marker file: %TEMP%/novolis-humanoid-lab-http.txt
/// </summary>
internal sealed class HumanoidLabHttpHost : IAsyncDisposable
{
    public const string MarkerFileName = "novolis-humanoid-lab-http.txt";
    public const int DefaultPort = 18765;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private readonly MainWindow _window;
    private readonly HttpListener _listener;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _loop;

    private HumanoidLabHttpHost(MainWindow window, string prefix)
    {
        _window = window;
        BaseUrl = prefix.TrimEnd('/');
        var listen = prefix.EndsWith('/') ? prefix : prefix + "/";
        _listener = new HttpListener();
        _listener.Prefixes.Add(listen);
        _listener.Start();
        _loop = Task.Run(() => ListenAsync(_cts.Token));
        try
        {
            File.WriteAllText(MarkerPath, $"{Environment.ProcessId}{Environment.NewLine}{BaseUrl}{Environment.NewLine}");
        }
        catch
        {
            // ignore
        }
    }

    public string BaseUrl { get; }

    public static string MarkerPath => Path.Combine(Path.GetTempPath(), MarkerFileName);

    public static HumanoidLabHttpHost Attach(MainWindow window, int? port = null)
    {
        var p = port
                ?? (int.TryParse(Environment.GetEnvironmentVariable("HUMANIOD_LAB_HTTP_PORT"), out var envPort) ? envPort : DefaultPort);
        return new HumanoidLabHttpHost(window, $"http://127.0.0.1:{p}/");
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        try
        {
            _listener.Stop();
            _listener.Close();
        }
        catch
        {
            // ignore
        }

        try
        {
            await _loop.ConfigureAwait(false);
        }
        catch
        {
            // ignore
        }

        _cts.Dispose();
        try
        {
            if (File.Exists(MarkerPath))
                File.Delete(MarkerPath);
        }
        catch
        {
            // ignore
        }
    }

    private async Task ListenAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var context = await _listener.GetContextAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
                _ = Task.Run(() => HandleAsync(context), cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (HttpListenerException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private async Task HandleAsync(HttpListenerContext context)
    {
        try
        {
            var req = context.Request;
            var path = (req.Url?.AbsolutePath ?? "/").TrimEnd('/');
            if (path.Length == 0)
                path = "/";

            if (req.HttpMethod == "OPTIONS")
            {
                WriteCors(context.Response);
                context.Response.StatusCode = 204;
                context.Response.Close();
                return;
            }

            if (path is "/" or "/health")
            {
                await WriteJsonAsync(context.Response, 200, new
                {
                    ok = true,
                    service = "HumanoidLab",
                    baseUrl = BaseUrl,
                    endpoints = new[]
                    {
                        "GET /health",
                        "GET /ragdoll",
                        "POST /ragdoll/reset",
                        "POST /ragdoll/tip",
                        "POST /ragdoll/entropy",
                    },
                }).ConfigureAwait(false);
                return;
            }

            if (path == "/ragdoll" && req.HttpMethod == "GET")
            {
                var status = await OnUiAsync(() => _window.Ragdoll.Snapshot()).ConfigureAwait(false);
                await WriteJsonAsync(context.Response, 200, ToDto(status)).ConfigureAwait(false);
                return;
            }

            if (path == "/ragdoll/reset" && req.HttpMethod == "POST")
            {
                await OnUiAsync(() => _window.Ragdoll.Reset()).ConfigureAwait(false);
                var status = await OnUiAsync(() => _window.Ragdoll.Snapshot()).ConfigureAwait(false);
                await WriteJsonAsync(context.Response, 200, ToDto(status)).ConfigureAwait(false);
                return;
            }

            if (path == "/ragdoll/tip" && req.HttpMethod == "POST")
            {
                Vector3? impulse = null;
                if (req.HasEntityBody)
                {
                    using var doc = await JsonDocument.ParseAsync(req.InputStream).ConfigureAwait(false);
                    if (doc.RootElement.TryGetProperty("impulse", out var arr) && arr.GetArrayLength() >= 3)
                    {
                        impulse = new Vector3(
                            arr[0].GetSingle(),
                            arr[1].GetSingle(),
                            arr[2].GetSingle());
                    }
                }

                await OnUiAsync(() => _window.Ragdoll.Tip(impulse)).ConfigureAwait(false);
                var status = await OnUiAsync(() => _window.Ragdoll.Snapshot()).ConfigureAwait(false);
                await WriteJsonAsync(context.Response, 200, ToDto(status)).ConfigureAwait(false);
                return;
            }

            if (path == "/ragdoll/entropy" && req.HttpMethod == "POST")
            {
                using var doc = await JsonDocument.ParseAsync(req.InputStream).ConfigureAwait(false);
                if (doc.RootElement.TryGetProperty("rate", out var rateProp))
                {
                    var rate = rateProp.GetSingle();
                    await OnUiAsync(() => _window.Ragdoll.EntropyPerSecond = rate).ConfigureAwait(false);
                }

                if (doc.RootElement.TryGetProperty("autoTip", out var autoTip))
                {
                    var enabled = autoTip.GetBoolean();
                    await OnUiAsync(() => _window.Ragdoll.AutoTipEnabled = enabled).ConfigureAwait(false);
                }

                var status = await OnUiAsync(() => _window.Ragdoll.Snapshot()).ConfigureAwait(false);
                await WriteJsonAsync(context.Response, 200, ToDto(status)).ConfigureAwait(false);
                return;
            }

            await WriteJsonAsync(context.Response, 404, new { ok = false, error = "not found", path }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            try
            {
                await WriteJsonAsync(context.Response, 500, new { ok = false, error = ex.Message }).ConfigureAwait(false);
            }
            catch
            {
                // ignore
            }
        }
    }

    private static object ToDto(RagdollStatus s) => new
    {
        ok = true,
        timeSeconds = s.TimeSeconds,
        tipped = s.Tipped,
        maxSpeed = s.MaxSpeed,
        kineticEnergy = s.KineticEnergy,
        boneError = s.BoneError,
        sleeping = s.Sleeping,
        sphereCount = s.SphereCount,
        minY = s.MinY,
        maxY = s.MaxY,
        hip = new { x = s.Hip.X, y = s.Hip.Y, z = s.Hip.Z },
        entropyPerSecond = s.EntropyPerSecond,
        autoTipEnabled = s.AutoTipEnabled,
        atRest = s.Sleeping == s.SphereCount && s.MaxSpeed < 0.15f,
    };

    private static Task<T> OnUiAsync<T>(Func<T> action)
    {
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                tcs.SetResult(action());
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });
        return tcs.Task;
    }

    private static Task OnUiAsync(Action action) => OnUiAsync(() =>
    {
        action();
        return true;
    });

    private static async Task WriteJsonAsync(HttpListenerResponse response, int status, object body)
    {
        WriteCors(response);
        response.StatusCode = status;
        response.ContentType = "application/json; charset=utf-8";
        var json = JsonSerializer.Serialize(body, JsonOptions);
        var bytes = Encoding.UTF8.GetBytes(json);
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes).ConfigureAwait(false);
        response.Close();
    }

    private static void WriteCors(HttpListenerResponse response)
    {
        response.Headers["Access-Control-Allow-Origin"] = "*";
        response.Headers["Access-Control-Allow-Methods"] = "GET, POST, OPTIONS";
        response.Headers["Access-Control-Allow-Headers"] = "Content-Type";
    }
}
