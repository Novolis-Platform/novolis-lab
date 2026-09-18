using Novolis.Astro.Assessment;
using Novolis.Economy;
using Novolis.Economy.Agents;
using Novolis.Economy.Markets;
using Novolis.Economy.Production;
using Novolis.Economy.Simulation;

namespace NearSolPolity;

/// <summary>
/// Sol Capital export hub.
/// <list type="bullet">
/// <item>Overflow <b>buy</b> bids only below Industry delivered price (never steal plant feedstock).</item>
/// <item><b>Export</b> (exogenous dump) only when warehouse Raw is above soft store-limit.</item>
/// <item>Volume ≤ one hull-load; time-gated so berth/dwell capacity matters.</item>
/// </list>
/// </summary>
internal sealed class SolExportHubAgent : IEconomicAgent
{
  private readonly PolityWorld.Ids _ids;
  private readonly ulong _rngSalt;

  public SolExportHubAgent(PolityWorld.Ids ids, ulong rngSalt = 0x534F4C45UL)
  {
    _ids = ids;
    _rngSalt = rngSalt;
    FirmId = ids.Station;
  }

  public FirmId FirmId { get; }

  public string LastDecision { get; private set; } = "export idle";

  public decimal LastExportQty { get; private set; }

  public void Tick(AgentContext context)
  {
    LastExportQty = 0m;
    if (!_ids.Sites.TryGetValue("sol", out var sol) || sol.Hub.Role != SystemRole.Capital)
    {
      LastDecision = "no sol hub";
      return;
    }

    var world = context.World;
    var loc = sol.Hub.LocationId;
    var limits = world.Inventory.Limits;
    var soft = limits.SoftCap(loc, _ids.Ore) ?? PolityWorld.SolRawSoftCap;
    var onHand = InventoryStoreLimits.OnHand(world.Inventory, loc, _ids.Ore);
    var surplus = limits.Surplus(world.Inventory, loc, _ids.Ore);
    var room = limits.Room(world.Inventory, loc, _ids.Ore);
    var rng = new DeterministicRandom(
      context.Simulation.State.Seed ^ _rngSalt ^ (ulong)context.Clock.HourIndex);

    // Overflow bid: fill toward soft only, at a price below Industry OreDelivered.
    if (onHand < soft * 0.85m && room >= PolityWorld.ExportMinLot)
    {
      var need = Math.Min(soft - onHand, Math.Min(room, _ids.Hull.CargoCapacity.Value));
      if (need >= PolityWorld.ExportMinLot)
      {
        var px = PolityWorld.OreDelivered - 1.5m;
        context.Enqueue(new PostHubOrder(
          FirmId, loc, _ids.Ore, HubOrderSide.Buy,
          Quantity.From(need), Money.From(Math.Round(px, 2))));
        LastDecision = $"Sol overflow bid Raw ×{need:0} @ {px:0.##}";
      }
    }

    if (surplus < PolityWorld.ExportMinLot)
    {
      if (!LastDecision.StartsWith("Sol overflow", StringComparison.Ordinal))
      {
        LastDecision = $"Sol hold Raw {onHand:0}/{soft:0}";
      }

      return;
    }

    // ExportBid / dump — large surplus only.
    if (context.Clock.HourIndex % 6 != 0 || rng.NextDouble() < 0.3)
    {
      LastDecision = $"Sol surplus wait ×{surplus:0}";
      return;
    }

    var dump = Math.Min(surplus, _ids.Hull.CargoCapacity.Value);
    context.Enqueue(new PlaceExportOrder(
      FirmId, loc, _ids.Ore, Quantity.From(dump), Money.From(PolityWorld.OreExport)));
    LastExportQty = dump;
    LastDecision = $"Sol export surplus Raw ×{dump:0} (above {soft:0})";
  }
}
