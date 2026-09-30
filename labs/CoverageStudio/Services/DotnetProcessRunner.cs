using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace CoverageStudio.Services;

internal readonly record struct DotnetProcessResult(int ExitCode, string Output, bool TimedOut);

/// <summary>Hidden <c>dotnet</c> child processes — no console window, redirected IO only.</summary>
internal static class DotnetProcessRunner
{
    private static readonly Regex TotalRe = new(@"total:\s*(\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex SucceededRe = new(@"succeeded:\s*(\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex FailedRe = new(@"failed:\s*(\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <param name="timeout">Per-process wall clock limit; use <see cref="Timeout.InfiniteTimeSpan"/> or ≤0 to disable.</param>
    /// <param name="onTick">Optional heartbeat (elapsed, limit-or-null) about every 250ms while waiting.</param>
    public static async Task<DotnetProcessResult> RunAsync(
        IReadOnlyList<string> args,
        string workingDirectory,
        TimeSpan timeout,
        CancellationToken cancellationToken,
        Action<TimeSpan, TimeSpan?>? onTick = null)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
        };
        foreach (var a in args)
            psi.ArgumentList.Add(a);

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start dotnet.");

        var stdoutTask = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
        var stderrTask = process.StandardError.ReadToEndAsync(CancellationToken.None);

        var limited = timeout > TimeSpan.Zero && timeout != Timeout.InfiniteTimeSpan;
        var started = DateTime.UtcNow;
        var deadline = limited ? started + timeout : DateTime.MaxValue;

        try
        {
            while (!process.HasExited)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var elapsed = DateTime.UtcNow - started;
                if (limited && DateTime.UtcNow >= deadline)
                {
                    await ForceKillAsync(process).ConfigureAwait(false);
                    var partial = await ReadOutputAsync(stdoutTask, stderrTask).ConfigureAwait(false);
                    return new DotnetProcessResult(ExitCode: -1, Output: partial, TimedOut: true);
                }

                onTick?.Invoke(elapsed, limited ? timeout : null);
                try
                {
                    await Task.Delay(250, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    await ForceKillAsync(process).ConfigureAwait(false);
                    throw;
                }
            }
        }
        catch (OperationCanceledException)
        {
            await ForceKillAsync(process).ConfigureAwait(false);
            throw;
        }

        // Ensure exit code is populated.
        process.WaitForExit(0);
        var output = await ReadOutputAsync(stdoutTask, stderrTask).ConfigureAwait(false);
        return new DotnetProcessResult(process.ExitCode, output, TimedOut: false);
    }

    public static (int Total, int Passed, int Failed) ParseTestCounts(string output)
    {
        var total = MatchInt(TotalRe, output);
        var passed = MatchInt(SucceededRe, output);
        var failed = MatchInt(FailedRe, output);
        return (total, passed, failed);
    }

    private static async Task ForceKillAsync(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch
        {
            // fall through to taskkill
        }

        try
        {
            if (!process.HasExited)
                process.WaitForExit(500);
        }
        catch
        {
            // ignore
        }

        if (OperatingSystem.IsWindows())
        {
            try
            {
                if (!process.HasExited)
                {
                    using var kill = Process.Start(new ProcessStartInfo
                    {
                        FileName = "taskkill",
                        Arguments = $"/PID {process.Id} /T /F",
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        WindowStyle = ProcessWindowStyle.Hidden,
                    });
                    if (kill is not null)
                        await kill.WaitForExitAsync().ConfigureAwait(false);
                }
            }
            catch
            {
                // best-effort
            }
        }
    }

    private static async Task<string> ReadOutputAsync(Task<string> stdoutTask, Task<string> stderrTask)
    {
        var all = Task.WhenAll(stdoutTask, stderrTask);
        var winner = await Task.WhenAny(all, Task.Delay(2000)).ConfigureAwait(false);
        if (!ReferenceEquals(winner, all))
            return "(output truncated after kill)";

        await all.ConfigureAwait(false);
        var stdout = await stdoutTask.ConfigureAwait(false);
        var stderr = await stderrTask.ConfigureAwait(false);
        var sb = new StringBuilder();
        if (!string.IsNullOrEmpty(stdout))
            sb.Append(stdout);
        if (!string.IsNullOrEmpty(stderr))
            sb.Append(stderr);
        return sb.ToString();
    }

    private static int MatchInt(Regex regex, string output)
    {
        var m = regex.Match(output);
        return m.Success && int.TryParse(m.Groups[1].Value, out var n) ? n : 0;
    }
}
