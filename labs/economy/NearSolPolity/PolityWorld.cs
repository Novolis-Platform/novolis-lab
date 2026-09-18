using System.Collections.Immutable;
using Novolis.Astro.Assessment;
using Novolis.Astro.Catalog;
using Novolis.Economy;
using Novolis.Economy.Logistics;
using Novolis.Economy.Population;
using Novolis.Economy.Production;
using Novolis.Economy.Simulation;

namespace NearSolPolity;

/// <summary>Seeds the near-Sol four-firm tycoon economy on the Astro bridge.</summary>
internal static class PolityWorld
{
  public const decimal OreBuy = 2m;
  public const decimal OreSell = 10m;
  /// <summary>Industry delivered Raw bid — above mine gate + haul, below Final gate.</summary>
  public const decimal OreDelivered = 9m;
  /// <summary>Sol export hub bid / exogenous Raw floor — only used when warehouse is in surplus.</summary>
  public const decimal OreExport = 12m;
    // Soft surplus threshold for Sol Raw — ExportBids only above this.
    public const decimal SolRawSoftCap = 40m;
    /// <summary>Hard store-limit for Sol Raw — inbound waits when full (no destroy).</summary>
    public const decimal SolRawHardCap = 96m;
  public const decimal ExportMinLot = 4m;
  /// <summary>Tramp hull cargo volume (scarcer than legacy open corridor 80).</summary>
  public const decimal HullCargoCapacity = 36m;
  /// <summary>Corridor max cargo (volume).</summary>
  public const decimal CorridorMaxCargo = 48m;
  public const decimal FreightPremiumPerUnit = 1.25m;
  public const decimal PartsPerOre = 0.15m;
  public const decimal OrePerFuel = 0.5m;
  public const decimal PartsBuy = 5m;
  public const decimal PartsSell = 9m;
  public const decimal PartsDelivered = 11m;
  /// <summary>Plant gate for Final — above ore/parts COGS so Industry stays liquid.</summary>
  public const decimal GoodsFactory = 12m;
  /// <summary>Household shelf price — Final is the consumption sink.</summary>
  public const decimal GoodsSell = 15m;
  /// <summary>Station restock bid (delivered Final).</summary>
  public const decimal GoodsDelivered = 17m;
  public const decimal FuelUnitCost = 1m;
  /// <summary>
  /// Floor haul Δ after fuel/toll/crew. Small positive keeps empty pickups selective;
  /// holding-cargo dumps use BestOutboundFrom without this floor.
  /// </summary>
  public const decimal MinMargin = 0.20m;

  /// <summary>Independent tramp firms (one hull each — CarrierFirmAgent is single-ship).</summary>
  public const int TrampFleetSize = 8;
  public const decimal MineOreCap = 100m;
  public const decimal MinePartsFloor = 10m;
  public const decimal PlantOreFloor = 18m;
  public const decimal RetailStockTarget = 32m;
  public const decimal FirmCashFloor = 1_500m;

  /// <summary>Working capital for the Final sink loop (Industry must stay liquid).</summary>
  public const decimal OpeningFirmCash = 18_000m;
  /// <summary>Household budget stock — spend into Final shelves (consumption sink).</summary>
  public const decimal OpeningHouseholdCredits = 85_000m;

  public const decimal FuelBurnPerDifficultyHour = 1m / 68m;

  public static string SkuLabel(ProductId product, Ids ids)
  {
    if (product.Equals(ids.Ore)) return "Raw";
    if (product.Equals(ids.Parts)) return "Capital";
    if (product.Equals(ids.Goods)) return "Final";
    if (product.Equals(ids.Fuel)) return "Energy";
    return "sku";
  }

  internal sealed class Site
  {
    public required AstroEconomyBridge.HubBinding Hub { get; init; }
    public FirmId? OwnerFirm { get; init; }
    /// <summary>Retail / demand-facing facility (Station Sales when present).</summary>
    public FacilityId? Facility { get; init; }
    /// <summary>Manufacturing facility when distinct from retail (mines / plants).</summary>
    public FacilityId? MfgFacility { get; init; }
    public FacilityId? CarrierPost { get; init; }
    public OperatingUnitId? MfgUnit { get; init; }
  }

