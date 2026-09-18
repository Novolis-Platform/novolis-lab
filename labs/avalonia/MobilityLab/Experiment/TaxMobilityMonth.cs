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

namespace MobilityLab.Experiment;

/// <summary>
/// Month order: agents → trade → Economy → Civics delivery → Gamma geo civic →
/// PopulationMigration → optional conflict → sample.
/// </summary>
static class TaxMobilityMonth
{
    public const double PeoplePerHousehold = 250_000;

    public static void Advance(TaxMobilityWorld.Model model, Queue<string> log)
    {
        var world = model.World;
        var telemetry = model.Telemetry;
        model.BattlesThisMonth = 0;

        if (model.AgentsEnabled)
        {
            SyncNationTreasuryHint(model.AlphaNation, model.AlphaEconomy, TaxMobilityWorld.AlphaState);
            SyncNationTreasuryHint(model.BetaNation, model.BetaEconomy, TaxMobilityWorld.BetaState);
            model.AlphaNation.Demography.LastEmigrationPressure =
                world.Polity(new PolityId(0)).Civic.EmigrationPressure;
            model.BetaNation.Demography.LastEmigrationPressure =
                world.Polity(new PolityId(1)).Civic.EmigrationPressure;
            model.FiscalAgent.AdjustPolicy(model.AlphaNation);
            model.FiscalAgent.AdjustPolicy(model.BetaNation);
        }

        // Identification: treatment taxes fixed after any agent intent
        TaxMobilityWorld.LockTreatmentTaxes(model);

        var tradeBefore = telemetry.CommonMarketVolume + telemetry.WorldMarketVolume;
        TradeClearing.RunMonth(world, telemetry);
        var tradeDelta = (telemetry.CommonMarketVolume + telemetry.WorldMarketVolume) - tradeBefore;

        if (world.AreAtWar(new PolityId(0), new PolityId(1)))
        {
            model.AlphaEconomy = HoldingLedger.Upsert(
                model.AlphaEconomy, TaxMobilityWorld.AlphaFirm, TaxMobilityWorld.RegionA, TaxMobilityWorld.OreId,
                Math.Max(0m, HoldingLedger.GetQuantity(
                    model.AlphaEconomy, TaxMobilityWorld.AlphaFirm, TaxMobilityWorld.RegionA, TaxMobilityWorld.OreId) - 6m));
        }
        else
        {
            model.AlphaEconomy = HoldingLedger.Upsert(
                model.AlphaEconomy, TaxMobilityWorld.AlphaFirm, TaxMobilityWorld.RegionA, TaxMobilityWorld.OreId,
                HoldingLedger.GetQuantity(
                    model.AlphaEconomy, TaxMobilityWorld.AlphaFirm, TaxMobilityWorld.RegionA, TaxMobilityWorld.OreId) + 5m);
            model.BetaEconomy = HoldingLedger.Upsert(
                model.BetaEconomy, TaxMobilityWorld.BetaFirm, TaxMobilityWorld.RegionB, TaxMobilityWorld.OreId,
                HoldingLedger.GetQuantity(
                    model.BetaEconomy, TaxMobilityWorld.BetaFirm, TaxMobilityWorld.RegionB, TaxMobilityWorld.OreId) + 5m);
        }

        SyncDemographyFromWorld(model.AlphaNation, world, new PolityId(0));
        SyncDemographyFromWorld(model.BetaNation, world, new PolityId(1));

        model.AlphaEconomy = SettleNation(
            model, model.AlphaNation, model.AlphaEconomy,
            TaxMobilityWorld.AlphaState, TaxMobilityWorld.AlphaFirm, TaxMobilityWorld.RegionA,
            world.Polity(new PolityId(0)), log, "α", out var alphaStats);
        model.BetaEconomy = SettleNation(
            model, model.BetaNation, model.BetaEconomy,
            TaxMobilityWorld.BetaState, TaxMobilityWorld.BetaFirm, TaxMobilityWorld.RegionB,
            world.Polity(new PolityId(1)), log, "β", out var betaStats);

        var gamma = world.Polity(new PolityId(2));
        var gFacts = BuildGeoFacts(world, gamma.Id);
        GeoCivic.ApplyMonth(gamma, new GeoCivic.MonthContext
        {
            ControlRatio = gFacts.ControlRatio,
            ActiveWars = gFacts.ActiveWars,
            ResourceShortage = gFacts.ResourceShortage,
            OccupyingForeignLand = gFacts.OccupyingForeignLand,
            LostHomeProvinces = gFacts.LostHomeTerritory,
            ResearchMultiplier = 1.0,
            NetMigration = gamma.Civic.LastNetMigration,
        }, world);

        var popBeforeA = world.OwnedPopulation(new PolityId(0));
        PopulationMigration.RunMonth(world, telemetry);
        var popDeltaA = world.OwnedPopulation(new PolityId(0)) - popBeforeA;
        if (Math.Abs(popDeltaA) > 500)
            Note(log, $"α population Δ {popDeltaA:0} (geo migration)");

        model.AlphaEconomy = SyncCohortsFromPopulation(
            model.AlphaEconomy, TaxMobilityWorld.RegionA, world.OwnedPopulation(new PolityId(0)));
        model.BetaEconomy = SyncCohortsFromPopulation(
            model.BetaEconomy, TaxMobilityWorld.RegionB, world.OwnedPopulation(new PolityId(1)));

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
                    Note(log, $"BATTLE {world.Polity(war.Attacker).Name} captured province");
                    break;
                }
            }
        }

        world.Day += WorldState.DaysPerMonth;
        var month = world.Day / WorldState.DaysPerMonth;
        var alpha = world.Polity(new PolityId(0));
        var beta = world.Polity(new PolityId(1));
        model.Phase = world.AreAtWar(alpha.Id, beta.Id) ? "war-shock" : "baseline";

        model.History.Record(new MonthSample
        {
            Month = month,
            Phase = model.Phase,
            AtWar = world.AreAtWar(alpha.Id, beta.Id),
            TradeDelta = tradeDelta,
            Alpha = SamplePolity(
                alpha, model.AlphaEconomy, TaxMobilityWorld.AlphaState, TaxMobilityWorld.AlphaFirm,
                TaxMobilityWorld.RegionA, alphaStats.ProductionValue,
                world.OwnedPopulation(alpha.Id), alpha.Civic.EmigrationPressure, alpha.Civic.LastNetMigration,
                world.PopWeightedControlRatio(alpha.Id)),
            Beta = SamplePolity(
                beta, model.BetaEconomy, TaxMobilityWorld.BetaState, TaxMobilityWorld.BetaFirm,
                TaxMobilityWorld.RegionB, betaStats.ProductionValue,
                world.OwnedPopulation(beta.Id), beta.Civic.EmigrationPressure, beta.Civic.LastNetMigration,
                world.PopWeightedControlRatio(beta.Id)),
            Gamma = SampleGeo(gamma, world.OwnedPopulation(gamma.Id), world.PopWeightedControlRatio(gamma.Id)),
        });

        Note(log,
            $"M{month} [{model.Phase}] α pop {world.OwnedPopulation(alpha.Id):0} " +
            $"push {alpha.Civic.EmigrationPressure:0.00} L {alpha.Civic.Legitimacy:0.00} · " +
            $"β pop {world.OwnedPopulation(beta.Id):0} push {beta.Civic.EmigrationPressure:0.00}");
    }

    public static void MaybeApplyWarShock(TaxMobilityWorld.Model model, Queue<string> log, int monthIndex)
    {
        if (!model.Spec.WarShockOn || monthIndex != 8)
            return;
        if (model.World.AreAtWar(new PolityId(0), new PolityId(1)))
            return;

        model.World.Relations.Set(new PolityId(0), new PolityId(1), -55);
        var war = DiplomaticInstruments.DeclareWar(
            model.World, model.Telemetry, new PolityId(0), new PolityId(1));
        if (war is null)
        {
            Note(log, "War shock refused.");
            return;
        }

        Note(log, "WAR SHOCK — Alpha vs Beta (confounder on).");
        model.Phase = "war-shock";
    }

    static PolityFacts SamplePolity(
        Polity polity,
        EconomyState eco,
        LegalEntityId state,
        LegalEntityId firm,
        RegionId region,
        double productionValue,
        double population,
        double emigrationPressure,
        double netMigration,
        double control) => new()
    {
        Population = population,
        NetMigration = netMigration,
        EmigrationPressure = emigrationPressure,
        Legitimacy = polity.Civic.Legitimacy,
        Approval = polity.Civic.Approval,
        HouseholdTaxRate = polity.Policy.HouseholdTaxRate,
        ProductionValue = productionValue,
        ControlRatio = control,
        TaxCollected = (double)eco.Flows.TaxCollected.Amount,
        StateCash = (double)eco.Entities[state].Cash.Amount,
        OreStock = (double)HoldingLedger.GetQuantity(eco, firm, region, TaxMobilityWorld.OreId),
    };

    static PolityFacts SampleGeo(Polity polity, double population, double control) => new()
    {
        Population = population,
        NetMigration = polity.Civic.LastNetMigration,
        EmigrationPressure = polity.Civic.EmigrationPressure,
        Legitimacy = polity.Civic.Legitimacy,
        Approval = polity.Civic.Approval,
        HouseholdTaxRate = polity.Policy.HouseholdTaxRate,
        ControlRatio = control,
        TaxCollected = polity.Civic.LastTaxCollected,
        StateCash = polity.Treasury,
    };

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
        TaxMobilityWorld.Model model,
        NationState nation,
        EconomyState economy,
        LegalEntityId stateId,
        LegalEntityId firmId,
        RegionId regionId,
        Polity polity,
        Queue<string> log,
        string tag,
        out (double Tax, double ProductionValue) stats)
    {
        economy = economy with
        {
            Policy = CivicEconomyBridge.ToEconomyStatePolicy(
                nation.Policy,
                transferPerHousehold: economy.Policy.TransferPerHousehold,
                wagePerLaborHour: economy.Policy.WagePerLaborHour,
                firmTaxRate: economy.Policy.FirmTaxRate),
        };

        economy = model.Engine.Advance(economy);

        var facts = new PeriodContext
        {
            ControlRatio = model.World.PopWeightedControlRatio(polity.Id),
            ActiveWars = model.World.ActiveWars.Count(w => w.Attacker == polity.Id || w.Defender == polity.Id),
            ResourceShortage = ResourceKinds.All.Sum(k => polity.Balance[k] < 0 ? -polity.Balance[k] : 0),
            OccupyingForeignLand = model.World.Provinces.Any(p => p.OwnerId == polity.Id && p.HomePolityId != polity.Id),
            LostHomeTerritory = model.World.Provinces.Any(p => p.HomePolityId == polity.Id && p.OwnerId != polity.Id),
            ResearchMultiplier = 1.0,
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

        stats = ((double)economy.Flows.TaxCollected.Amount, (double)economy.Flows.ProductionOutputValue.Amount);
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
        while (log.Count > 40)
            log.Dequeue();
        log.Enqueue(line);
    }
}
