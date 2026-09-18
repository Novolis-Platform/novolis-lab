using Novolis.Economy.Core;
using Novolis.Economy.Core.Holdings;
using Novolis.Geopolitics.Core;
using Spectre.Console;

namespace PolityTriad;

static class Dashboard
{
    public static Table Build(TriadWorld.Model model, Queue<string> log, bool running, int pulseMs)
    {
        var world = model.World;
        var alpha = world.Polity(new PolityId(0));
        var beta = world.Polity(new PolityId(1));
        var gamma = world.Polity(new PolityId(2));
        var month = world.Day / WorldState.DaysPerMonth;
        var atWar = world.AreAtWar(new PolityId(0), new PolityId(1));
        var h = model.History;

        var root = new Table().Expand().Border(TableBorder.None);
        root.AddColumn(new TableColumn(""));

        root.AddRow(new Markup(
            $"[bold steelblue1]Polity Triad[/]  M[bold]{month}[/] d{world.Day}  " +
            $"phase [yellow]{model.Phase}[/]  " +
            (running ? "[green]RUN[/]" : "[yellow]PAUSE[/]") +
            $"  {pulseMs}ms  agents {(model.AgentsEnabled ? "[green]on[/]" : "[grey]off[/]")}  " +
            (atWar ? "[red]WAR α↔β[/]" : "[grey]peace[/]") +
            $"  captures [bold]{model.CapturesTotal}[/]"));

        var civics = new Table().Expand().Border(TableBorder.Rounded);
        civics.AddColumn("Alpha · Democracy · Economy→Civics");
        civics.AddColumn("Beta · Autocracy · Economy→Civics");
        civics.AddColumn("Gamma · Multiparty · geo civic");
        civics.AddRow(CivicBlock(alpha, model.AlphaEconomy, TriadWorld.AlphaState),
            CivicBlock(beta, model.BetaEconomy, TriadWorld.BetaState),
            GeoOnlyBlock(gamma, world));
        root.AddRow(civics);

        var eco = new Table().Expand().Border(TableBorder.Simple);
        eco.AddColumn("Industrial");
        eco.AddColumn("α cash / stocks");
        eco.AddColumn("β cash / stocks");
        eco.AddRow(
            new Markup(
                $"tax α {model.AlphaEconomy.Flows.TaxCollected.Amount:0.#}  β {model.BetaEconomy.Flows.TaxCollected.Amount:0.#}\n" +
                $"xfer α {model.AlphaEconomy.Flows.TransfersPaid.Amount:0.#}  β {model.BetaEconomy.Flows.TransfersPaid.Amount:0.#}\n" +
                $"wages α {model.AlphaEconomy.Flows.WagesAccrued.Amount:0.#}  β {model.BetaEconomy.Flows.WagesAccrued.Amount:0.#}"),
            EcoCash(model.AlphaEconomy, TriadWorld.AlphaState, TriadWorld.AlphaHh, TriadWorld.AlphaFirm, TriadWorld.RegionA),
            EcoCash(model.BetaEconomy, TriadWorld.BetaState, TriadWorld.BetaHh, TriadWorld.BetaFirm, TriadWorld.RegionB));
        root.AddRow(eco);

        var force = new Table().Expand().Border(TableBorder.Simple);
        force.AddColumn("Force");
        force.AddColumn("Land");
        force.AddColumn("Air");
        force.AddColumn("Naval");
        force.AddColumn("Total");
        force.AddColumn("ctrl");
        force.AddRow("α", $"{alpha.Military.Land:0}", $"{alpha.Military.Air:0}", $"{alpha.Military.Naval:0}",
            $"{alpha.Military.Total:0.#}", $"{Control(world, alpha.Id):0.00}");
        force.AddRow("β", $"{beta.Military.Land:0}", $"{beta.Military.Air:0}", $"{beta.Military.Naval:0}",
            $"{beta.Military.Total:0.#}", $"{Control(world, beta.Id):0.00}");
        force.AddRow("γ", $"{gamma.Military.Land:0}", $"{gamma.Military.Air:0}", $"{gamma.Military.Naval:0}",
            $"{gamma.Military.Total:0.#}", $"{Control(world, gamma.Id):0.00}");
        root.AddRow(force);

        var diplo = new Table().Expand().Border(TableBorder.Simple);
        diplo.AddColumn("Theatre");
        diplo.AddRow(new Markup(
            $"rel αβ [teal]{world.Relations.Get(new PolityId(0), new PolityId(1)):0}[/]  " +
            $"αγ [teal]{world.Relations.Get(new PolityId(0), new PolityId(2)):0}[/]  " +
            $"βγ [teal]{world.Relations.Get(new PolityId(1), new PolityId(2)):0}[/]\n" +
            $"treaties CM {world.CountActiveTreatiesOfKind(TreatyKind.CommonMarket)}  " +
            $"R&D {world.CountActiveTreatiesOfKind(TreatyKind.ResearchPartnership)}  " +
            $"Peace {world.CountActiveTreatiesOfKind(TreatyKind.Peace)}  " +
            $"wars {world.ActiveWars.Count()}\n" +
            $"trade CMΣ {model.Telemetry.CommonMarketVolume:0.#}  worldΣ {model.Telemetry.WorldMarketVolume:0.#}  " +
            $"prov captured {model.Telemetry.ProvincesCaptured}\n" +
            $"map {MapLine(world)}"));
        root.AddRow(diplo);

        if (h.AlphaLegitimacy.Count > 1)
        {
            root.AddRow(new Markup(
                $"[grey]sparks[/] αL {TriadHistory.Spark(h.AlphaLegitimacy)}  " +
                $"αWF {TriadHistory.Spark(h.AlphaWarFatigue)}  " +
                $"βWF {TriadHistory.Spark(h.BetaWarFatigue)}  " +
                $"trade {TriadHistory.Spark(h.TradeVolume)}"));
        }

        var feed = new Panel(string.Join('\n', log.Reverse().Take(10).Reverse().Select(Markup.Escape)))
        {
            Header = new PanelHeader("Month log"),
            Border = BoxBorder.Rounded,
            Expand = true,
        };
        root.AddRow(feed);
        root.AddRow(new Markup(
            "[grey]Keys:[/] [bold]Space[/] pause  [bold]1–4[/] speed  [bold]W[/] war  [bold]P[/] peace  " +
            "[bold]C[/] CM  [bold]R[/] R&D  [bold]A[/] agents  [bold]Q[/] quit  " +
            "[grey]|[/] [bold]--headless 36[/]"));

        return root;
    }

