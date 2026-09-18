using Novolis.Civics.Core;
using Novolis.Civics.EconomyBridge;
using Novolis.Economy.Core;
using Novolis.Economy.Core.Holdings;
using Novolis.Geopolitics.Core;
using Novolis.Geopolitics.Diplomacy;
using Novolis.Geopolitics.Trade;
using CivicsEngine = Novolis.Civics.Core.CivicEngine;
using CivicsGov = Novolis.Civics.Core.GovernmentType;
using CivicsGovRules = Novolis.Civics.Core.GovernmentRules;
using GeoCivic = Novolis.Geopolitics.Core.CivicEngine;

namespace PolityTriad;

/// <summary>
/// Composed month:
/// agents → trade → Economy periods → Civics delivery → Gamma geo civic →
/// PopulationMigration → treaty effects → conflict → history sample.
/// </summary>
static class MonthTick
{
    public const double PeoplePerHousehold = 250_000; // theatre scale: cohort count ↔ provincial people

    public static void Advance(TriadWorld.Model model, Queue<string> log)
    {
        var world = model.World;
        var telemetry = model.Telemetry;
        model.BattlesThisMonth = 0;

        if (model.AgentsEnabled)
        {
            SyncNationTreasuryHint(model.AlphaNation, model.AlphaEconomy, TriadWorld.AlphaState);
            SyncNationTreasuryHint(model.BetaNation, model.BetaEconomy, TriadWorld.BetaState);
            // Refresh emigration pressure hint for agent from last civic settle
            model.AlphaNation.Demography.LastEmigrationPressure =
                world.Polity(new PolityId(0)).Civic.EmigrationPressure;
            model.BetaNation.Demography.LastEmigrationPressure =
                world.Polity(new PolityId(1)).Civic.EmigrationPressure;
            var aTax0 = model.AlphaNation.Policy.HouseholdTaxRate;
            var bMil0 = model.BetaNation.Policy.MilitaryShare;
            model.FiscalAgent.AdjustPolicy(model.AlphaNation);
            model.FiscalAgent.AdjustPolicy(model.BetaNation);
            if (Math.Abs(model.AlphaNation.Policy.HouseholdTaxRate - aTax0) > 1e-9)
            {
                var msg = $"α tax intent {aTax0:0.00}→{model.AlphaNation.Policy.HouseholdTaxRate:0.00}";
                Note(log, msg);
                model.History.Mark("civics-agent", msg);
            }

            if (Math.Abs(model.BetaNation.Policy.MilitaryShare - bMil0) > 1e-9)
            {
                var msg = $"β mil share {bMil0:0.00}→{model.BetaNation.Policy.MilitaryShare:0.00}";
                Note(log, msg);
                model.History.Mark("civics-agent", msg);
            }
        }

        var tradeBefore = telemetry.CommonMarketVolume + telemetry.WorldMarketVolume;
        TradeClearing.RunMonth(world, telemetry);
        var tradeDelta = (telemetry.CommonMarketVolume + telemetry.WorldMarketVolume) - tradeBefore;

        if (world.AreAtWar(new PolityId(0), new PolityId(1)))
        {
            model.AlphaEconomy = HoldingLedger.Upsert(
                model.AlphaEconomy, TriadWorld.AlphaFirm, TriadWorld.RegionA, TriadWorld.OreId,
                Math.Max(0m, HoldingLedger.GetQuantity(
                    model.AlphaEconomy, TriadWorld.AlphaFirm, TriadWorld.RegionA, TriadWorld.OreId) - 6m));
        }
        else
        {
            model.AlphaEconomy = HoldingLedger.Upsert(
                model.AlphaEconomy, TriadWorld.AlphaFirm, TriadWorld.RegionA, TriadWorld.OreId,
                HoldingLedger.GetQuantity(
                    model.AlphaEconomy, TriadWorld.AlphaFirm, TriadWorld.RegionA, TriadWorld.OreId) + 5m);
            model.BetaEconomy = HoldingLedger.Upsert(
                model.BetaEconomy, TriadWorld.BetaFirm, TriadWorld.RegionB, TriadWorld.OreId,
                HoldingLedger.GetQuantity(
                    model.BetaEconomy, TriadWorld.BetaFirm, TriadWorld.RegionB, TriadWorld.OreId) + 4m);
        }

        SyncDemographyFromWorld(model.AlphaNation, world, new PolityId(0));
        SyncDemographyFromWorld(model.BetaNation, world, new PolityId(1));

        model.AlphaEconomy = SettleNation(
            model, model.AlphaNation, model.AlphaEconomy,
            TriadWorld.AlphaState, TriadWorld.AlphaFirm, TriadWorld.RegionA,
            world.Polity(new PolityId(0)), log, "α", out var alphaStats);
        model.BetaEconomy = SettleNation(
            model, model.BetaNation, model.BetaEconomy,
            TriadWorld.BetaState, TriadWorld.BetaFirm, TriadWorld.RegionB,
            world.Polity(new PolityId(1)), log, "β", out var betaStats);

        var gamma = world.Polity(new PolityId(2));
        var gFacts = BuildGeoFacts(world, gamma.Id);
        var gammaResearch = world.TreatiesContaining(gamma.Id, TreatyKind.ResearchPartnership).Any() ? 1.15 : 1.0;
        GeoCivic.ApplyMonth(gamma, new GeoCivic.MonthContext
        {
            ControlRatio = gFacts.ControlRatio,
            ActiveWars = gFacts.ActiveWars,
            ResourceShortage = gFacts.ResourceShortage,
            OccupyingForeignLand = gFacts.OccupyingForeignLand,
            LostHomeProvinces = gFacts.LostHomeTerritory,
            ResearchMultiplier = gammaResearch,
            NetMigration = gamma.Civic.LastNetMigration,
        }, world);

        var popBeforeA = world.OwnedPopulation(new PolityId(0));
        PopulationMigration.RunMonth(world, telemetry);
        var popDeltaA = world.OwnedPopulation(new PolityId(0)) - popBeforeA;
        if (Math.Abs(popDeltaA) > 500)
        {
            var msg = $"α population Δ {popDeltaA:0} (geo migration)";
            Note(log, msg);
            model.History.Mark("population", msg);
        }

        model.AlphaEconomy = SyncCohortsFromPopulation(model.AlphaEconomy, TriadWorld.RegionA, world.OwnedPopulation(new PolityId(0)));
        model.BetaEconomy = SyncCohortsFromPopulation(model.BetaEconomy, TriadWorld.RegionB, world.OwnedPopulation(new PolityId(1)));

        TreatyEffects.RunMonth(world, telemetry);

        if (world.AreAtWar(new PolityId(0), new PolityId(1)))
        {
            foreach (var war in world.ActiveWars.Where(w =>
                         (w.Attacker.Value == 0 && w.Defender.Value == 1) ||
                         (w.Attacker.Value == 1 && w.Defender.Value == 0)).ToList())
            {
                for (var attempt = 0; attempt < 3; attempt++)
                {
                    if (!model.Conflict.TryResolveFront(world, war))
                        continue;
                    model.BattlesThisMonth++;
                    model.CapturesTotal++;
                    telemetry.ProvincesCaptured++;
                    var msg =
                        $"{world.Polity(war.Attacker).Name} captured province " +
                        $"(attacker taken tally {war.ProvincesTakenByAttacker})";
                    Note(log, $"BATTLE {msg}");
                    model.History.Mark("conflict", msg);
                    break;
                }

                if (model.BattlesThisMonth == 0)
                    Note(log, "Front stalled — casualties only");
            }
        }

        world.Day += WorldState.DaysPerMonth;
        var month = world.Day / WorldState.DaysPerMonth;
        var alpha = world.Polity(new PolityId(0));
        var beta = world.Polity(new PolityId(1));
        model.Phase = DescribePhase(world, alpha, beta);

        var aShortage = ResourceKinds.All.Sum(k => alpha.Balance[k] < 0 ? -alpha.Balance[k] : 0);
        var bShortage = ResourceKinds.All.Sum(k => beta.Balance[k] < 0 ? -beta.Balance[k] : 0);
        var gShortage = ResourceKinds.All.Sum(k => gamma.Balance[k] < 0 ? -gamma.Balance[k] : 0);

        model.History.Record(new MonthSample
        {
            Month = month,
            Phase = model.Phase,
            AtWar = world.AreAtWar(alpha.Id, beta.Id),
            Battles = model.BattlesThisMonth,
            TradeDelta = tradeDelta,
            Alpha = MonthSampleFactory.FromEconomyPolity(
                alpha, model.AlphaEconomy, TriadWorld.AlphaState, TriadWorld.AlphaFirm, TriadWorld.RegionA,
                alphaStats.Widgets, alphaStats.ProductionValue, alphaStats.ForceDemand, aShortage,
                world.PopWeightedControlRatio(alpha.Id),
                world.OwnedPopulation(alpha.Id), alpha.Civic.EmigrationPressure, alpha.Civic.LastNetMigration),
            Beta = MonthSampleFactory.FromEconomyPolity(
                beta, model.BetaEconomy, TriadWorld.BetaState, TriadWorld.BetaFirm, TriadWorld.RegionB,
                betaStats.Widgets, betaStats.ProductionValue, betaStats.ForceDemand, bShortage,
                world.PopWeightedControlRatio(beta.Id),
                world.OwnedPopulation(beta.Id), beta.Civic.EmigrationPressure, beta.Civic.LastNetMigration),
            Gamma = MonthSampleFactory.FromGeoOnly(
                gamma, gShortage, world.PopWeightedControlRatio(gamma.Id),
                world.OwnedPopulation(gamma.Id), gamma.Civic.EmigrationPressure, gamma.Civic.LastNetMigration),
        });

        Note(log,
            $"M{month} [{model.Phase}] tradeΔ {tradeDelta:0.#} · " +
            $"α tax{alphaStats.Tax:0.#}/prod{alphaStats.ProductionValue:0.#}/WF{alpha.Civic.WarFatigue:0.00} · " +
            $"β WF{beta.Civic.WarFatigue:0.00} ctrl {Control(world, beta.Id):0.00}");
    }

