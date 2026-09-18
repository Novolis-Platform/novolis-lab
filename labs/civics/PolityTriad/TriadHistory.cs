using System.Globalization;
using Novolis.Economy.Core;
using Novolis.Economy.Core.Holdings;
using Novolis.Geopolitics.Core;

namespace PolityTriad;

/// <summary>Per-month samples + discrete milestones for headless evidence reporting.</summary>
sealed class TriadHistory
{
    public List<MonthSample> Months { get; } = [];
    public List<Milestone> Milestones { get; } = [];

    // Sparklines still used by live dashboard
    public List<double> AlphaLegitimacy => Months.Select(m => m.Alpha.Legitimacy).ToList();
    public List<double> AlphaApproval => Months.Select(m => m.Alpha.Approval).ToList();
    public List<double> AlphaWarFatigue => Months.Select(m => m.Alpha.WarFatigue).ToList();
    public List<double> AlphaStateCash => Months.Select(m => m.Alpha.StateCash).ToList();
    public List<double> AlphaGdp => Months.Select(m => m.Alpha.Gdp).ToList();
    public List<double> BetaWarFatigue => Months.Select(m => m.Beta.WarFatigue).ToList();
    public List<double> BetaLegitimacy => Months.Select(m => m.Beta.Legitimacy).ToList();
    public List<double> TradeVolume => Months.Select(m => m.TradeDelta).ToList();
    public List<int> Battles => Months.Select(m => m.Battles).ToList();
    public List<string> Phases => Months.Select(m => m.Phase).ToList();

    public void Mark(string layer, string what) =>
        Milestones.Add(new Milestone(Months.Count + 1, layer, what));

    public void Record(MonthSample sample) => Months.Add(sample);

    public static string Spark(IReadOnlyList<double> series, int width = 24)
    {
        if (series.Count == 0)
            return "";
        const string blocks = "▁▂▃▄▅▆▇█";
        var take = series.Count <= width ? series : series.Skip(series.Count - width).ToList();
        var min = take.Min();
        var max = take.Max();
        var span = Math.Max(1e-9, max - min);
        return string.Concat(take.Select(v =>
        {
            var t = (v - min) / span;
            var i = (int)Math.Round(t * (blocks.Length - 1));
            return blocks[Math.Clamp(i, 0, blocks.Length - 1)];
        }));
    }

    public void WriteEvidenceReport(TriadWorld.Model model, int months, TimeSpan elapsed)
    {
        var inv = CultureInfo.InvariantCulture;
        var world = model.World;
        var alpha = world.Polity(new PolityId(0));
        var beta = world.Polity(new PolityId(1));
        var gamma = world.Polity(new PolityId(2));

        Console.WriteLine("=== Polity Triad — evidence report ===");
        Console.WriteLine(
            $"Run: {months} months in {elapsed.TotalSeconds.ToString("0.00", inv)}s  " +
            $"final phase={model.Phase}  samples={Months.Count}");
        Console.WriteLine();

        WriteTimeline(inv);
        WriteEconomyEvidence(model, inv);
        WriteCivicsEvidence(model, inv);
        WritePopulationEvidence(model, inv);
        WriteGeopoliticsEvidence(model, alpha, beta, gamma, inv);
        WriteCrossChecks(model, inv);
    }

