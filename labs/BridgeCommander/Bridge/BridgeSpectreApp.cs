using Spectre.Console;

namespace BridgeCommander.Bridge;

/// <summary>Interactive Spectre bridge console (manual orders).</summary>
public static class BridgeSpectreApp
{
    public static async Task RunInteractiveAsync(
        BridgeSession session,
        CancellationToken cancellationToken = default)
    {
        BridgeSpectreUi.ShowTitle();
        AnsiConsole.MarkupLine("[grey]Type orders with station prefix (helm, tactical, weaps…). Empty line skips.[/]");
        AnsiConsole.MarkupLine("[grey]Commands: [cyan]help[/], [cyan]exit[/][/]");
        AnsiConsole.WriteLine();

        while (!cancellationToken.IsCancellationRequested)
        {
            BridgeSpectreUi.RenderBridge(session.State);

            var order = AnsiConsole.Prompt(
                new TextPrompt<string>("[yellow]Captain[/] order")
                    .AllowEmpty()
                    .DefaultValue(""));

            if (string.IsNullOrWhiteSpace(order))
                continue;

            if (order.Equals("exit", StringComparison.OrdinalIgnoreCase) ||
                order.Equals("quit", StringComparison.OrdinalIgnoreCase))
                break;

            await session.TransmitAsync(order, cancellationToken: cancellationToken).ConfigureAwait(false);
            await session.Announcer.AnnounceAsync(session.State.StatusLine, cancellationToken)
                .ConfigureAwait(false);
        }

        AnsiConsole.MarkupLine("[green]Watch relieved. Bridge out.[/]");
    }
}
