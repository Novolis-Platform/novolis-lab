using System.Collections.Immutable;
using Novolis.Economy;
using Novolis.Economy.Logistics;
using Novolis.Economy.Population;
using Novolis.Economy.Production;
using Novolis.Economy.Simulation;

namespace TrampFreighterSim;

/// <summary>Self-contained tramp world (duplicated — dogfood observer).</summary>
internal static class TrampWorld
{
  // Market book — sized so a careful main-lane haul clears crew/toll/fuel with room to spare.
  public const decimal OreBuy = 2m;
  public const decimal OreSell = 16m;
  public const decimal PartsBuy = 3m;
  public const decimal PartsSell = 14m;
  public const decimal MinMargin = 40m;

  internal sealed record Ids(
    FirmId Tramp,
    InventoryLocationId LocFrontier,
    InventoryLocationId LocWay,
    InventoryLocationId LocCore,
    InventoryLocationId LocRim,
    TransportHubId HubFrontier,
    TransportHubId HubWay,
    TransportHubId HubCore,
    TransportHubId HubRim,
    VehicleClassId Hull,
    FacilityId CoreFacility,
    FacilityId FrontierFacility,
    ProductId Ore,
    ProductId Parts,
    ProductId Fuel,
    VehicleClass HullClass);

  internal static (EconomySimulation Sim, Ids Ids) Create(ulong seed = 77)
  {
    var builder = new EconomyWorldBuilder(new EconomyPolicy
    {
      WageRatePerHour = Money.From(10m),
      LaborHoursPerOutputUnit = 0.1m,
      PeriodHours = 24,
    });

    var tramp = FirmId.From(Guid.Parse("00000000-0000-4000-8000-0000000000c1"));
    var locF = InventoryLocationId.From(builder.NextGuid());
    var locW = InventoryLocationId.From(builder.NextGuid());
    var locC = InventoryLocationId.From(builder.NextGuid());
    var locR = InventoryLocationId.From(builder.NextGuid());
    var hubF = TransportHubId.From(builder.NextGuid());
    var hubW = TransportHubId.From(builder.NextGuid());
    var hubC = TransportHubId.From(builder.NextGuid());
    var hubR = TransportHubId.From(builder.NextGuid());
    var hullId = VehicleClassId.From(builder.NextGuid());
    var coreFacility = FacilityId.From(builder.NextGuid());
    var frontierFacility = FacilityId.From(builder.NextGuid());
    var oreCat = ProductCategoryId.From(builder.NextGuid());
    var partsCat = ProductCategoryId.From(builder.NextGuid());
    var fuelCat = ProductCategoryId.From(builder.NextGuid());
    var ore = ProductId.From(builder.NextGuid());
    var parts = ProductId.From(builder.NextGuid());
    var fuel = ProductId.From(builder.NextGuid());
    var process = ProductionProcessId.From(Guid.Parse("00000000-0000-4000-8000-000000000099"));
    var coreCohort = ConsumerCohortId.From(builder.NextGuid());
    var frontierCohort = ConsumerCohortId.From(builder.NextGuid());
    var area = GeographicAreaId.From(builder.NextGuid());
    var unitCore = OperatingUnitId.From(builder.NextGuid());
    var unitFront = OperatingUnitId.From(builder.NextGuid());

    ProductDefinition Def(ProductId id, ProductCategoryId cat) =>
      new(id, cat, ImmutableArray<ProductInput>.Empty,
        ImmutableArray<ProductAttributeDefinition>.Empty, process, null);

    // One crew seat — wages matter, but a priced main-lane job can still clear.
    var vehicle = new VehicleClass(
      hullId,
      Quantity.From(30m),
      FuelBurnPerDifficultyHour: 1m,
      CrewLaborPerUnderwayHour: 1m,
      FuelTankCapacity: Quantity.From(5m));

    FacilityLayout Layout(OperatingUnitId unit) => new(
      ImmutableDictionary<OperatingUnitId, OperatingUnit>.Empty
        .Add(unit, new OperatingUnit(unit, OperatingUnitKind.Storage, Quantity.From(100m))),
      ImmutableArray<MaterialRoute>.Empty);

    TransportCorridor Lane(TransportHubId a, TransportHubId b, long hours, decimal toll) =>
      new(TransportCorridorId.From(builder.NextGuid()), a, b, hours, Quantity.From(50m), 1m, Money.From(toll));

    builder
      .AddProduct(Def(ore, oreCat))
      .AddProduct(Def(parts, partsCat))
      .AddProduct(Def(fuel, fuelCat))
      .AddFirm(tramp, "MV Independent", Money.From(8_000m))
      .AddFacility(new FacilityBinding(coreFacility, tramp, locC, locC, Layout(unitCore)))
      .AddFacility(new FacilityBinding(frontierFacility, tramp, locF, locF, Layout(unitFront)))
      .AddHub(new TransportHub(hubF, locF, "Frontier Outpost", 2, 2))
      .AddHub(new TransportHub(hubW, locW, "Jump Waystation", 1, 2))
      .AddHub(new TransportHub(hubC, locC, "Core Port", 2, 3))
      .AddHub(new TransportHub(hubR, locR, "Sparse Rim Dock", 3, 1))
      .AddCorridor(Lane(hubF, hubW, 4, 12))
      .AddCorridor(Lane(hubW, hubF, 4, 12))
      .AddCorridor(Lane(hubW, hubC, 5, 18))
      .AddCorridor(Lane(hubC, hubW, 5, 18))
      .AddCorridor(Lane(hubF, hubR, 12, 8))
      .AddCorridor(Lane(hubR, hubF, 12, 8))
      .AddVehicleClass(vehicle)
      .SetTransportFuel(fuel, Money.From(1m))
      .SetLabor(tramp, 16m)
      .AddCohort(new ConsumerCohort(
        coreCohort,
        new PopulationCount(120),
        Money.From(12_000m),
        new PreferenceProfile(
          ImmutableArray.Create(new CategoryPreference(oreCat, 1m)),
          0.6m, 0m, 0m),
        area))
      .AddCohort(new ConsumerCohort(
        frontierCohort,
        new PopulationCount(90),
        Money.From(9_000m),
        new PreferenceProfile(
          ImmutableArray.Create(new CategoryPreference(partsCat, 1m)),
          0.6m, 0m, 0m),
        area));

    var ids = new Ids(
      tramp, locF, locW, locC, locR, hubF, hubW, hubC, hubR, hullId,
      coreFacility, frontierFacility, ore, parts, fuel, vehicle);
    var sim = new EconomySimulation(seed, builder.Build());
    Seed(sim, ids);
    return (sim, ids);
  }

  internal static void Seed(EconomySimulation sim, Ids ids)
  {
    var inv = sim.State.World.Inventory;
    inv.Add(new InventoryKey(ids.Tramp, ids.LocFrontier, ids.Ore),
      new ProductBatch(ids.Ore, Quantity.From(25m), new ProductQuality(100m), Money.From(OreBuy), SimulationDate.Epoch, null));
    inv.Add(new InventoryKey(ids.Tramp, ids.LocFrontier, ids.Fuel),
      new ProductBatch(ids.Fuel, Quantity.From(40m), new ProductQuality(100m), Money.From(1m), SimulationDate.Epoch, null));
    inv.Add(new InventoryKey(ids.Tramp, ids.LocWay, ids.Fuel),
      new ProductBatch(ids.Fuel, Quantity.From(40m), new ProductQuality(100m), Money.From(1m), SimulationDate.Epoch, null));
    inv.Add(new InventoryKey(ids.Tramp, ids.LocCore, ids.Fuel),
      new ProductBatch(ids.Fuel, Quantity.From(20m), new ProductQuality(100m), Money.From(1m), SimulationDate.Epoch, null));
  }
}