  internal sealed class Ids
  {
    public required FirmId Mining { get; init; }
    public required FirmId Industry { get; init; }
    public required FirmId Station { get; init; }
    public required FirmId Carrier { get; init; }
    public required IReadOnlyList<FirmId> Carriers { get; init; }
    public required ProductId Ore { get; init; }
    public required ProductId Parts { get; init; }
    public required ProductId Goods { get; init; }
    public required ProductId Fuel { get; init; }
    public required ProductCategoryId OreCat { get; init; }
    public required ProductCategoryId PartsCat { get; init; }
    public required ProductCategoryId GoodsCat { get; init; }
    public required VehicleClassId HullId { get; init; }
    public required VehicleClass Hull { get; init; }
    public required AstroEconomyBridge.BridgeResult Bridge { get; init; }
    public required IReadOnlyDictionary<string, Site> Sites { get; init; }
    public required string RoleSummary { get; init; }

    public IEnumerable<(string Name, FirmId Id)> Firms
    {
      get
      {
        yield return ("Mining", Mining);
        yield return ("Industry", Industry);
        yield return ("Station", Station);
        yield return ("Carrier", Carrier);
        for (var i = 1; i < Carriers.Count; i++)
        {
          yield return ($"Tramp{i + 1}", Carriers[i]);
        }
      }
    }
  }