    public static void DeclareWar(TriadWorld.Model model, Queue<string> log)
    {
        var a = new PolityId(0);
        var b = new PolityId(1);
        if (model.World.AreAtWar(a, b))
        {
            Note(log, "Already at war.");
            return;
        }

        model.World.Relations.Set(a, b, -55);
        var war = DiplomaticInstruments.DeclareWar(model.World, model.Telemetry, a, b);
        if (war is null)
        {
            Note(log, "War refused.");
            model.History.Mark("diplomacy", "DeclareWar refused");
            return;
        }

        Note(log, "WAR — Alpha vs Beta. Ore replenishment stops; ore drain on Alpha.");
        model.History.Mark("diplomacy", "War declared Alpha vs Beta (ore drain starts)");
        model.Phase = "war";
    }

    public static void OfferPeace(TriadWorld.Model model, Queue<string> log)
    {
        var a = new PolityId(0);
        var b = new PolityId(1);
        var war = model.World.ActiveWars.FirstOrDefault(w =>
            (w.Attacker == a && w.Defender == b) || (w.Attacker == b && w.Defender == a));
        if (war is null)
        {
            Note(log, "No active Alpha–Beta war.");
            return;
        }

        war.Active = false;
        DiplomaticInstruments.SignTreaty(model.World, model.Telemetry, TreatyKind.Peace, a, b, 360);
        model.World.Relations.Adjust(a, b, 15);
        model.Telemetry.WarsEnded++;
        Note(log, "PEACE signed — 360d grace; occupation may remain.");
        model.History.Mark("diplomacy", "Peace treaty Alpha–Beta (360d); occupation retained");
        model.Phase = "peace";
    }

