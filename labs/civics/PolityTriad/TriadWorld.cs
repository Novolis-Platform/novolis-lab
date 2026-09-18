using Novolis.Civics.Agents;
using Novolis.Civics.Core;
using Novolis.Civics.EconomyBridge;
using Novolis.Economy.Core;
using Novolis.Economy.Core.Holdings;
using Novolis.Economy.Core.Steps;
using Novolis.Geopolitics.Conflict;
using Novolis.Geopolitics.Core;
using CivicsGov = Novolis.Civics.Core.GovernmentType;
using EcoResourceKind = Novolis.Economy.Core.ResourceKind;
using GeoGov = Novolis.Geopolitics.Core.GovernmentType;
using GeoResourceKind = Novolis.Geopolitics.Core.ResourceKind;
using CoreMoney = Novolis.Economy.Core.Money;
using CoreEntity = Novolis.Economy.Core.LegalEntity;
using CoreEntityKind = Novolis.Economy.Core.LegalEntityKind;

namespace PolityTriad;

/// <summary>
/// Three-polity frontier: Alpha (Democracy, full Economy→Civics), Beta (Autocracy, Economy→Civics),
/// Gamma (Multiparty, geo civic only + Common Market with Alpha).
/// </summary>
static class TriadWorld
{
    public static readonly RegionId RegionA = RegionId.From(Guid.Parse("c1000000-0000-0000-0000-000000000001"));
    public static readonly RegionId RegionB = RegionId.From(Guid.Parse("c1000000-0000-0000-0000-000000000002"));

    public static readonly LegalEntityId AlphaFirm = LegalEntityId.From(Guid.Parse("c2000000-0000-0000-0000-000000000011"));
    public static readonly LegalEntityId AlphaHh = LegalEntityId.From(Guid.Parse("c2000000-0000-0000-0000-000000000012"));
    public static readonly LegalEntityId AlphaState = LegalEntityId.From(Guid.Parse("c2000000-0000-0000-0000-000000000013"));
    public static readonly LegalEntityId BetaFirm = LegalEntityId.From(Guid.Parse("c2000000-0000-0000-0000-000000000021"));
    public static readonly LegalEntityId BetaHh = LegalEntityId.From(Guid.Parse("c2000000-0000-0000-0000-000000000022"));
    public static readonly LegalEntityId BetaState = LegalEntityId.From(Guid.Parse("c2000000-0000-0000-0000-000000000023"));

    public static readonly ResourceId OreId = ResourceId.From(Guid.Parse("c3000000-0000-0000-0000-000000000001"));
    public static readonly ResourceId WidgetId = ResourceId.From(Guid.Parse("c3000000-0000-0000-0000-000000000002"));
    public static readonly ActivityId AlphaAct = ActivityId.From(Guid.Parse("c4000000-0000-0000-0000-000000000001"));
    public static readonly ActivityId BetaAct = ActivityId.From(Guid.Parse("c4000000-0000-0000-0000-000000000002"));

    public sealed class Model
    {
        public required WorldState World { get; init; }
        public required NationState AlphaNation { get; init; }
        public required NationState BetaNation { get; init; }
        public required EconomyState AlphaEconomy { get; set; }
        public required EconomyState BetaEconomy { get; set; }
        public required EconomyEngine Engine { get; init; }
        public required WorldTelemetry Telemetry { get; init; }
        public required ConflictResolver Conflict { get; init; }
        public required HeuristicFiscalAgent FiscalAgent { get; init; }
        public required TriadHistory History { get; init; }
        public bool AgentsEnabled { get; set; } = true;
        public int BattlesThisMonth { get; set; }
        public int CapturesTotal { get; set; }
        public string Phase { get; set; } = "peace";
    }

    public static Model Create(int seed = 42)
    {
        var world = TheatreWorld();
        var alpha = world.Polity(new PolityId(0));
        var beta = world.Polity(new PolityId(1));
        var alphaNation = ToNation(alpha, "a");
        var betaNation = ToNation(beta, "b");
        alphaNation.Demography.Population = wOwned(world, 0);
        betaNation.Demography.Population = wOwned(world, 1);
        var alphaEco = OpenIndustrial(alphaNation, AlphaFirm, AlphaHh, AlphaState, AlphaAct, RegionA, seedCash: true);
        var betaEco = OpenIndustrial(betaNation, BetaFirm, BetaHh, BetaState, BetaAct, RegionB, seedCash: true);
        CivicEconomyBridge.BindEconomyState(alphaNation, AlphaState);
        CivicEconomyBridge.BindEconomyState(betaNation, BetaState);

        return new Model
        {
            World = world,
            AlphaNation = alphaNation,
            BetaNation = betaNation,
            AlphaEconomy = alphaEco,
            BetaEconomy = betaEco,
            Engine = DefaultPeriodPipeline.CreateEngine(),
            Telemetry = new WorldTelemetry(),
            Conflict = new ConflictResolver(new Random(seed)),
            FiscalAgent = new HeuristicFiscalAgent(),
            History = new TriadHistory(),
        };

        static double wOwned(WorldState w, int id) => w.OwnedPopulation(new PolityId(id));
    }