  internal static (EconomySimulation Sim, Ids Ids) Create(ulong seed = 1001)
  {
    var catalog = NearSolCatalog.Load();
    var mining = FirmId.From(Guid.Parse("00000000-0000-4000-8000-0000000000a1"));
    var industry = FirmId.From(Guid.Parse("00000000-0000-4000-8000-0000000000a2"));
    var station = FirmId.From(Guid.Parse("00000000-0000-4000-8000-0000000000a3"));
    var carriers = Enumerable.Range(0, TrampFleetSize)
      .Select(i => FirmId.From(Guid.Parse($"00000000-0000-4000-8000-00000000{(0xb0 + i):x4}")))
      .ToArray();
    var carrier = carriers[0];

    var builder = new EconomyWorldBuilder(new EconomyPolicy
    {
      WageRatePerHour = Money.From(12m),
      LaborHoursPerOutputUnit = 0.05m,
      PeriodHours = 24,
      HouseholdCreditFromWages = true,
      CohortBudgetResetMode = CohortBudgetResetMode.CarryForward,
      TollBeneficiaryFirmId = station,
      PriceElasticity = 0.8m,
      HouseholdComfortThresholdPerHousehold = Money.From(40m),
    });

    var bridge = AstroEconomyBridge.Build(catalog, builder, seed);
    var roleSummary = RoleAssigner.Summarize(bridge.Roles)
      + " · " + RoleAssigner.SummarizePotentials(bridge.Hubs);

    var oreCat = ProductCategoryId.From(builder.NextGuid());
    var partsCat = ProductCategoryId.From(builder.NextGuid());
    var goodsCat = ProductCategoryId.From(builder.NextGuid());
    var fuelCat = ProductCategoryId.From(builder.NextGuid());
    var ore = ProductId.From(builder.NextGuid());
    var parts = ProductId.From(builder.NextGuid());
    var goods = ProductId.From(builder.NextGuid());
    var fuel = ProductId.From(builder.NextGuid());
    var process = ProductionProcessId.From(Guid.Parse("00000000-0000-4000-8000-000000000099"));
    var hullId = VehicleClassId.From(builder.NextGuid());
    var areas = new Dictionary<string, GeographicAreaId>(StringComparer.OrdinalIgnoreCase);
    foreach (var hub in bridge.Hubs)
    {
      areas[hub.SystemId] = GeographicAreaId.From(builder.NextGuid());
    }

    var oreDef = new ProductDefinition(
      ore, oreCat,
      ImmutableArray.Create(new ProductInput(parts, Quantity.From(PartsPerOre))),
      ImmutableArray<ProductAttributeDefinition>.Empty, process, null);
    var partsDef = new ProductDefinition(
      parts, partsCat, ImmutableArray.Create(new ProductInput(ore, Quantity.From(1m))),
      ImmutableArray<ProductAttributeDefinition>.Empty, process, null);
    var goodsDef = new ProductDefinition(
      goods, goodsCat, ImmutableArray.Create(new ProductInput(parts, Quantity.From(1m))),
      ImmutableArray<ProductAttributeDefinition>.Empty, process, null);
    var fuelDef = new ProductDefinition(
      fuel, fuelCat, ImmutableArray.Create(new ProductInput(ore, Quantity.From(OrePerFuel))),
      ImmutableArray<ProductAttributeDefinition>.Empty, process, null);

    var hull = new VehicleClass(
      hullId,
      Quantity.From(HullCargoCapacity),
      FuelBurnPerDifficultyHour,
      CrewLaborPerUnderwayHour: 0.02m,
      // Large enough for typical multi-leg burns when origin tops off the tank.
      FuelTankCapacity: Quantity.From(24m));

    builder
      .AddProduct(oreDef)
      .AddProduct(partsDef)
      .AddProduct(goodsDef)
      .AddProduct(fuelDef)
      .AddFirm(mining, "Near-Sol Mining", Money.From(OpeningFirmCash))
      .AddFirm(industry, "Near-Sol Industry", Money.From(OpeningFirmCash))
      .AddCivic(station, "Near-Sol Station", Money.From(OpeningFirmCash), "nearsol-civic")
      .AddFirm(carrier, "MV Independent", Money.From(OpeningFirmCash));
    for (var i = 1; i < carriers.Length; i++)
    {
      builder.AddFirm(carriers[i], $"MV Tramp {i + 1}", Money.From(OpeningFirmCash * 0.45m));
    }

    // Carrier crew labor only — region pools supply manufacturing firms.
    builder
      .AddVehicleClass(hull)
      .SetTransportFuel(fuel, Money.From(FuelUnitCost))
      .SetLabor(carrier, 32m);
    foreach (var tramp in carriers.Skip(1))
    {
      builder.SetLabor(tramp, 28m);
    }

    var sites = new Dictionary<string, Site>(StringComparer.OrdinalIgnoreCase);
    var householdSeeds = new List<(AstroEconomyBridge.HubBinding Hub, int Households, FirmId HouseholdId)>();
    var mfgSlotsNeeded = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    var livingNeeded = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    foreach (var hub in bridge.Hubs.OrderBy(h => h.SystemId, StringComparer.Ordinal))
    {
      if (hub.Role is SystemRole.Capital or SystemRole.Inhabited or SystemRole.Industrial
          or SystemRole.Mining)
      {
        // Cohort PopulationCount is household count (no headcount layer).
        // Mining camps are small but required — region labor pools are area-local.
        var households = hub.Role switch
        {
          SystemRole.Capital => 100,
          SystemRole.Industrial => 45,
          SystemRole.Mining => 12,
          _ => 30,
        };
        livingNeeded[hub.SystemId] = livingNeeded.GetValueOrDefault(hub.SystemId) + households;
        householdSeeds.Add((hub, households, FirmId.From(builder.NextGuid())));
      }

      if (hub.Role is SystemRole.Mining or SystemRole.Industrial)
      {
        mfgSlotsNeeded[hub.SystemId] = mfgSlotsNeeded.GetValueOrDefault(hub.SystemId) + 1;
      }
    }

    foreach (var hub in bridge.Hubs.OrderBy(h => h.SystemId, StringComparer.Ordinal))
    {
      var area = areas[hub.SystemId];
      var roleFloor = hub.Role switch
      {
        SystemRole.Capital => 120,
        SystemRole.Industrial => 50,
        SystemRole.Inhabited => 40,
        SystemRole.Mining => 20,
        _ => 5,
      };
      var seededHh = livingNeeded.GetValueOrDefault(hub.SystemId);
      var living = Math.Max(roleFloor, seededHh);
      var production = mfgSlotsNeeded.GetValueOrDefault(hub.SystemId) + 2;
      builder.AddRegion(area, living, production);
    }

    FirmId? capitalHousehold = null;
    foreach (var hub in bridge.Hubs.OrderBy(h => h.SystemId, StringComparer.Ordinal))
    {
      FirmId? owner = hub.Role switch
      {
        SystemRole.Mining => mining,
        SystemRole.Industrial => industry,
        SystemRole.Capital or SystemRole.Inhabited => station,
        _ => null,
      };

      FacilityId? facility = null;
      FacilityId? mfgFacility = null;
      OperatingUnitId? mfg = null;
      FacilityId? carrierPost = null;
      var area = areas[hub.SystemId];

      if (owner is { } firmId && hub.Role is SystemRole.Mining or SystemRole.Industrial
          or SystemRole.Inhabited or SystemRole.Capital)
      {
        var facilityId = FacilityId.From(builder.NextGuid());
        var unitId = OperatingUnitId.From(builder.NextGuid());
        mfg = unitId;
        var kind = hub.Role is SystemRole.Mining or SystemRole.Industrial
          ? OperatingUnitKind.Manufacturing
          : OperatingUnitKind.Sales;
        var capacity = hub.Role switch
        {
          SystemRole.Capital => 200m,
          SystemRole.Industrial => 120m,
          SystemRole.Mining => 80m,
          _ => 60m,
        };
        var layout = new FacilityLayout(
          ImmutableDictionary<OperatingUnitId, OperatingUnit>.Empty
            .Add(unitId, new OperatingUnit(unitId, kind, Quantity.From(capacity))),
          ImmutableArray<MaterialRoute>.Empty);
        builder.AddFacility(new FacilityBinding(facilityId, firmId, hub.LocationId, hub.LocationId, layout, area));
        if (hub.Role is SystemRole.Mining or SystemRole.Industrial)
        {
          mfgFacility = facilityId;
        }
        else
        {
          facility = facilityId; // Station Sales at Capital / Inhabited
        }
      }

      // Mining camps: Station Sales shelf so Final consumption can sink household budgets.
      if (hub.Role is SystemRole.Mining)
      {
        var retailId = FacilityId.From(builder.NextGuid());
        var salesUnit = OperatingUnitId.From(builder.NextGuid());
        var retailLayout = new FacilityLayout(
          ImmutableDictionary<OperatingUnitId, OperatingUnit>.Empty
            .Add(salesUnit, new OperatingUnit(salesUnit, OperatingUnitKind.Sales, Quantity.From(80m))),
          ImmutableArray<MaterialRoute>.Empty);
        builder.AddFacility(new FacilityBinding(
          retailId, station, hub.LocationId, hub.LocationId, retailLayout, area));
        facility = retailId;
      }

      if (hub.Role is SystemRole.Capital or SystemRole.Inhabited or SystemRole.Industrial or SystemRole.Mining)
      {
        var postId = FacilityId.From(builder.NextGuid());
        var store = OperatingUnitId.From(builder.NextGuid());
        var layout = new FacilityLayout(
          ImmutableDictionary<OperatingUnitId, OperatingUnit>.Empty
            .Add(store, new OperatingUnit(store, OperatingUnitKind.Storage, Quantity.From(80m))),
          ImmutableArray<MaterialRoute>.Empty);
        builder.AddFacility(new FacilityBinding(postId, carrier, hub.LocationId, hub.LocationId, layout, area));
        carrierPost = postId;
      }

      sites[hub.SystemId] = new Site
      {
        Hub = hub,
        OwnerFirm = owner,
        Facility = facility,
        MfgFacility = mfgFacility,
        CarrierPost = carrierPost,
        MfgUnit = mfg,
      };
    }

    var householdSum = householdSeeds.Sum(h => h.Households);
    var creditsLeft = OpeningHouseholdCredits;
    for (var i = 0; i < householdSeeds.Count; i++)
    {
      var (hub, households, householdId) = householdSeeds[i];
      decimal budget;
      if (i == householdSeeds.Count - 1)
      {
        budget = creditsLeft;
      }
      else
      {
        budget = Math.Round(OpeningHouseholdCredits * households / householdSum, 2, MidpointRounding.AwayFromZero);
        creditsLeft -= budget;
      }

      // Final (Goods) is the household consumption sink — not Capital parts.
      var prefs = ImmutableArray.Create(
        new CategoryPreference(goodsCat, 1m));

      if (hub.Role == SystemRole.Capital && capitalHousehold is null)
      {
        capitalHousehold = householdId;
      }

      builder.AddCohort(new ConsumerCohort(
        ConsumerCohortId.From(builder.NextGuid()),
        new PopulationCount(households),
        Money.From(budget),
        new PreferenceProfile(prefs, 0.7m, 0m, 0m),
        areas[hub.SystemId],
        HouseholdProductivityKind.Mean,
        householdId));
    }

    // Civic majority + capital seed + float for household PurchaseOwnership.
    if (capitalHousehold is { } capitalHh)
    {
      builder.SetOwnership(mining, capitalHh, 0.15m);
      builder.SetOwnership(mining, station, 0.7m);
    }
    else
    {
      builder.SetOwnership(mining, station, 0.85m);
    }

    builder.SetOwnership(industry, station, 0.35m);

    var ids = new Ids
    {
      Mining = mining,
      Industry = industry,
      Station = station,
      Carrier = carrier,
      Carriers = carriers,
      Ore = ore,
      Parts = parts,
      Goods = goods,
      Fuel = fuel,
      OreCat = oreCat,
      PartsCat = partsCat,
      GoodsCat = goodsCat,
      HullId = hullId,
      Hull = hull,
      Bridge = bridge,
      Sites = sites,
      RoleSummary = roleSummary,
    };

    var sim = new EconomySimulation(seed, builder.Build());
    SeedInventory(sim, ids);
    ApplyStoreLimits(sim, ids);
    SeedInvariants.Assert(ids, sim);
    return (sim, ids);
  }

