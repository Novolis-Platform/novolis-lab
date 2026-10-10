using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using CursorRemote.Protocol;
using CursorRemote.Services;
using CursorRemote.Desktop.Windows;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;

namespace CursorRemote.Desktop.Host;

public sealed class CursorRemoteHost : IHostedService, IAsyncDisposable
{
    // Flip6 is ~1080 CSS px wide; keep enough pixels for pinch-zoom without mush.
    private const int MaximumScreenshotWidth = 3200;
    private readonly HostActivityLog _log;
    private WebApplication? _app;
    private UdpClient? _discovery;
    private CancellationTokenSource? _discoveryCts;
    private Task? _discoveryLoop;
    private string[] _endpoints = [];
    private long _screenHits;
    private long _statusHits;
    private long _discoveryHits;
    private DateTime _lastScreenLogUtc = DateTime.MinValue;
    private DateTime _lastStatusLogUtc = DateTime.MinValue;
    private DateTime _lastDiscoveryLogUtc = DateTime.MinValue;

    public CursorRemoteHost(HostActivityLog log)
    {
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    public bool IsRunning => _app is not null;

    public string Status { get; private set; } = "Starting host…";

    public RemoteConnectionInfo? Connection { get; private set; }

    public event EventHandler? Changed;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var addresses = TailscaleAddresses().ToArray();
        if (addresses.Length == 0)
            addresses = [IPAddress.Loopback];

        try
        {
            _log.Info(
                $"Starting on {string.Join(", ", addresses.Select(a => $"{a}:{RemoteProtocol.DefaultPort}"))}");
            var builder = WebApplication.CreateSlimBuilder();
            builder.WebHost.ConfigureKestrel(options =>
            {
                foreach (var address in addresses)
                    options.Listen(address, RemoteProtocol.DefaultPort);
            });

            var app = builder.Build();
            ConfigureRoutes(app);
            await app.StartAsync(cancellationToken).ConfigureAwait(false);
            _app = app;
            _endpoints = addresses
                .Select(address => $"http://{address}:{RemoteProtocol.DefaultPort}")
                .ToArray();
            var preferred = addresses.FirstOrDefault(IsTailscaleAddress);
            var endpoint = $"http://{preferred ?? addresses[0]}:{RemoteProtocol.DefaultPort}";
            Connection = new RemoteConnectionInfo(
                endpoint,
                preferred?.ToString(),
                RemoteProtocol.Version,
                Environment.MachineName);
            StartDiscoveryResponder();
            Status = preferred is null
                ? "Host is running on loopback; Tailscale was not detected."
                : "Host is discoverable on LAN/Tailscale. Open Cursor Remote on the phone.";
            _log.Info(Status);
            foreach (var listening in _endpoints)
                _log.Info($"Listening {listening}");
            RaiseChanged();
        }
        catch (Exception exception)
        {
            Status = $"Host failed: {exception.Message}";
            _log.Error(Status);
            Connection = null;
            RaiseChanged();
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await StopDiscoveryResponderAsync().ConfigureAwait(false);

        if (_app is null)
            return;

        await _app.StopAsync(cancellationToken).ConfigureAwait(false);
        await _app.DisposeAsync().ConfigureAwait(false);
        _app = null;
        Status = "Host stopped.";
        _log.Info(Status);
        RaiseChanged();
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync(CancellationToken.None).ConfigureAwait(false);
    }

    private void ConfigureRoutes(WebApplication app)
    {
        app.Use(async (context, next) =>
        {
            var started = Stopwatch.GetTimestamp();
            await next().ConfigureAwait(false);
            var elapsed = Stopwatch.GetElapsedTime(started);
            LogHttp(context, elapsed);
        });

        app.MapGet("/health", () => Results.Ok(new { ok = true, appId = RemoteProtocol.AppId }));
        app.MapGet("/api/v1/hello", () => Results.Ok(BuildHello()));
        app.MapGet("/api/v1/status", () =>
        {
            var region = WindowsScreenCapture.ResolveRegion();
            WindowsScreenCapture.CurrentRegion = region;
            return Results.Ok(new RemoteStatusDto(
                RemoteProtocol.Version,
                Environment.MachineName,
                _endpoints,
                region.Width,
                region.Height,
                region.OriginX,
                region.OriginY,
                IsCursorRunning()));
        });
        app.MapGet("/api/v1/screen", () =>
        {
            var frame = WindowsScreenCapture.Capture(MaximumScreenshotWidth);
            return Results.File(frame.Png, "image/jpeg");
        });
        app.MapPost("/api/v1/focus", () =>
        {
            var ok = WindowsInput.FocusCursor();
            return Results.Ok(new RemoteOperationResponse(
                ok,
                ok ? "Cursor focused." : "Cursor is not running or has no window."));
        });
        app.MapPost("/api/v1/input/click", (RemoteClickRequest request) =>
        {
            var region = WindowsScreenCapture.CurrentRegion;
            if (double.IsNaN(request.X)
                || double.IsNaN(request.Y)
                || request.X < 0
                || request.Y < 0
                || request.X >= region.Width
                || request.Y >= region.Height)
            {
                return Results.BadRequest(new RemoteOperationResponse(
                    false,
                    "Click coordinates are outside the captured screen."));
            }

            var ok = WindowsInput.Click(
                request.X,
                request.Y,
                request.Button,
                request.ClickCount);
            return Results.Ok(new RemoteOperationResponse(
                ok,
                ok ? "Click sent." : "Click failed."));
        });
        app.MapPost("/api/v1/input/text", (RemoteTextRequest request) =>
        {
            if (request.Text.Length > 20_000)
                return Results.BadRequest(new RemoteOperationResponse(
                    false,
                    "Text payload is too large."));

            var ok = WindowsInput.TypeText(request.Text);
            return Results.Ok(new RemoteOperationResponse(
                ok,
                ok ? "Text sent." : "Text input failed."));
        });
        app.MapPost("/api/v1/input/key", (RemoteKeyRequest request) =>
        {
            var ok = WindowsInput.SendKey(request.Key);
            return Results.Ok(new RemoteOperationResponse(
                ok,
                ok ? $"Key {request.Key} sent." : $"Unsupported key: {request.Key}."));
        });
    }

    private void LogHttp(HttpContext context, TimeSpan elapsed)
    {
        var path = context.Request.Path.Value ?? "/";
        var remote = context.Connection.RemoteIpAddress?.ToString() ?? "?";
        var status = context.Response.StatusCode;
        var ms = elapsed.TotalMilliseconds;

        if (path.StartsWith("/api/v1/screen", StringComparison.OrdinalIgnoreCase))
        {
            Interlocked.Increment(ref _screenHits);
            if (DateTime.UtcNow - _lastScreenLogUtc < TimeSpan.FromSeconds(5))
                return;
            _lastScreenLogUtc = DateTime.UtcNow;
            var hits = Interlocked.Exchange(ref _screenHits, 0);
            _log.Info($"GET screen ×{hits} from {remote} → {status} ({ms:0}ms last)");
            return;
        }

        if (path.StartsWith("/api/v1/status", StringComparison.OrdinalIgnoreCase))
        {
            Interlocked.Increment(ref _statusHits);
            if (DateTime.UtcNow - _lastStatusLogUtc < TimeSpan.FromSeconds(5))
                return;
            _lastStatusLogUtc = DateTime.UtcNow;
            var hits = Interlocked.Exchange(ref _statusHits, 0);
            _log.Info($"GET status ×{hits} from {remote} → {status}");
            return;
        }

        if (string.Equals(path, "/health", StringComparison.OrdinalIgnoreCase))
            return;

        var detail = path switch
        {
            var p when p.StartsWith("/api/v1/input/click", StringComparison.OrdinalIgnoreCase)
                => "click",
            var p when p.StartsWith("/api/v1/input/text", StringComparison.OrdinalIgnoreCase)
                => "text",
            var p when p.StartsWith("/api/v1/input/key", StringComparison.OrdinalIgnoreCase)
                => "key",
            var p when p.StartsWith("/api/v1/focus", StringComparison.OrdinalIgnoreCase)
                => "focus",
            var p when p.StartsWith("/api/v1/hello", StringComparison.OrdinalIgnoreCase)
                => "hello",
            _ => path,
        };

        _log.Info($"{context.Request.Method} {detail} from {remote} → {status} ({ms:0}ms)");
    }

    private RemoteHelloDto BuildHello() =>
        new(
            RemoteProtocol.AppId,
            RemoteProtocol.Version,
            Environment.MachineName,
            _endpoints,
            RemoteProtocol.DefaultPort,
            RemoteProtocol.DiscoveryPort);

    private void StartDiscoveryResponder()
    {
        _discoveryCts = new CancellationTokenSource();
        try
        {
            _discovery = new UdpClient(new IPEndPoint(IPAddress.Any, RemoteProtocol.DiscoveryPort));
            _discovery.EnableBroadcast = true;
            _discoveryLoop = Task.Run(() => DiscoveryLoopAsync(_discoveryCts.Token));
            _log.Info($"Discovery beacon on UDP :{RemoteProtocol.DiscoveryPort}");
        }
        catch (Exception exception)
        {
            Status = $"{Status} Discovery beacon unavailable: {exception.Message}";
            _log.Warn($"Discovery beacon unavailable: {exception.Message}");
            RaiseChanged();
        }
    }

    private async Task DiscoveryLoopAsync(CancellationToken cancellationToken)
    {
        if (_discovery is null)
            return;

        var who = Encoding.UTF8.GetBytes(RemoteProtocol.DiscoveryWho);
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var result = await _discovery.ReceiveAsync(cancellationToken).ConfigureAwait(false);
                if (!result.Buffer.AsSpan().SequenceEqual(who))
                    continue;

                var payload = Encoding.UTF8.GetBytes(
                    JsonSerializer.Serialize(BuildHello(), RemoteProtocol.JsonOptions));
                await _discovery.SendAsync(
                        payload,
                        payload.Length,
                        result.RemoteEndPoint)
                    .WaitAsync(cancellationToken)
                    .ConfigureAwait(false);

                Interlocked.Increment(ref _discoveryHits);
                if (DateTime.UtcNow - _lastDiscoveryLogUtc >= TimeSpan.FromSeconds(3))
                {
                    _lastDiscoveryLogUtc = DateTime.UtcNow;
                    var hits = Interlocked.Exchange(ref _discoveryHits, 0);
                    _log.Info($"UDP WHO ×{hits} from {result.RemoteEndPoint.Address}");
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception exception)
            {
                _log.Warn($"Discovery loop: {exception.Message}");
            }
        }
    }