    void WritePopulationEvidence(TriadWorld.Model model, CultureInfo inv)
    {
        Console.WriteLine("--- Population / mobility ---");
        if (Months.Count == 0)
        {
            Console.WriteLine("  (no samples)");
            Console.WriteLine();
            return;
        }

        var first = Months[0];
        var last = Months[^1];
        Console.WriteLine(
            $"  α pop M1→M{Months.Count}: {first.Alpha.Population.ToString("0", inv)} → {last.Alpha.Population.ToString("0", inv)}  " +
            $"netΣ {Months.Sum(m => m.Alpha.NetMigration).ToString("0", inv)}");
        Console.WriteLine(
            $"  β pop M1→M{Months.Count}: {first.Beta.Population.ToString("0", inv)} → {last.Beta.Population.ToString("0", inv)}");
        Console.WriteLine(
            $"  γ pop M1→M{Months.Count}: {first.Gamma.Population.ToString("0", inv)} → {last.Gamma.Population.ToString("0", inv)}");
        Console.WriteLine(
            $"  Telemetry migrated {model.Telemetry.PopulationMigrated.ToString("0", inv)}  " +
            $"displaced {model.Telemetry.RefugeesDisplaced.ToString("0", inv)}");
        var peakPush = Months.MaxBy(m => m.Alpha.EmigrationPressure)!;
        Console.WriteLine(
            $"  Peak α emigration pressure {peakPush.Alpha.EmigrationPressure.ToString("0.00", inv)} at M{peakPush.Month} " +
            $"(tax {peakPush.Alpha.HouseholdTaxRate.ToString("0.00", inv)})");
        Console.WriteLine($"  α pop spark: {Spark(Months.Select(m => m.Alpha.Population).ToList())}");
        Console.WriteLine($"  α push spark:{Spark(Months.Select(m => m.Alpha.EmigrationPressure).ToList())}");
        Console.WriteLine();
    }

    void WriteTimeline(CultureInfo inv)
    {
        Console.WriteLine("--- Timeline (milestones) ---");
        if (Milestones.Count == 0)
        {
            Console.WriteLine("  (none)");
        }
        else
        {
            foreach (var m in Milestones)
                Console.WriteLine($"  M{m.MonthIndex,2} [{m.Layer,-12}] {m.What}");
        }

        // Phase spans
        if (Months.Count > 0)
        {
            Console.WriteLine("  Phase spans:");
            var start = 1;
            var cur = Months[0].Phase;
            for (var i = 1; i <= Months.Count; i++)
            {
                var next = i < Months.Count ? Months[i].Phase : null;
                if (next == cur)
                    continue;
                Console.WriteLine($"    M{start}–M{i}: {cur}");
                start = i + 1;
                cur = next ?? cur;
            }
        }

        Console.WriteLine();
    }

