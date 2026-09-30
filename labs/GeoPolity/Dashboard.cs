using GeoPolity.Session;
using Novolis.Geopolitics.Core;
using Novolis.Geopolitics.Simulation;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace GeoPolity;

internal static class Dashboard
{
    public static IRenderable Build(
        WorldState world,
        WorldSimulation sim,
        IReadOnlyList<string> headlines,
        bool running,
        int daysPerPulse,
        string? statusNote)
    {
        var wars = world.ActiveWars.Count();
        var cm = world.CountActiveTreatiesOfKind(TreatyKind.CommonMarket);
        var alliances = world.CountActiveTreatiesOfKind(TreatyKind.Alliance);
        var embargoes = world.CountActiveTreatiesOfKind(TreatyKind.EconomicEmbargo);
        var shortage = world.Polities.Sum(p =>
            ResourceKinds.All.Sum(k => p.Balance[k] < 0 ? -p.Balance[k] : 0));
        var maxPower = Math.Max(1.0, world.Polities.Max(p => p.PowerScore));

        var root = new Table().Border(TableBorder.Rounded).Expand();
        root.AddColumn(new TableColumn("[steelblue1]Command[/]").Padding(0, 0, 1, 0));
        root.AddColumn(new TableColumn("[darkorange3]Theatre[/]"));

        var left = new Table().HideHeaders().Border(TableBorder.None);
        left.AddColumn("k");
        left.AddColumn("v");

        left.AddRow("Date", $"[bold]Y{world.Year}[/] · M{world.Month + 1} · D{world.DayOfYear + 1}");
        left.AddRow("Clock", running
            ? $"[green]RUN[/]  {daysPerPulse}d / pulse"
            : "[yellow]PAUSE[/]  Space to step");
        if (!string.IsNullOrEmpty(statusNote))
        {
            left.AddRow("", $"[grey]{Markup.Escape(statusNote)}[/]");
        }

        left.AddRow("World", $"{world.Polities.Count} nations · {world.Provinces.Count} provinces");
        left.AddRow("Wars", ColorWars(wars));
        left.AddRow("Blocs", $"[teal]{cm}[/] markets · [steelblue1]{alliances}[/] alliances · [red]{embargoes}[/] embargoes");
        left.AddRow("Orgs", $"{world.ActiveOrgs.Count()} active · {sim.Telemetry.OrgJoins} joins");
        left.AddRow("Strain", shortage < 500
            ? $"[green]{shortage:0}[/] shortage idx"
            : shortage < 3000
                ? $"[yellow]{shortage:0}[/] shortage idx"
                : $"[red]{shortage:0}[/] shortage idx");
        left.AddRow("Civics",
            $"legit [teal]{sim.Telemetry.MeanLegitimacy:0.00}[/] · approval [teal]{sim.Telemetry.MeanApproval:0.00}[/]");
        left.AddRow("Campaign",
            $"wars {sim.Telemetry.WarsStarted}/{sim.Telemetry.WarsEnded} · captured {sim.Telemetry.ProvincesCaptured}");

        left.AddRow("", "");
        left.AddRow("[grey]Power[/]", "[grey]top 5[/]");
        var rank = 1;
        foreach (var p in world.Polities.OrderByDescending(x => x.PowerScore).Take(5))
        {
            var bar = Bar(p.PowerScore / maxPower, 14);
            var gov = Markup.Escape(ShortGov(p.Government));
            left.AddRow(
                $"{rank}. {Markup.Escape(Truncate(p.Name, 12))}",
                $"[darkorange3]{bar}[/] {p.PowerScore:0} [grey]{gov} L{p.Civic.Legitimacy:0.00}[/]");
            rank++;
        }

        var theatre = new Rows(
            ContinentPanel(world),
            OrgPanel(world),
            HeadlinePanel(headlines));

        root.AddRow(left, theatre);
        return root;
    }

    public static bool IsHeadline(GeoEvent e) => HeadlineFeedController.IsHeadline(e);