    static WorldState TheatreWorld()
    {
        var w = new WorldState { Seed = 42, SeedName = "polity-triad-theatre", Day = 0 };
        w.Polities.Add(MakePolity(0, "Alpha", GeoGov.Democracy, milShare: 0.28, land: 280, air: 70, naval: 55, tax: 0.26));
        w.Polities.Add(MakePolity(1, "Beta", GeoGov.Autocracy, milShare: 0.42, land: 320, air: 60, naval: 40, tax: 0.22));
        w.Polities.Add(MakePolity(2, "Gamma", GeoGov.Multiparty, milShare: 0.22, land: 150, air: 40, naval: 80, tax: 0.14));

        // Hex-ish strip: A0-A1-B0-B1 / G0-G1 with cross borders
        AddProv(w, 0, 0, coastal: true, pop: 1_400_000, 1, 2);
        AddProv(w, 1, 0, coastal: false, pop: 1_200_000, 0, 2, 4);
        AddProv(w, 2, 1, coastal: true, pop: 1_300_000, 0, 1, 3);
        AddProv(w, 3, 1, coastal: true, pop: 1_100_000, 2, 5);
        AddProv(w, 4, 2, coastal: false, pop: 1_500_000, 1, 5);
        AddProv(w, 5, 2, coastal: true, pop: 1_600_000, 3, 4);

        // Complementary resource weights → trade pressure
        SetWeights(w.Provinces[0], food: 0.55, materials: 0.1);
        SetWeights(w.Provinces[1], food: 0.15, materials: 0.45);
        SetWeights(w.Provinces[2], food: 0.1, materials: 0.5);
        SetWeights(w.Provinces[3], food: 0.4, materials: 0.15);
        SetWeights(w.Provinces[4], food: 0.35, materials: 0.2);
        SetWeights(w.Provinces[5], food: 0.25, materials: 0.25);

        w.Relations.Set(new PolityId(0), new PolityId(1), -20);
        w.Relations.Set(new PolityId(0), new PolityId(2), 55);
        w.Relations.Set(new PolityId(1), new PolityId(2), 10);
        return w;
    }

    static void AddProv(WorldState w, int id, int owner, bool coastal, double pop, params int[] neighbors)
    {
        w.Provinces.Add(new Province
        {
            Id = new ProvinceId(id),
            Name = owner switch { 0 => $"A{id}", 1 => $"B{id}", _ => $"G{id}" },
            OwnerId = new PolityId(owner),
            HomePolityId = new PolityId(owner),
            Population = pop,
            Wealth = 35_000 + id * 4_000,
            Coastal = coastal,
            Neighbors = neighbors.Select(n => new ProvinceId(n)).ToList(),
            ResourceWeights = ResourceVector.FromArray([0.3, 0.2, 0.15, 0.15, 0.1, 0.1]),
        });
    }

    static void SetWeights(Province p, double food, double materials)
    {
        var rest = Math.Max(0.05, 1.0 - food - materials) / 4.0;
        p.ResourceWeights[GeoResourceKind.Food] = food;
        p.ResourceWeights[GeoResourceKind.Materials] = materials;
        p.ResourceWeights[GeoResourceKind.Energy] = rest;
        p.ResourceWeights[GeoResourceKind.Goods] = rest;
        p.ResourceWeights[GeoResourceKind.MilitaryGoods] = rest;
        p.ResourceWeights[GeoResourceKind.Rare] = rest;
    }

    static Polity MakePolity(int id, string name, GeoGov gov, double milShare, double land, double air, double naval, double tax = 0.22) => new()
    {
        Id = new PolityId(id),
        Name = name,
        Continent = "Frontier",
        Government = gov,
        Gdp = 90_000 + id * 8_000,
        Treasury = 18_000,
        Stability = 0.72,
        TechLevel = 1.0 + id * 0.05,
        Policy =
        {
            HouseholdTaxRate = tax,
            TransferShare = 0.22 + (gov == GeoGov.Democracy ? 0.06 : 0),
            InfrastructureShare = 0.45,
            PropagandaShare = gov == GeoGov.Autocracy ? 0.35 : 0.18,
            MilitaryShare = milShare,
        },
        Civic =
        {
            Legitimacy = 0.62,
            Approval = 0.52,
            Corruption = gov == GeoGov.Autocracy ? 0.22 : 0.14,
            HumanDevelopment = gov == GeoGov.Multiparty ? 0.62 : 0.52,
            WarFatigue = 0,
            ImmigrationAttractiveness = gov == GeoGov.Multiparty ? 0.75 : 0.45,
        },
        Military = new MilitaryForce { Land = land, Air = air, Naval = naval },
    };