    void WriteEconomyEvidence(TriadWorld.Model model, CultureInfo inv)
    {
        Console.WriteLine("--- Economy (cash ledgers + production) ---");
        if (Months.Count == 0)
        {
            Console.WriteLine("  (no samples)");
            Console.WriteLine();
            return;
        }

        var peace = Months.Where(m => m.Phase is "peace" or "integration").ToList();
        var war = Months.Where(m => m.Phase is "war" or "war-fatigue").ToList();

        static (double prod, double tax, double wages, double ore) Avg(IReadOnlyList<MonthSample> xs) =>
            xs.Count == 0
                ? (0, 0, 0, 0)
                : (xs.Average(m => m.Alpha.ProductionValue),
                    xs.Average(m => m.Alpha.TaxCollected),
                    xs.Average(m => m.Alpha.Wages),
                    xs.Average(m => m.Alpha.OreStock));

        var p = Avg(peace);
        var w = Avg(war);

        Console.WriteLine(
            $"  Alpha peacetime avg/mo: prodVal {p.prod.ToString("0.00", inv)}  " +
            $"tax {p.tax.ToString("0.00", inv)}  wages {p.wages.ToString("0.00", inv)}  ore {p.ore.ToString("0.0", inv)}");
        Console.WriteLine(
            $"  Alpha wartime   avg/mo: prodVal {w.prod.ToString("0.00", inv)}  " +
            $"tax {w.tax.ToString("0.00", inv)}  wages {w.wages.ToString("0.00", inv)}  ore {w.ore.ToString("0.0", inv)}");

        var oreDrop = p.ore > 0 ? (1.0 - w.ore / p.ore) * 100.0 : 0;
        var prodDrop = p.prod > 0 ? (1.0 - w.prod / p.prod) * 100.0 : 0;
        Console.WriteLine(
            $"  Wartime vs peace: ore stock {oreDrop.ToString("0.0", inv)}% lower  " +
            $"production value {prodDrop.ToString("0.0", inv)}% lower");

        var first = Months[0];
        var last = Months[^1];
        Console.WriteLine(
            $"  Alpha State cash M1→M{Months.Count}: " +
            $"{first.Alpha.StateCash.ToString("0.0", inv)} → {last.Alpha.StateCash.ToString("0.0", inv)}  " +
            $"(Economy State entity is treasury truth)");
        Console.WriteLine(
            $"  Alpha HH/Firm cash now: " +
            $"{model.AlphaEconomy.Entities[TriadWorld.AlphaHh].Cash.Amount.ToString("0.0", inv)} / " +
            $"{model.AlphaEconomy.Entities[TriadWorld.AlphaFirm].Cash.Amount.ToString("0.0", inv)}");
        Console.WriteLine(
            $"  Beta  State cash M1→M{Months.Count}: " +
            $"{first.Beta.StateCash.ToString("0.0", inv)} → {last.Beta.StateCash.ToString("0.0", inv)}");

        var aTaxSum = Months.Sum(m => m.Alpha.TaxCollected);
        var aXferSum = Months.Sum(m => m.Alpha.Transfers);
        var aWageSum = Months.Sum(m => m.Alpha.Wages);
        var aProdSum = Months.Sum(m => m.Alpha.ProductionValue);
        var bTaxSum = Months.Sum(m => m.Beta.TaxCollected);
        Console.WriteLine(
            $"  Cumulative α flows: tax {aTaxSum.ToString("0.0", inv)}  " +
            $"xfer {aXferSum.ToString("0.0", inv)}  wages {aWageSum.ToString("0.0", inv)}  " +
            $"production value {aProdSum.ToString("0.0", inv)}");
        Console.WriteLine(
            $"  Cumulative β tax: {bTaxSum.ToString("0.0", inv)}  " +
            $"(independent EconomyState for Beta Autocracy)");

        var starved = war.OrderBy(m => m.Alpha.ProductionValue).ThenBy(m => m.Alpha.OreStock).FirstOrDefault();
        if (starved is not null)
        {
            Console.WriteLine(
                $"  Tightest war month M{starved.Month}: α ore {starved.Alpha.OreStock.ToString("0.0", inv)}  " +
                $"prodVal {starved.Alpha.ProductionValue.ToString("0.00", inv)}  " +
                $"tax {starved.Alpha.TaxCollected.ToString("0.00", inv)}  " +
                $"Δwidgets {starved.Alpha.WidgetsProduced.ToString("0.00", inv)}");
        }

        Console.WriteLine(
            $"  α tax flow spark: {Spark(Months.Select(m => m.Alpha.TaxCollected).ToList())}");
        Console.WriteLine(
            $"  α prodVal spark:  {Spark(Months.Select(m => m.Alpha.ProductionValue).ToList())}");
        Console.WriteLine(
            $"  α ore stock spark:{Spark(Months.Select(m => m.Alpha.OreStock).ToList())}");
        Console.WriteLine();
    }

