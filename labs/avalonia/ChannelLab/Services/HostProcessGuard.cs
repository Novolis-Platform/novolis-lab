using System.Diagnostics;
using System.Net.Http;

namespace ChannelLab.Services;

/// <summary>Starts ChannelHost when health check fails; stops child process on app close.</summary>
internal sealed class HostProcessGuard : IAsyncDisposable
{
    readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(2) };
    Process? _process;

    public async Task<bool> EnsureRunningAsync(CancellationToken cancellationToken = default)
    {
        if (await IsHealthyAsync(cancellationToken).ConfigureAwait(false))
            return true;

        var project = ResolveHostProjectPath();
        if (project is null)
            return false;

        var start = new ProcessStartInfo
        {
            FileName = "dotnet",
            ArgumentList =
            {
                "run",
                "--project",
                project,
                "--no-launch-profile",
            },
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(project)!,
        };
        start.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";

        _process = Process.Start(start);
        if (_process is null)
            return false;

        for (var i = 0; i < 40; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(250, cancellationToken).ConfigureAwait(false);
            if (await IsHealthyAsync(cancellationToken).ConfigureAwait(false))
                return true;
            if (_process.HasExited)
                return false;
        }

        return false;
    }

    public async Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _http.GetAsync(HostEndpoints.HealthUri, cancellationToken).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    static string? ResolveHostProjectPath()
    {
        // ChannelLab/ → ChannelHost/ChannelHost.csproj (dev tree)
        var candidates = new[]
        {
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "ChannelHost", "ChannelHost.csproj")),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "ChannelHost", "ChannelHost.csproj")),
            @"d:\novolis\novolis-lab\labs\avalonia\ChannelLab\ChannelHost\ChannelHost.csproj",
        };

        foreach (var path in candidates)
        {
            if (File.Exists(path))
                return path;
        }

        return null;
    }

    public async ValueTask DisposeAsync()
    {
        _http.Dispose();
        if (_process is null || _process.HasExited)
            return;

        try
        {
            _process.Kill(entireProcessTree: true);
            await _process.WaitForExitAsync().ConfigureAwait(false);
        }
        catch
        {
            // best-effort shutdown
        }
        finally
        {
            _process.Dispose();
            _process = null;
        }
    }
}
