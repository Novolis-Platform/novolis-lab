using Spectre.Console;

namespace BridgeCommander.Bridge;

/// <summary>Spectre.Console rendering for bridge state and exchange beats.</summary>
public static class BridgeSpectreUi
{
    public static void ShowTitle(bool starTrek = true)
    {
        AnsiConsole.Write(new FigletText("Bridge Commander").Color(Color.Cyan1));
        if (starTrek)
        {
            AnsiConsole.MarkupLine("[grey]U.S.S. Novolis · Red alert patrol · per-station voice & color[/]");
            AnsiConsole.MarkupLine("[grey]Novolis.Commands dogfood · Ctrl+C to abort[/]");
        }
        else
        {
            AnsiConsole.MarkupLine("[grey]Voice exchange · Novolis.Commands dogfood · Ctrl+C to abort[/]");
        }

        AnsiConsole.WriteLine();
    }

    public static void ShowBeat(BridgeExchangeBeat beat) =>
        ShowCharacterLine(beat.Character, beat.Display);

    public static void ShowCharacterLine(BridgeCharacter character, string line)
    {
        var style = character.MarkupStyle;
        AnsiConsole.MarkupLine(
            $"[{style}]{Markup.Escape(character.DisplayName)}[/]: [default]{Markup.Escape(line)}[/]");
    }

    public static void RenderBridge(BridgeState state)
    {
        var status = new Panel(Markup.Escape(state.StatusLine))
            .Header("[cyan]Station report[/]")
            .RoundedBorder();

        var ship = new Panel(
            $"[bold]{Markup.Escape(state.ShipName)}[/]\n{Markup.Escape(state.FormatStatus())}")
            .Header("[cyan]Tactical[/]")
            .RoundedBorder();

        var logLines = state.FormatHistory().TakeLast(12).ToArray();
        var logBody = logLines.Length == 0
            ? "[grey](no entries yet)[/]"
            : string.Join('\n', logLines.Select(l => Markup.Escape(l)));

        var log = new Panel(logBody)
            .Header("[cyan]Command log[/]")
            .RoundedBorder()
            .Expand();

        var grid = new Grid();
        grid.AddColumn();
        grid.AddRow(new Columns(ship, status));
        grid.AddRow(log);

        AnsiConsole.Write(grid);
        AnsiConsole.WriteLine();
    }

    public static void ShowCrewLegend()
    {
        AnsiConsole.MarkupLine("[bold]Bridge crew[/]");
        foreach (var character in BridgeCharacterRegistry.All)
        {
            AnsiConsole.Markup(
                $"  [{character.MarkupStyle}]{Markup.Escape(character.DisplayName)}[/]  ");
        }

        AnsiConsole.WriteLine();
        AnsiConsole.WriteLine();
    }

    public static void ShowClosing(bool starTrek = true)
    {
        if (starTrek)
            AnsiConsole.MarkupLine("[green]End of shift. Live long and prosper.[/] [cyan]--interactive[/] for manual orders.");
        else
            AnsiConsole.MarkupLine("[green]Exchange complete.[/] Run with [cyan]--interactive[/] for manual orders.");
    }
}