    void WriteCivicsEvidence(TriadWorld.Model model, CultureInfo inv)
    {
        Console.WriteLine("--- Civics (stocks from Economy delivery + geo facts) ---");
        if (Months.Count == 0)
        {
            Console.WriteLine("  (no samples)");
            Console.WriteLine();
            return;
        }

        var first = Months[0];
        var last = Months[^1];
        var peakWfA = Months.MaxBy(m => m.Alpha.WarFatigue)!;
        var peakWfB = Months.MaxBy(m => m.Beta.WarFatigue)!;
        var minLegA = Months.MinBy(m => m.Alpha.Legitimacy)!;
        var maxForceA = Months.MaxBy(m => m.Alpha.ForceDemand)!;

        Console.WriteLine(
            $"  Alpha L/A/WF/HD M1: " +
            $"{first.Alpha.Legitimacy.ToString("0.00", inv)}/" +
            $"{first.Alpha.Approval.ToString("0.00", inv)}/" +
            $"{first.Alpha.WarFatigue.ToString("0.00", inv)}/" +
            $"{first.Alpha.HumanDevelopment.ToString("0.00", inv)}");
        Console.WriteLine(
            $"  Alpha L/A/WF/HD M{Months.Count}: " +
            $"{last.Alpha.Legitimacy.ToString("0.00", inv)}/" +
            $"{last.Alpha.Approval.ToString("0.00", inv)}/" +
            $"{last.Alpha.WarFatigue.ToString("0.00", inv)}/" +
            $"{last.Alpha.HumanDevelopment.ToString("0.00", inv)}");
        Console.WriteLine(
            $"  Peak α war-fatigue: {peakWfA.Alpha.WarFatigue.ToString("0.00", inv)} at M{peakWfA.Month}  " +
            $"(ActiveWars fed into PeriodContext)");
        Console.WriteLine(
            $"  Peak β war-fatigue: {peakWfB.Beta.WarFatigue.ToString("0.00", inv)} at M{peakWfB.Month}");
        Console.WriteLine(
            $"  Lowest α legitimacy: {minLegA.Alpha.Legitimacy.ToString("0.00", inv)} at M{minLegA.Month} " +
            $"phase={minLegA.Phase}");

        // Delivery bridge: civic LastTax should track economy tax
        var deliveryErr = Months
            .Select(m => Math.Abs(m.Alpha.CivicLastTax - m.Alpha.TaxCollected))
            .DefaultIfEmpty(0)
            .Average();
        Console.WriteLine(
            $"  Economy→Civics delivery match (avg |civic.LastTax − eco.tax|): " +
            $"{deliveryErr.ToString("0.000", inv)}  (PeriodContextFromDelivery)");

        var agentMoves = Milestones.Count(m => m.Layer == "civics-agent");
        Console.WriteLine(
            $"  Fiscal agent policy moves logged: {agentMoves}  " +
            $"(HeuristicFiscalAgent on NationState intent)");

        Console.WriteLine(
            $"  Peak α force capability demand: {maxForceA.Alpha.ForceDemand.ToString("0.00", inv)} at M{maxForceA.Month}  " +
            $"→ mapped to land/air/naval");
        Console.WriteLine(
            $"  Alpha force Total M1→M{Months.Count}: " +
            $"{first.Alpha.ForceTotal.ToString("0.0", inv)} → {last.Alpha.ForceTotal.ToString("0.0", inv)}");

        // Gamma uses geo CivicEngine (GDP×rate path), not Economy delivery
        Console.WriteLine(
            $"  Gamma (geo civic only) L {last.Gamma.Legitimacy.ToString("0.00", inv)}  " +
            $"tech {last.Gamma.Tech.ToString("0.00", inv)}  " +
            $"(R&D treaty multiplies research when active)");

        Console.WriteLine($"  α WF spark: {Spark(AlphaWarFatigue)}");
        Console.WriteLine($"  α L  spark: {Spark(AlphaLegitimacy)}");
        Console.WriteLine($"  β WF spark: {Spark(BetaWarFatigue)}");
        Console.WriteLine();
    }

