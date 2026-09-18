using Novolis.Economy;
using Novolis.Economy.Logistics;
using Novolis.Economy.Production;
using Novolis.Economy.Simulation;

namespace TrampFreighterSim;

/// <summary>Evaluated haul opportunity (simple margin heuristic).</summary>
internal sealed record JobQuote(
  string Name,
  TransportHubId Origin,
  TransportHubId Destination,
  InventoryLocationId BuyAt,
  InventoryLocationId SellAt,
  FacilityId SellFacility,
  ProductId Product,
  decimal Quantity,
  decimal EffectiveBuyUnit,
  decimal SellUnit,
  long UnderwayHours,
  decimal FuelUnits,
  decimal Tolls,
  decimal CrewHours,
  decimal WageRate,
  decimal FuelUnitCost,
  bool Feasible)
{
  public decimal Revenue => Quantity * SellUnit;
  public decimal Cog => Quantity * EffectiveBuyUnit;
  public decimal FuelCost => FuelUnits * FuelUnitCost;
  public decimal CrewCost => CrewHours * WageRate;
  public decimal Margin => Feasible
    ? Revenue - Cog - FuelCost - Tolls - CrewCost
    : decimal.MinValue / 4m;

  public string Summary =>
    Feasible
      ? $"{Name}: qty {Quantity:0} Δ{Margin:0.#} (rev {Revenue:0} cog {Cog:0} fuel {FuelCost:0} toll {Tolls:0} crew {CrewCost:0})"
      : $"{Name}: infeasible";
}

/// <summary>Decision-based tramp operator with a thin margin heuristic.</summary>
internal sealed class TrampAutopilot
{
  private readonly EconomySimulation _sim;
  private readonly TrampWorld.Ids _ids;
  private readonly VehicleClass _hull;
  private int _idleHours;
  private int _sellWaitHours;
  private bool _askPosted;
  private ProductId? _sellingProduct;

  public TrampAutopilot(EconomySimulation sim, TrampWorld.Ids ids, VehicleClass hull)
  {
    _sim = sim;
    _ids = ids;
    _hull = hull;
  }

  public string LastDecision { get; private set; } = "standing by";
  public string LastEval { get; private set; } = "";

  public void Tick()
  {
    var world = _sim.State.World;
    var busy = world.Shipments.Any(s => !s.IsLegacy && s.Status == ShipmentStatus.InTransit)
      || world.PendingPlanShipments.Count > 0
      || world.PendingProcurement.Count > 0;
    if (busy)
    {
      LastDecision = "underway / pending orders";
      return;
    }

    decimal Qty(InventoryLocationId loc, ProductId p) =>
      world.Inventory.GetQuantity(new InventoryKey(_ids.Tramp, loc, p)).Value;

    TopUpFuelIfNeeded(Qty);

    if (_askPosted && _sellingProduct is { } selling)
    {
      var remaining = selling.Equals(_ids.Ore)
        ? Qty(_ids.LocCore, selling)
        : Qty(_ids.LocFrontier, selling);
      if (remaining > 0.5m && _sellWaitHours < 40)
      {
        _sellWaitHours++;
        LastDecision = $"waiting sales ({(selling.Equals(_ids.Ore) ? "ore@Core" : "parts@Frontier")} {remaining:0} left)";
        return;
      }

      _askPosted = false;
      _sellingProduct = null;
      _sellWaitHours = 0;
    }

    var oreCore = Qty(_ids.LocCore, _ids.Ore);
    var partsFrontier = Qty(_ids.LocFrontier, _ids.Parts);

    if (oreCore >= 1m)
    {
      _sim.Enqueue(new SetRetailPrice(_ids.Tramp, _ids.CoreFacility, _ids.Ore, Money.From(TrampWorld.OreSell)));
      _askPosted = true;
      _sellingProduct = _ids.Ore;
      _sellWaitHours = 0;
      LastDecision = $"list ore @ Core {TrampWorld.OreSell:0.##} (have {oreCore:0})";
      LastEval = "delivered cargo — sell before next accept";
      return;
    }

    if (partsFrontier >= 1m)
    {
      _sim.Enqueue(new SetRetailPrice(_ids.Tramp, _ids.FrontierFacility, _ids.Parts, Money.From(TrampWorld.PartsSell)));
      _askPosted = true;
      _sellingProduct = _ids.Parts;
      _sellWaitHours = 0;
      LastDecision = $"list parts @ Frontier {TrampWorld.PartsSell:0.##} (have {partsFrontier:0})";
      LastEval = "backhaul delivered — sell before next accept";
      return;
    }

    var quotes = BuildQuotes(Qty).OrderByDescending(q => q.Margin).ToList();
    LastEval = string.Join(" · ", quotes.Select(q =>
      q.Feasible ? $"{q.Name} Δ{q.Margin:0}" : $"{q.Name} no"));

    var best = quotes.FirstOrDefault(q => q.Feasible && q.Margin >= TrampWorld.MinMargin);
    if (best is null)
    {
      _idleHours++;
      var rim = quotes.FirstOrDefault(q => q.Name.StartsWith("Rim", StringComparison.Ordinal));
      if (_idleHours % 48 == 12 && rim is not null)
      {
        LastDecision = $"reject {rim.Name}: {DescribeReject(rim)}";
        return;
      }

      var near = quotes.Where(q => q.Feasible).OrderByDescending(q => q.Margin).FirstOrDefault();
      LastDecision = near is null
        ? "idle — no feasible jobs"
        : $"idle — best {near.Name} Δ{near.Margin:0.#} < min {TrampWorld.MinMargin}";
      return;
    }

    _idleHours = 0;
    CommitJob(best, Qty);
  }