    public static void SignCommonMarket(TriadWorld.Model model, Queue<string> log)
    {
        var a = new PolityId(0);
        var g = new PolityId(2);
        if (model.World.HaveTreaty(a, g, TreatyKind.CommonMarket))
        {
            Note(log, "Alpha–Gamma Common Market already active.");
            return;
        }

        model.World.Relations.Set(a, g, Math.Max(50, model.World.Relations.Get(a, g)));
        var t = DiplomaticInstruments.SignTreaty(
            model.World, model.Telemetry, TreatyKind.CommonMarket, a, g, 2000);
        if (t is null)
        {
            Note(log, "CM refused.");
            model.History.Mark("diplomacy", "Common Market refused");
            return;
        }

        Note(log, "TREATY — Alpha–Gamma Common Market.");
        model.History.Mark("diplomacy", "Common Market signed Alpha–Gamma");
    }

    public static void SignResearch(TriadWorld.Model model, Queue<string> log)
    {
        var a = new PolityId(0);
        var g = new PolityId(2);
        model.World.Relations.Set(a, g, Math.Max(55, model.World.Relations.Get(a, g)));
        var t = DiplomaticInstruments.SignTreaty(
            model.World, model.Telemetry, TreatyKind.ResearchPartnership, a, g, 1500);
        if (t is null)
        {
            Note(log, "Research treaty refused.");
            model.History.Mark("diplomacy", "Research Partnership refused");
            return;
        }

        Note(log, "TREATY — Research Partnership (α/γ research ×1.15).");
        model.History.Mark("diplomacy", "Research Partnership signed (research ×1.15)");
    }