    void WriteGeopoliticsEvidence(
        TriadWorld.Model model, Polity alpha, Polity beta, Polity gamma, CultureInfo inv)
    {
        Console.WriteLine("--- Geopolitics (theatre: trade, treaties, war, map) ---");
        var world = model.World;
        var tel = model.Telemetry;

        Console.WriteLine(
            $"  Telemetry: wars started {tel.WarsStarted}  ended {tel.WarsEnded}  " +
            $"captures {tel.ProvincesCaptured}  treaties signed {tel.TreatiesSigned}");
        Console.WriteLine(
            $"  Trade Σ CM {tel.CommonMarketVolume.ToString("0.0", inv)}  " +
            $"world {tel.WorldMarketVolume.ToString("0.0", inv)}  " +
            $"shortage events {tel.ResourceShortageEvents}");

        if (Months.Count > 0)
        {
            var preCm = Months.Where(m => m.Month <= 2).ToList();
            var withCm = Months.Where(m => m.Month is > 2 and < 8).ToList();
            var wartime = Months.Where(m => m.AtWar).ToList();
            double AvgTrade(IReadOnlyList<MonthSample> xs) =>
                xs.Count == 0 ? 0 : xs.Average(m => m.TradeDelta);

            Console.WriteLine(
                $"  Trade Δ/mo pre-CM: {AvgTrade(preCm).ToString("0.00", inv)}  " +
                $"post-CM (pre-war): {AvgTrade(withCm).ToString("0.00", inv)}  " +
                $"wartime: {AvgTrade(wartime).ToString("0.00", inv)}");
        }

        Console.WriteLine(
            $"  Relations now: αβ {world.Relations.Get(alpha.Id, beta.Id).ToString("0", inv)}  " +
            $"αγ {world.Relations.Get(alpha.Id, gamma.Id).ToString("0", inv)}  " +
            $"βγ {world.Relations.Get(beta.Id, gamma.Id).ToString("0", inv)}");
        Console.WriteLine(
            $"  Active treaties: CM {world.CountActiveTreatiesOfKind(TreatyKind.CommonMarket)}  " +
            $"R&D {world.CountActiveTreatiesOfKind(TreatyKind.ResearchPartnership)}  " +
            $"Peace {world.CountActiveTreatiesOfKind(TreatyKind.Peace)}");

        Console.WriteLine(
            $"  Control ratios: α {Control(world, alpha.Id).ToString("0.00", inv)}  " +
            $"β {Control(world, beta.Id).ToString("0.00", inv)}  " +
            $"γ {Control(world, gamma.Id).ToString("0.00", inv)}");

        Console.WriteLine("  Province map (home→owner if flipped):");
        foreach (var p in world.Provinces.OrderBy(x => x.Id.Value))
        {
            var home = Tag(p.HomePolityId);
            var own = Tag(p.OwnerId);
            var mark = p.OwnerId == p.HomePolityId
                ? $"{p.Name} ({home})"
                : $"{p.Name} OCCUPIED {home}→{own}";
            Console.WriteLine($"    {mark}");
        }

        var captureMs = Milestones.Where(m => m.Layer == "conflict").ToList();
        if (captureMs.Count > 0)
        {
            Console.WriteLine("  Capture events:");
            foreach (var c in captureMs)
                Console.WriteLine($"    M{c.MonthIndex}: {c.What}");
        }

        if (Months.Count > 0)
        {
            var peakShort = Months.MaxBy(m => m.Alpha.GeoShortage + m.Beta.GeoShortage)!;
            Console.WriteLine(
                $"  Peak geo resource shortage (α+β Balance): " +
                $"{(peakShort.Alpha.GeoShortage + peakShort.Beta.GeoShortage).ToString("0.0", inv)} at M{peakShort.Month}");
            Console.WriteLine($"  trade Δ spark: {Spark(TradeVolume)}");
            Console.WriteLine($"  battles/mo:    {Spark(Battles.Select(b => (double)b).ToList())}");
        }

        Console.WriteLine();
    }