  /// <summary>Hard warehouse caps + soft surplus thresholds (export policy reads soft).</summary>
  private static void ApplyStoreLimits(EconomySimulation sim, Ids ids)
  {
    var limits = sim.State.World.Inventory.Limits;
    if (ids.Sites.TryGetValue("sol", out var sol))
    {
      var loc = sol.Hub.LocationId;
      limits.Set(loc, ids.Ore, SolRawSoftCap, SolRawHardCap);
      limits.Set(loc, ids.Parts, softCap: 48m, hardCap: 96m);
      limits.Set(loc, ids.Goods, softCap: 56m, hardCap: 100m);
      limits.Set(loc, ids.Fuel, softCap: 64m, hardCap: 140m);
    }
  }

  private static void SeedInventory(EconomySimulation sim, Ids ids)
  {
    var inv = sim.State.World.Inventory;
    var epoch = SimulationDate.Epoch;

    void Add(FirmId firm, InventoryLocationId loc, ProductId product, decimal qty, decimal unitCost)
    {
      if (qty <= 0)
      {
        return;
      }

      inv.Add(
        new InventoryKey(firm, loc, product),
        new ProductBatch(product, Quantity.From(qty), new ProductQuality(100m), Money.From(unitCost), epoch, null),
        bypassLimits: true);
    }

    foreach (var site in ids.Sites.Values)
    {
      var hub = site.Hub;
      switch (hub.Role)
      {
        case SystemRole.Mining:
          Add(ids.Mining, hub.LocationId, ids.Ore, 45m, OreBuy);
          Add(ids.Mining, hub.LocationId, ids.Parts, 40m, PartsBuy);
          Add(ids.Station, hub.LocationId, ids.Goods, 18m, GoodsSell * 0.4m);
          Add(ids.Station, hub.LocationId, ids.Fuel, 28m, FuelUnitCost);
          foreach (var tramp in ids.Carriers)
          {
            Add(tramp, hub.LocationId, ids.Fuel, 10m, FuelUnitCost);
          }

          break;
        case SystemRole.Industrial:
          Add(ids.Industry, hub.LocationId, ids.Ore, 55m, OreBuy);
          Add(ids.Industry, hub.LocationId, ids.Parts, 40m, PartsBuy);
          Add(ids.Industry, hub.LocationId, ids.Goods, 20m, GoodsSell * 0.4m);
          Add(ids.Industry, hub.LocationId, ids.Fuel, 24m, FuelUnitCost);
          Add(ids.Station, hub.LocationId, ids.Fuel, 32m, FuelUnitCost);
          foreach (var tramp in ids.Carriers)
          {
            Add(tramp, hub.LocationId, ids.Fuel, 10m, FuelUnitCost);
          }

          break;
        case SystemRole.Capital:
          Add(ids.Station, hub.LocationId, ids.Parts, 20m, PartsBuy);
          Add(ids.Station, hub.LocationId, ids.Goods, 25m, GoodsSell * 0.4m);
          Add(ids.Station, hub.LocationId, ids.Fuel, 40m, FuelUnitCost);
          // Modest Raw buffer under soft cap — export only after inbound surplus piles up.
          Add(ids.Station, hub.LocationId, ids.Ore, 28m, OreBuy);
          foreach (var tramp in ids.Carriers)
          {
            Add(tramp, hub.LocationId, ids.Fuel, 10m, FuelUnitCost);
          }

          break;
        case SystemRole.Inhabited:
          Add(ids.Station, hub.LocationId, ids.Goods, 10m, GoodsSell * 0.4m);
          Add(ids.Station, hub.LocationId, ids.Fuel, 22m, FuelUnitCost);
          foreach (var tramp in ids.Carriers)
          {
            Add(tramp, hub.LocationId, ids.Fuel, 8m, FuelUnitCost);
          }

          break;
        case SystemRole.Transit:
          Add(ids.Station, hub.LocationId, ids.Fuel, 32m, FuelUnitCost);
          foreach (var tramp in ids.Carriers)
          {
            Add(tramp, hub.LocationId, ids.Fuel, 9m, FuelUnitCost);
          }

          break;
        default:
          Add(ids.Station, hub.LocationId, ids.Fuel, 14m, FuelUnitCost);
          foreach (var tramp in ids.Carriers)
          {
            Add(tramp, hub.LocationId, ids.Fuel, 6m, FuelUnitCost);
          }

          break;
      }
    }
  }
}