    public static void RunScriptedArc(TriadWorld.Model model, Queue<string> log, int monthIndex)
    {
        switch (monthIndex)
        {
            case 1:
                model.AlphaNation.Policy.HouseholdTaxRate = 0.38;
                model.World.Polity(new PolityId(0)).Policy.HouseholdTaxRate = 0.38;
                model.History.Mark("policy", "Alpha household tax spiked to 0.38 (mobility pressure)");
                Note(log, "POLICY — Alpha tax spike 0.38");
                break;
            case 2:
                SignCommonMarket(model, log);
                break;
            case 4:
                SignResearch(model, log);
                break;
            case 8:
                DeclareWar(model, log);
                model.World.Polity(new PolityId(0)).Military.Land += 400;
                model.World.Polity(new PolityId(0)).Military.Air += 120;
                model.History.Mark("conflict", "Alpha force reinforced (+400 land / +120 air) for offensive");
                break;
            case 18 when model.World.AreAtWar(new PolityId(0), new PolityId(1)):
                OfferPeace(model, log);
                break;
        }
    }

    static void SyncDemographyFromWorld(NationState nation, WorldState world, PolityId id)
    {
        nation.Demography.Population = world.OwnedPopulation(id);
        nation.Demography.LastNetMigration = world.Polity(id).Civic.LastNetMigration;
        nation.Demography.LastEmigrationPressure = world.Polity(id).Civic.EmigrationPressure;
    }

    static EconomyState SyncCohortsFromPopulation(EconomyState economy, RegionId region, double population)
    {
        var targetHouseholds = Math.Max(1, (int)Math.Round(population / PeoplePerHousehold));
        var cohorts = new Dictionary<CohortId, HouseholdCohort>(economy.Cohorts);
        var regional = cohorts.Values.Where(c => c.RegionId.Equals(region)).ToList();
        if (regional.Count == 0)
            return economy;
        var primary = regional[0];
        cohorts[primary.Id] = primary with { HouseholdCount = targetHouseholds };
        return economy with { Cohorts = cohorts };
    }

    static EconomyState SettleNation(
        TriadWorld.Model model,
        NationState nation,
        EconomyState economy,
        LegalEntityId stateId,
        LegalEntityId firmId,
        RegionId regionId,
        Polity polity,
        Queue<string> log,
        string tag,
        out (decimal Widgets, double Tax, double ForceDemand, double ProductionValue) stats)
    {
        economy = economy with
        {
            Policy = CivicEconomyBridge.ToEconomyStatePolicy(
                nation.Policy,
                transferPerHousehold: economy.Policy.TransferPerHousehold,
                wagePerLaborHour: economy.Policy.WagePerLaborHour,
                firmTaxRate: economy.Policy.FirmTaxRate),
        };

        var beforeWidgets = HoldingLedger.GetQuantity(economy, firmId, regionId, TriadWorld.WidgetId);
        economy = model.Engine.Advance(economy);
        var produced = HoldingLedger.GetQuantity(economy, firmId, regionId, TriadWorld.WidgetId) - beforeWidgets;

        var facts = new PeriodContext
        {
            ControlRatio = model.World.PopWeightedControlRatio(polity.Id),
            ActiveWars = model.World.ActiveWars.Count(w => w.Attacker == polity.Id || w.Defender == polity.Id),
            ResourceShortage = ResourceKinds.All.Sum(k => polity.Balance[k] < 0 ? -polity.Balance[k] : 0),
            OccupyingForeignLand = model.World.Provinces.Any(p => p.OwnerId == polity.Id && p.HomePolityId != polity.Id),
            LostHomeTerritory = model.World.Provinces.Any(p => p.HomePolityId == polity.Id && p.OwnerId != polity.Id),
            ResearchMultiplier = tag == "α" && model.World.TreatiesContaining(polity.Id, TreatyKind.ResearchPartnership).Any()
                ? 1.15
                : 1.0,
            NetMigration = polity.Civic.LastNetMigration,
        };

        var period = CivicEconomyBridge.PeriodContextFromDelivery(
            (double)economy.Flows.TaxCollected.Amount,
            (double)economy.Flows.TransfersPaid.Amount,
            facts);

        var outcome = CivicsEngine.ApplyPeriod(nation, period);
        SyncPolityFromNation(polity, nation, economy, stateId);
        ApplyForceGrowth(polity, outcome.ForceCapabilityDemand);
        polity.Civic.EmigrationPressure = outcome.EmigrationPressure;
        polity.Civic.ImmigrationAttractiveness = outcome.ImmigrationAttractiveness;
        nation.Demography.LastEmigrationPressure = outcome.EmigrationPressure;

        stats = (
            produced,
            (double)economy.Flows.TaxCollected.Amount,
            outcome.ForceCapabilityDemand,
            (double)economy.Flows.ProductionOutputValue.Amount);

        if (produced != 0 || economy.Flows.WagesAccrued.Amount > 0 || economy.Flows.ProductionOutputValue.Amount > 0)
        {
            Note(log,
                $"{tag} eco: tax {economy.Flows.TaxCollected.Amount:0.#} xfer {economy.Flows.TransfersPaid.Amount:0.#} " +
                $"wages {economy.Flows.WagesAccrued.Amount:0.#} prodVal {economy.Flows.ProductionOutputValue.Amount:0.#} " +
                $"Δwidgets {produced:0.#} force+{outcome.ForceCapabilityDemand:0.##}");
        }

        return economy;
    }