    void WriteCrossChecks(TriadWorld.Model model, CultureInfo inv)
    {
        Console.WriteLine("--- Cross-layer checks (did the triad couple?) ---");
        var checks = new List<(bool ok, string text)>();

        var warMonths = Months.Where(m => m.AtWar).ToList();
        var peaceMonths = Months.Where(m => !m.AtWar && m.Phase is "peace" or "integration").ToList();

        if (warMonths.Count > 0 && peaceMonths.Count > 0)
        {
            var peaceOre = peaceMonths.Average(m => m.Alpha.OreStock);
            var warOre = warMonths.Average(m => m.Alpha.OreStock);
            var peaceProd = peaceMonths.Average(m => m.Alpha.ProductionValue);
            var warProd = warMonths.Average(m => m.Alpha.ProductionValue);
            checks.Add((warOre < peaceOre * 0.75,
                $"Economy×Geopolitics: wartime α ore ({warOre.ToString("0.0", inv)}) " +
                $"< 75% of peacetime ({peaceOre.ToString("0.0", inv)})"));
            checks.Add((warMonths.Any(m => m.Alpha.OreStock < 0.5) || warProd < peaceProd * 0.85,
                $"Economy×Geopolitics: war binds production " +
                $"(peace prodVal {peaceProd.ToString("0.00", inv)} → war {warProd.ToString("0.00", inv)}; " +
                $"ore hit zero={warMonths.Any(m => m.Alpha.OreStock < 0.5)})"));
        }

        if (warMonths.Count > 0)
        {
            var peakWf = warMonths.Max(m => m.Alpha.WarFatigue);
            checks.Add((peakWf > 0.05,
                $"Civics×Geopolitics: α war-fatigue rose under ActiveWars (peak {peakWf.ToString("0.00", inv)})"));
        }

        var deliveryOk = Months.Count > 0 &&
            Months.Average(m => Math.Abs(m.Alpha.CivicLastTax - m.Alpha.TaxCollected)) < 0.05;
        checks.Add((deliveryOk,
            "Economy×Civics: civic.LastTax tracks Economy TaxCollected (delivery bridge)"));

        var hadCm = Milestones.Any(m => m.What.Contains("Common Market", StringComparison.Ordinal));
        var cmTrade = Months.Where(m => m.Month > 2).Sum(m => m.TradeDelta);
        checks.Add((hadCm && cmTrade > 0,
            $"Diplomacy×Trade: Common Market signed and post-sign tradeΔ Σ={cmTrade.ToString("0.0", inv)}"));

        var captures = model.CapturesTotal;
        var occupied = model.World.Provinces.Count(p => p.OwnerId != p.HomePolityId);
        checks.Add((captures > 0 && occupied > 0,
            $"Conflict×Map: {captures} capture(s), {occupied} province(s) still occupied after peace"));

        var forceGrew = Months.Count >= 2 && Months[^1].Alpha.ForceTotal > Months[0].Alpha.ForceTotal;
        checks.Add((forceGrew,
            $"Civics×Conflict: α force grew from capability demand " +
            $"({Months[0].Alpha.ForceTotal.ToString("0.0", inv)}→{Months[^1].Alpha.ForceTotal.ToString("0.0", inv)})"));

        var netOut = Months.Sum(m => m.Alpha.NetMigration) < -10_000;
        var pushRose = Months.Any(m => m.Alpha.EmigrationPressure > 0.35);
        checks.Add((netOut && pushRose,
            $"Tax×Mobility: α net migration outflow under pressure " +
            $"(netΣ {Months.Sum(m => m.Alpha.NetMigration).ToString("0", inv)}; " +
            $"peak push {Months.Max(m => m.Alpha.EmigrationPressure).ToString("0.00", inv)}; " +
            $"geo migrated {model.Telemetry.PopulationMigrated.ToString("0", inv)})"));

        var gammaGained = Months.Count >= 2 && Months[^1].Gamma.Population > Months[0].Gamma.Population;
        checks.Add((gammaGained || model.Telemetry.PopulationMigrated > 0,
            "Geo×Civics: spatial population moved (γ gained or telemetry migrated > 0)"));

        foreach (var (ok, text) in checks)
            Console.WriteLine($"  {(ok ? "PASS" : "FAIL")}  {text}");

        var pass = checks.Count(c => c.ok);
        Console.WriteLine($"  Result: {pass}/{checks.Count} coupled checks passed");
        Console.WriteLine();
    }

    static double Control(WorldState world, PolityId id)
    {
        var home = world.Provinces.Count(p => p.HomePolityId == id);
        return home == 0 ? 1 : world.CountOwnedProvinces(id) / (double)home;
    }

    static string Tag(PolityId id) => id.Value switch { 0 => "α", 1 => "β", _ => "γ" };
}

readonly record struct Milestone(int MonthIndex, string Layer, string What);