    static Markup CivicBlock(Polity p, EconomyState eco, LegalEntityId state) => new(
        $"[bold]{p.Name}[/]  GDP {p.Gdp:0}  treas {eco.Entities[state].Cash.Amount:0.#}\n" +
        $"L {p.Civic.Legitimacy:0.00}  A {p.Civic.Approval:0.00}  HD {p.Civic.HumanDevelopment:0.00}\n" +
        $"WF {p.Civic.WarFatigue:0.00}  corr {p.Civic.Corruption:0.00}  stab {p.Stability:0.00}\n" +
        $"tax {p.Policy.HouseholdTaxRate:0.00}  xfer {p.Policy.TransferShare:0.00}  mil {p.Policy.MilitaryShare:0.00}\n" +
        $"tech {p.TechLevel:0.00}  last tax {p.Civic.LastTaxCollected:0.#}");

    static Markup GeoOnlyBlock(Polity p, WorldState world) => new(
        $"[bold]{p.Name}[/]  GDP {p.Gdp:0}  treas {p.Treasury:0.#}\n" +
        $"L {p.Civic.Legitimacy:0.00}  A {p.Civic.Approval:0.00}  HD {p.Civic.HumanDevelopment:0.00}\n" +
        $"WF {p.Civic.WarFatigue:0.00}  corr {p.Civic.Corruption:0.00}  stab {p.Stability:0.00}\n" +
        $"tax {p.Policy.HouseholdTaxRate:0.00}  mil {p.Policy.MilitaryShare:0.00}  tech {p.TechLevel:0.00}\n" +
        $"shortage {ResourceKinds.All.Sum(k => p.Balance[k] < 0 ? -p.Balance[k] : 0):0.#}  " +
        $"owned {world.CountOwnedProvinces(p.Id)}");

    static Markup EcoCash(
        EconomyState eco, LegalEntityId state, LegalEntityId hh, LegalEntityId firm, RegionId region) => new(
        $"State [teal]{eco.Entities[state].Cash.Amount:0.#}[/]  HH [teal]{eco.Entities[hh].Cash.Amount:0.#}[/]\n" +
        $"Firm [teal]{eco.Entities[firm].Cash.Amount:0.#}[/]\n" +
        $"ore {HoldingLedger.GetQuantity(eco, firm, region, TriadWorld.OreId):0.#}  " +
        $"widgets {HoldingLedger.GetQuantity(eco, firm, region, TriadWorld.WidgetId):0.#}");

    static double Control(WorldState world, PolityId id) => world.PopWeightedControlRatio(id);

    static string MapLine(WorldState world) =>
        string.Join(' ', world.Provinces.OrderBy(p => p.Id.Value).Select(p =>
        {
            var home = p.HomePolityId.Value switch { 0 => "A", 1 => "B", _ => "G" };
            var own = p.OwnerId.Value switch { 0 => "α", 1 => "β", _ => "γ" };
            var mark = p.OwnerId == p.HomePolityId ? $"{home}{p.Id.Value}" : $"{home}{p.Id.Value}→{own}";
            return mark;
        }));
}