    static PeriodContext BuildGeoFacts(WorldState world, PolityId id)
    {
        var owned = world.CountOwnedProvinces(id);
        var home = world.Provinces.Count(p => p.HomePolityId == id);
        var polity = world.Polity(id);
        return new PeriodContext
        {
            ControlRatio = home == 0 ? 1.0 : owned / (double)home,
            ActiveWars = world.ActiveWars.Count(w => w.Attacker == id || w.Defender == id),
            ResourceShortage = ResourceKinds.All.Sum(k => polity.Balance[k] < 0 ? -polity.Balance[k] : 0),
            OccupyingForeignLand = world.Provinces.Any(p => p.OwnerId == id && p.HomePolityId != id),
            LostHomeTerritory = world.Provinces.Any(p => p.HomePolityId == id && p.OwnerId != id),
            ResearchMultiplier = 1.0,
        };
    }

    static double Control(WorldState world, PolityId id) => world.PopWeightedControlRatio(id);

    static string DescribePhase(WorldState world, Polity alpha, Polity beta)
    {
        if (world.AreAtWar(alpha.Id, beta.Id))
            return alpha.Civic.WarFatigue > 0.45 ? "war-fatigue" : "war";
        if (world.Provinces.Any(p => p.OwnerId != p.HomePolityId))
            return "occupation";
        if (world.CountActiveTreatiesOfKind(TreatyKind.CommonMarket) > 0)
            return "integration";
        return "peace";
    }

    static void SyncNationTreasuryHint(NationState nation, EconomyState economy, LegalEntityId stateId) =>
        nation.Treasury = (double)economy.Entities[stateId].Cash.Amount;

    static void SyncPolityFromNation(Polity polity, NationState nation, EconomyState economy, LegalEntityId stateId)
    {
        polity.Gdp = nation.Gdp;
        polity.Treasury = (double)economy.Entities[stateId].Cash.Amount;
        polity.Stability = nation.Stability;
        polity.TechLevel = nation.TechnologyStock;
        polity.TechProgress = nation.TechnologyProgress;
        polity.Civic.Legitimacy = nation.Civic.Legitimacy;
        polity.Civic.Approval = nation.Civic.Approval;
        polity.Civic.Corruption = nation.Civic.Corruption;
        polity.Civic.HumanDevelopment = nation.Civic.HumanDevelopment;
        polity.Civic.WarFatigue = nation.Civic.WarFatigue;
        polity.Civic.LastTaxCollected = nation.Civic.LastTaxCollected;
        polity.Civic.LastTransfersPaid = nation.Civic.LastTransfersPaid;
        polity.Civic.EmigrationPressure = nation.Demography.LastEmigrationPressure;
        polity.TaxRate = Math.Clamp(nation.Policy.HouseholdTaxRate, 0, 0.6);
        polity.MilitaryBudgetShare = Math.Clamp(nation.Policy.MilitaryShare, 0, 0.7);
        polity.Policy.HouseholdTaxRate = nation.Policy.HouseholdTaxRate;
        polity.Policy.TransferShare = nation.Policy.TransferShare;
        polity.Policy.InfrastructureShare = nation.Policy.InfrastructureShare;
        polity.Policy.PropagandaShare = nation.Policy.PropagandaShare;
        polity.Policy.MilitaryShare = nation.Policy.MilitaryShare;
    }

    static void ApplyForceGrowth(Polity polity, double demand)
    {
        var upkeep = CivicsGovRules.MilitaryUpkeepFactor((CivicsGov)(int)polity.Government);
        polity.Military.Land += demand * 0.55;
        polity.Military.Air += demand * 0.25;
        polity.Military.Naval += demand * 0.20;
        polity.Military.Scale(1.0 - 0.008 * upkeep);
    }

    static void Note(Queue<string> log, string line)
    {
        while (log.Count > 18)
            log.Dequeue();
        log.Enqueue(line);
    }
}