sealed class NationMonthFacts
{
    public double StateCash { get; init; }
    public double TaxCollected { get; init; }
    public double Transfers { get; init; }
    public double Wages { get; init; }
    public double WidgetsProduced { get; init; }
    public double ProductionValue { get; init; }
    public double OreStock { get; init; }
    public double CivicLastTax { get; init; }
    public double CivicLastXfer { get; init; }
    public double Legitimacy { get; init; }
    public double Approval { get; init; }
    public double WarFatigue { get; init; }
    public double HumanDevelopment { get; init; }
    public double Gdp { get; init; }
    public double ForceDemand { get; init; }
    public double ForceTotal { get; init; }
    public double GeoShortage { get; init; }
    public double ControlRatio { get; init; }
    public double Tech { get; init; }
    public double HouseholdTaxRate { get; init; }
    public double MilitaryShare { get; init; }
    public double Population { get; init; }
    public double EmigrationPressure { get; init; }
    public double NetMigration { get; init; }
}

sealed class MonthSample
{
    public required int Month { get; init; }
    public required string Phase { get; init; }
    public required bool AtWar { get; init; }
    public required int Battles { get; init; }
    public required double TradeDelta { get; init; }
    public required NationMonthFacts Alpha { get; init; }
    public required NationMonthFacts Beta { get; init; }
    public required NationMonthFacts Gamma { get; init; }
}

static class MonthSampleFactory
{
    public static NationMonthFacts FromEconomyPolity(
        Polity polity,
        EconomyState eco,
        LegalEntityId state,
        LegalEntityId firm,
        RegionId region,
        decimal widgetsDelta,
        double productionValue,
        double forceDemand,
        double shortage,
        double control,
        double population,
        double emigrationPressure,
        double netMigration)
    {
        return new NationMonthFacts
        {
            StateCash = (double)eco.Entities[state].Cash.Amount,
            TaxCollected = (double)eco.Flows.TaxCollected.Amount,
            Transfers = (double)eco.Flows.TransfersPaid.Amount,
            Wages = (double)eco.Flows.WagesAccrued.Amount,
            WidgetsProduced = (double)widgetsDelta,
            ProductionValue = productionValue,
            OreStock = (double)HoldingLedger.GetQuantity(eco, firm, region, TriadWorld.OreId),
            CivicLastTax = polity.Civic.LastTaxCollected,
            CivicLastXfer = polity.Civic.LastTransfersPaid,
            Legitimacy = polity.Civic.Legitimacy,
            Approval = polity.Civic.Approval,
            WarFatigue = polity.Civic.WarFatigue,
            HumanDevelopment = polity.Civic.HumanDevelopment,
            Gdp = polity.Gdp,
            ForceDemand = forceDemand,
            ForceTotal = polity.Military.Total,
            GeoShortage = shortage,
            ControlRatio = control,
            Tech = polity.TechLevel,
            HouseholdTaxRate = polity.Policy.HouseholdTaxRate,
            MilitaryShare = polity.Policy.MilitaryShare,
            Population = population,
            EmigrationPressure = emigrationPressure,
            NetMigration = netMigration,
        };
    }

    public static NationMonthFacts FromGeoOnly(
        Polity polity, double shortage, double control,
        double population, double emigrationPressure, double netMigration) => new()
    {
        StateCash = polity.Treasury,
        TaxCollected = polity.Civic.LastTaxCollected,
        Transfers = polity.Civic.LastTransfersPaid,
        Legitimacy = polity.Civic.Legitimacy,
        Approval = polity.Civic.Approval,
        WarFatigue = polity.Civic.WarFatigue,
        HumanDevelopment = polity.Civic.HumanDevelopment,
        Gdp = polity.Gdp,
        ForceTotal = polity.Military.Total,
        GeoShortage = shortage,
        ControlRatio = control,
        Tech = polity.TechLevel,
        HouseholdTaxRate = polity.Policy.HouseholdTaxRate,
        MilitaryShare = polity.Policy.MilitaryShare,
        Population = population,
        EmigrationPressure = emigrationPressure,
        NetMigration = netMigration,
    };
}
