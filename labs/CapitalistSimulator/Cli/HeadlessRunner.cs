using CapitalistSimulator.Persistence;
using CapitalistSimulator.Sim;
using Spectre.Console;

namespace CapitalistSimulator.Cli;

internal static class HeadlessRunner
{
    public static int Run(RunOptions options)
    {
        var store = new SaveStore();
        GameWorld world;
        if (options.LoadName is { } loadName)
        {
            world = store.Load(loadName) ?? throw new InvalidOperationException($"Save not found: {loadName}");
        }
        else
        {
            world = WorldFactory.Create(
                options.Scenario,
                options.StartingCash,
                options.AiCount,
                options.AiAggressiveness,
                options.Seed);
        }

        if (!world.FirmsOf(world.Player.Id).Any())
            StarterBootstrap.EnsureStarterRetail(world);

        var proc = new CommandProcessor(world);
        proc.Apply(new AdvanceDaysCommand(options.Days));

        if (options.SaveName is { } saveName)
            store.Save(world, saveName);

        if (!options.Quiet)
            WriteReport(world);

        return world.Win.Lost ? 1 : 0;
    }

    /// <summary>Test helper — same as production starter.</summary>
    internal static void BootstrapRetail(GameWorld world) =>
        StarterBootstrap.EnsureStarterRetail(world);

    private static void WriteReport(GameWorld world)
    {
        var console = AnsiConsole.Console;
        console.MarkupLine("[bold]Capitalist Simulator[/] — headless report");
        console.WriteLine($"Day {world.Day}  Year {world.Year}  Scenario {world.Scenario}");
        console.WriteLine($"Cash ${world.Player.Cash:N0}  Share ${world.Player.SharePrice:N2}  NetWorth ${world.PlayerNetWorth():N0}");
        console.WriteLine($"Month Rev ${world.Player.MonthRevenue:N0}  Exp ${world.Player.MonthExpense:N0}  LastYearProfit~ ${world.Player.LastYearProfit:N0}");
        console.WriteLine($"Firms {world.FirmsOf(world.Player.Id).Count()}  Loans {world.Player.Loans.Sum(l => l.Principal):N0}");
        if (world.Win.Won) console.MarkupLine($"[green]WIN[/] {world.Win.Message}");
        if (world.Win.Lost) console.MarkupLine($"[red]LOSE[/] {world.Win.Message}");

        var coach = TutorialCoach.Next(world);
        console.MarkupLine($"[cyan]Next:[/] {coach.Title} — {coach.Body}");

        var table = new Table().AddColumns("Product", "Sold", "Revenue");
        foreach (var g in world.LastMonthSales.Where(s => s.CorpId.Equals(world.Player.Id))
                     .GroupBy(s => s.ProductId)
                     .OrderByDescending(g => g.Sum(x => x.Revenue))
                     .Take(12))
        {
            table.AddRow(g.Key, g.Sum(x => x.UnitsSold).ToString("N0"), g.Sum(x => x.Revenue).ToString("N0"));
        }
        console.Write(table);

        console.MarkupLine("[dim]Recent news[/]");
        foreach (var n in world.News.TakeLast(8))
            console.WriteLine($"  d{n.Day}: {n.Text}");
    }
}