  private static string DescribeReject(JobQuote q) =>
    !q.Feasible ? "infeasible (tank/path)" : $"margin {q.Margin:0.#}";

  private List<JobQuote> BuildQuotes(Func<InventoryLocationId, ProductId, decimal> qty)
  {
    var wage = _sim.State.World.Policy.WageRatePerHour.Amount;
    var fuelCost = _sim.State.World.TransportFuelUnitCost.Amount;
    const long mainUnderway = 9;
    const decimal mainFuel = 9m;
    const decimal mainToll = 30m;
    var crewHours = mainUnderway * _hull.CrewLaborPerUnderwayHour;

    var oreHave = qty(_ids.LocFrontier, _ids.Ore);
    var oreQty = Math.Min(_hull.CargoCapacity.Value, Math.Max(oreHave > 0 ? oreHave : 20m, 15m));
    var oreNeedBuy = Math.Max(0m, oreQty - oreHave);

    var partsHave = qty(_ids.LocCore, _ids.Parts);
    var partsQty = Math.Min(_hull.CargoCapacity.Value, Math.Max(partsHave > 0 ? partsHave : 15m, 12m));
    var partsNeedBuy = Math.Max(0m, partsQty - partsHave);

    return
    [
      Quote(
        "Ore F→C", oreQty, oreNeedBuy, TrampWorld.OreBuy, TrampWorld.OreSell,
        _ids.HubFrontier, _ids.HubCore, _ids.LocFrontier, _ids.LocCore, _ids.CoreFacility,
        _ids.Ore, mainUnderway, mainFuel, mainToll, crewHours, wage, fuelCost, feasible: true),
      Quote(
        "Parts C→F", partsQty, partsNeedBuy, TrampWorld.PartsBuy, TrampWorld.PartsSell,
        _ids.HubCore, _ids.HubFrontier, _ids.LocCore, _ids.LocFrontier, _ids.FrontierFacility,
        _ids.Parts, mainUnderway, mainFuel, mainToll, crewHours, wage, fuelCost, feasible: true),
      Quote(
        "Rim F→R", 5m, 5m, TrampWorld.OreBuy, TrampWorld.OreSell * 2.5m,
        _ids.HubFrontier, _ids.HubRim, _ids.LocFrontier, _ids.LocRim, _ids.CoreFacility,
        _ids.Ore, 12, 12m, 8m, 12 * _hull.CrewLaborPerUnderwayHour, wage, fuelCost, feasible: false),
    ];
  }

  private static JobQuote Quote(
    string name,
    decimal qty,
    decimal needBuy,
    decimal buyUnit,
    decimal sellUnit,
    TransportHubId origin,
    TransportHubId dest,
    InventoryLocationId buyAt,
    InventoryLocationId sellAt,
    FacilityId sellFacility,
    ProductId product,
    long underway,
    decimal fuel,
    decimal toll,
    decimal crewHours,
    decimal wage,
    decimal fuelCost,
    bool feasible)
  {
    var effectiveBuy = qty <= 0m ? buyUnit : needBuy * buyUnit / qty;
    return new JobQuote(
      name, origin, dest, buyAt, sellAt, sellFacility, product, qty,
      effectiveBuy, sellUnit, underway, fuel, toll, crewHours, wage, fuelCost, feasible);
  }

  private void CommitJob(JobQuote job, Func<InventoryLocationId, ProductId, decimal> qty)
  {
    var have = qty(job.BuyAt, job.Product);
    var need = job.Quantity - have;
    if (need > 0.01m)
    {
      var unit = job.Product.Equals(_ids.Ore) ? TrampWorld.OreBuy : TrampWorld.PartsBuy;
      _sim.Enqueue(new PlaceProcurementOrder(
        _ids.Tramp, job.BuyAt, job.Product, Quantity.From(need), Money.From(unit)));
      LastDecision = $"buy {need:0} for {job.Name} (eval Δ{job.Margin:0.#})";
      LastEval = job.Summary;
      return;
    }

    // Where is the hull? Only accept jobs that start from a hub where we have the cargo.
    // Cargo location implies we're operating from that hub after prior delivery.
    _sim.Enqueue(new PlanShipment(
      _ids.Tramp,
      job.Origin.Value,
      job.Destination.Value,
      job.Product,
      Quantity.From(job.Quantity),
      _ids.Hull.Value));
    LastDecision = $"accept {job.Name} qty {job.Quantity:0} Δ{job.Margin:0.#}";
    LastEval = job.Summary;
  }

  private void TopUpFuelIfNeeded(Func<InventoryLocationId, ProductId, decimal> qty)
  {
    void Maybe(InventoryLocationId loc, decimal min, decimal price)
    {
      if (qty(loc, _ids.Fuel) + 0.01m >= min)
      {
        return;
      }

      _sim.Enqueue(new PlaceProcurementOrder(
        _ids.Tramp, loc, _ids.Fuel, Quantity.From(12m), Money.From(price)));
    }

    Maybe(_ids.LocFrontier, 10m, 1m);
    Maybe(_ids.LocWay, 10m, 1.5m);
    Maybe(_ids.LocCore, 10m, 1m);
  }
}