    static NationState ToNation(Polity p, string tag) => new()
    {
        Id = NationId.From(Guid.Parse($"a1000000-0000-0000-0000-0000000000{tag}{p.Id.Value}")),
        Name = p.Name,
        Government = (CivicsGov)(int)p.Government,
        Gdp = p.Gdp,
        Treasury = p.Treasury,
        Stability = p.Stability,
        TechnologyStock = p.TechLevel,
        TechnologyProgress = p.TechProgress,
        Policy =
        {
            HouseholdTaxRate = p.Policy.HouseholdTaxRate,
            TransferShare = p.Policy.TransferShare,
            InfrastructureShare = p.Policy.InfrastructureShare,
            PropagandaShare = p.Policy.PropagandaShare,
            MilitaryShare = p.Policy.MilitaryShare,
        },
        Civic =
        {
            Legitimacy = p.Civic.Legitimacy,
            Approval = p.Civic.Approval,
            Corruption = p.Civic.Corruption,
            HumanDevelopment = p.Civic.HumanDevelopment,
            WarFatigue = p.Civic.WarFatigue,
        },
    };

    static EconomyState OpenIndustrial(
        NationState nation,
        LegalEntityId firm,
        LegalEntityId hh,
        LegalEntityId state,
        ActivityId act,
        RegionId regionId,
        bool seedCash)
    {
        var region = new Region(regionId, LivingCapacity: 80, ProductionCapacity: 40m, LogisticsCapacity: 30m);
        var cohort = new HouseholdCohort(
            CohortId.New(), regionId, HouseholdCount: 5,
            new HouseholdProfile(ConsumptionWeight: 0.6m, 0.3m, 1m, MigrationPreference: 0.75m),
            HouseholdLaborKind.Common, CoreMoney.From(40m), hh);

        var recipe = new ActivityRecipe(
            [new ResourceAmount(OreId, 1m)],
            [new ResourceAmount(WidgetId, 1m)],
            LaborHoursPerRun: 2m,
            ProductionSpacePerRun: 1m);
        var activity = new Activity(act, firm, regionId, recipe, InstalledCapacity: 4m);

        var policy = CivicEconomyBridge.ToEconomyStatePolicy(
            nation.Policy,
            transferPerHousehold: CoreMoney.From(8m),
            wagePerLaborHour: CoreMoney.From(1.5m),
            firmTaxRate: 0.06m);

        var stateCash = seedCash ? CoreMoney.From(140m) : CoreMoney.From(80m);
        var eco = EconomyState.Empty with
        {
            Entities = new Dictionary<LegalEntityId, CoreEntity>
            {
                [hh] = new CoreEntity(hh, CoreEntityKind.Household, CoreMoney.From(90m)),
                [firm] = new CoreEntity(firm, CoreEntityKind.Firm, CoreMoney.From(110m)),
                [state] = new CoreEntity(state, CoreEntityKind.State, stateCash),
            },
            Regions = new Dictionary<RegionId, Region> { [regionId] = region },
            Cohorts = new Dictionary<CohortId, HouseholdCohort> { [cohort.Id] = cohort },
            Resources = new Dictionary<ResourceId, Novolis.Economy.Core.Resource>
            {
                [OreId] = new Novolis.Economy.Core.Resource(OreId, "Ore", Novolis.Economy.Core.ResourceKind.IntermediateGood),
                [WidgetId] = new Novolis.Economy.Core.Resource(WidgetId, "Widget", Novolis.Economy.Core.ResourceKind.ConsumerGood),
            },
            Activities = new Dictionary<ActivityId, Activity> { [act] = activity },
            PostedPrices = new Dictionary<string, PostedPrice>
            {
                [EconomyState.PriceKey(regionId, WidgetId)] =
                    new PostedPrice(regionId, WidgetId, CoreMoney.From(12m)),
            },
            Policy = policy,
        };

        eco = HoldingLedger.Credit(eco, firm, regionId, OreId, 40m);
        eco = HoldingLedger.Credit(eco, firm, regionId, WidgetId, 6m);
        return eco;
    }
}