    public static string FormatHeadline(Headline h)
    {
        var tag = h.Tag switch
        {
            "WAR" => "[red]WAR[/]",
            "PEACE" => "[green]PEACE[/]",
            "ALLY" => "[steelblue1]ALLY[/]",
            "TAKE" => "[darkorange3]TAKE[/]",
            "ORG" => "[teal]ORG[/]",
            "EMB" => "[red]EMB[/]",
            "BUD" => "[yellow]BUD[/]",
            "RES" => "[yellow]RES[/]",
            "BUILD" => "[darkorange3]BUILD[/]",
            _ => "[grey]···[/]",
        };
        return $"{tag} [grey]D{h.Day}[/] {Markup.Escape(h.Text)}";
    }

    public static string FormatHeadline(GeoEvent e) => FormatHeadline(HeadlineFeedController.FromEvent(e));

    private static Panel ContinentPanel(WorldState world)
    {
        var chart = new BarChart()
            .Width(42)
            .Label("[steelblue1]Continent power[/]")
            .CenterLabel();

        var colors = new[]
        {
            Color.SteelBlue1, Color.Teal, Color.DarkOrange3, Color.Orange3,
            Color.CadetBlue, Color.Aqua, Color.Gold1, Color.Grey,
        };

        var groups = world.Polities
            .GroupBy(p => p.Continent)
            .OrderByDescending(g => g.Sum(p => p.PowerScore))
            .Take(8)
            .ToList();

        for (var i = 0; i < groups.Count; i++)
        {
            var g = groups[i];
            var wars = world.ActiveWars.Count(w =>
                world.Polity(w.Attacker).Continent == g.Key
                || world.Polity(w.Defender).Continent == g.Key);
            chart.AddItem($"{g.Key} ({wars}w)", g.Sum(p => p.PowerScore), colors[i % colors.Length]);
        }

        return new Panel(chart)
        {
            Header = new PanelHeader(" Continents "),
            Border = BoxBorder.Square,
            BorderStyle = new Style(Color.SteelBlue1),
            Padding = new Padding(1, 0),
        };
    }

    private static Panel OrgPanel(WorldState world)
    {
        var lines = world.ActiveOrgs
            .OrderByDescending(o => o.MemberIds.Count)
            .Take(12)
            .Select(o =>
            {
                var bar = Bar(o.MemberIds.Count / 25.0, 8);
                var tag = Markup.Escape(GeoPolity.Presentation.TheatreLabels.OrgKind(o.Kind));
                return $"[grey]{tag,-8}[/] [teal]{Markup.Escape(Truncate(o.Name, 16)),-16}[/] {bar} {o.MemberIds.Count}";
            });

        var body = string.Join('\n', lines);
        if (string.IsNullOrEmpty(body))
        {
            body = "[grey]no blocs[/]";
        }

        return new Panel(body)
        {
            Header = new PanelHeader(" Supranationals "),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Teal),
            Padding = new Padding(1, 0),
        };
    }

    private static Panel HeadlinePanel(IReadOnlyList<string> headlines)
    {
        var body = headlines.Count == 0
            ? "[grey]quiet — waiting for wars, peaces, orgs…[/]"
            : string.Join('\n', headlines);
        return new Panel(body)
        {
            Header = new PanelHeader(" Headlines "),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Grey),
            Height = 9,
            Padding = new Padding(1, 0),
        };
    }

    private static string ColorWars(int wars) => wars switch
    {
        0 => "[green]0[/]",
        < 15 => $"[yellow]{wars}[/]",
        _ => $"[red]{wars}[/]  [grey]hot[/]",
    };

    private static string Bar(double t, int width)
    {
        t = Math.Clamp(t, 0, 1);
        var filled = (int)Math.Round(t * width);
        return new string('█', filled) + new string('░', Math.Max(0, width - filled));
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..(max - 1)] + "…";

    private static string ShortGov(GovernmentType g) => g switch
    {
        GovernmentType.Democracy => "Dem",
        GovernmentType.Multiparty => "Multi",
        GovernmentType.SingleParty => "1Pty",
        GovernmentType.Autocracy => "Auto",
        GovernmentType.MilitaryJunta => "Junta",
        GovernmentType.Monarchy => "Mon",
        _ => "?",
    };
}
