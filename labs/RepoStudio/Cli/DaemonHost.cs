using Novolis.IO.Git;
using Spectre.Console;

namespace RepoStudio.Cli;

internal static class DaemonHost
{
    public static async Task RunAsync(CliOptions options)
    {
        var root = GitWorkspace.ResolveRoot(options.Root);
        await using var scheduler = new FetchScheduler();
        scheduler.CycleCompleted += (_, result) =>
            AnsiConsole.MarkupLine($"[grey]{DateTimeOffset.UtcNow:HH:mm:ss}[/] fetch cycle ok={result.Ok} repos={result.Results.Count}");
        scheduler.CycleFailed += (_, ex) =>
            AnsiConsole.MarkupLine($"[red]fetch cycle failed[/] {ex.Message}");

        AnsiConsole.MarkupLine($"[green]daemon fetch[/] root={root} interval={options.IntervalSeconds}s (Ctrl+C to stop)");
        scheduler.Start(root, TimeSpan.FromSeconds(options.IntervalSeconds));

        var tcs = new TaskCompletionSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            tcs.TrySetResult();
        };
        await tcs.Task;
        scheduler.Stop();
    }
}
