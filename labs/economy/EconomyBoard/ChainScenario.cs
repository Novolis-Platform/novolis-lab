using System.Collections.Immutable;
using Novolis.Economy;
using Novolis.Economy.Logistics;
using Novolis.Economy.Population;
using Novolis.Economy.Production;
using Novolis.Economy.Simulation;

namespace EconomyBoard;

/// <summary>Self-contained commodity-chain world (duplicated on purpose).</summary>
internal static class ChainScenario
{
  internal sealed record Ids(
    FirmId Firm,
    FacilityId Facility,
    InventoryLocationId Storage,
    InventoryLocationId Retail,
    FreightRouteId Route,
    ProductId Raw,
    ProductId Mid,
    ProductId Fin);

  internal static (EconomySimulation Sim, Ids Ids) Create(ulong seed = 42)
  {
    var builder = new EconomyWorldBuilder(new EconomyPolicy
    {
      WageRatePerHour = Money.From(5m),
      LaborHoursPerOutputUnit = 0.05m,
      PeriodHours = 24,
    });

    var firm = FirmId.From(Guid.Parse("00000000-0000-4000-8000-000000000001"));
    var facility = FacilityId.From(builder.NextGuid());
    var storage = InventoryLocationId.From(builder.NextGuid());
    var retail = InventoryLocationId.From(builder.NextGuid());
    var rawCat = ProductCategoryId.From(builder.NextGuid());
    var midCat = ProductCategoryId.From(builder.NextGuid());
    var finCat = ProductCategoryId.From(builder.NextGuid());
    var raw = ProductId.From(builder.NextGuid());
    var mid = ProductId.From(builder.NextGuid());
    var fin = ProductId.From(builder.NextGuid());
    var process = ProductionProcessId.From(Guid.Parse("00000000-0000-4000-8000-000000000099"));
    var mfg = OperatingUnitId.From(builder.NextGuid());
    var routeId = FreightRouteId.From(builder.NextGuid());
    var cohortId = ConsumerCohortId.From(builder.NextGuid());
    var area = GeographicAreaId.From(builder.NextGuid());

    var rawDef = new ProductDefinition(
      raw, rawCat, ImmutableArray<ProductInput>.Empty,
      ImmutableArray<ProductAttributeDefinition>.Empty, process, null);
    var midDef = new ProductDefinition(
      mid, midCat, ImmutableArray.Create(new ProductInput(raw, Quantity.From(1m))),
      ImmutableArray<ProductAttributeDefinition>.Empty, process, null);
    var finDef = new ProductDefinition(
      fin, finCat, ImmutableArray.Create(new ProductInput(mid, Quantity.From(1m))),
      ImmutableArray<ProductAttributeDefinition>.Empty, process, null);

    var layout = new FacilityLayout(
      ImmutableDictionary<OperatingUnitId, OperatingUnit>.Empty
        .Add(mfg, new OperatingUnit(mfg, OperatingUnitKind.Manufacturing, Quantity.From(100m))),
      ImmutableArray<MaterialRoute>.Empty);

    builder
      .AddProduct(rawDef)
      .AddProduct(midDef)
      .AddProduct(finDef)
      .AddFirm(firm, "Integrated Co", Money.From(10_000m))
      .AddFacility(new FacilityBinding(facility, firm, storage, retail, layout))
      .AddInventory(firm, storage, new ProductBatch(
        raw, Quantity.From(500m), new ProductQuality(100m), Money.From(1m), SimulationDate.Epoch, null))
      .AddRoute(new FreightRoute(routeId, storage, retail, TransitHours: 1, Capacity: Quantity.From(50m)))
      .SetRestockRoute(facility, routeId)
      .SetLabor(firm, 40m)
      .AddCohort(new ConsumerCohort(
        cohortId,
        new PopulationCount(100),
        Money.From(5_000m),
        new PreferenceProfile(
          ImmutableArray.Create(new CategoryPreference(finCat, 1m)),
          1m, 0m, 0m),
        area));

    var ids = new Ids(firm, facility, storage, retail, routeId, raw, mid, fin);
    var sim = new EconomySimulation(seed, builder.Build());
    sim.Enqueue(new SetProductionPlan(firm, facility, mid, Quantity.From(10m)));
    sim.Enqueue(new SetProductionPlan(firm, facility, fin, Quantity.From(8m)));
    sim.Enqueue(new SetRetailPrice(firm, facility, fin, Money.From(5m)));
    return (sim, ids);
  }
}
