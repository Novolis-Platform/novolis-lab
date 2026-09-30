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