    private async Task StopDiscoveryResponderAsync()
    {
        if (_discoveryCts is not null)
        {
            await _discoveryCts.CancelAsync().ConfigureAwait(false);
            _discoveryCts.Dispose();
            _discoveryCts = null;
        }

        if (_discoveryLoop is not null)
        {
            try
            {
                await _discoveryLoop.ConfigureAwait(false);
            }
            catch
            {
                // Ignored on shutdown.
            }

            _discoveryLoop = null;
        }

        _discovery?.Dispose();
        _discovery = null;
    }

    private static bool IsCursorRunning()
    {
        var processes = Process.GetProcessesByName("Cursor");
        try
        {
            return processes.Length > 0;
        }
        finally
        {
            foreach (var process in processes)
                process.Dispose();
        }
    }

    private static IEnumerable<IPAddress> TailscaleAddresses() =>
        NetworkInterface.GetAllNetworkInterfaces()
            .Where(networkInterface =>
                networkInterface.OperationalStatus == OperationalStatus.Up)
            .SelectMany(networkInterface => networkInterface.GetIPProperties().UnicastAddresses)
            .Select(address => address.Address)
            .Where(IsTailscaleAddress)
            .Distinct();

    private static bool IsTailscaleAddress(IPAddress address)
    {
        if (address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
            return false;
        var bytes = address.GetAddressBytes();
        return bytes[0] == 100
               && bytes[1] >= 64
               && bytes[1] <= 127;
    }

    private void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
