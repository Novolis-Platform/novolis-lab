using Novolis.Astro.Assessment;
using Novolis.Astro.Catalog;
using Novolis.Astro.Routing;
using Novolis.Economy;
using Novolis.Economy.Logistics;
using Novolis.Economy.Simulation;

namespace NearSolPolity;

/// <summary>Maps Astro catalog + hop graph into Economy hubs and corridors.</summary>
internal static class AstroEconomyBridge
{
  /// <summary>
  /// Soft-SF tramp cruise: 1.3 days per light-year.
  /// Kernel corridors store hours = ceil(ly × days/ly × 24).
  /// </summary>
  public const double CruiseDaysPerLy = 1.3;

  /// <summary>Ly covered per simulation hour (derived from <see cref="CruiseDaysPerLy"/>).</summary>
  public const double CruiseLyPerHour = 1.0 / (CruiseDaysPerLy * 24.0);

  public const double MaxRangeLy = 12.0;
  public const decimal TollPerLy = 0.5m;
  public const decimal CorridorMaxCargo = 48m;

  /// <summary>Campaign seed for <see cref="SystemProfileGenerator"/> (matches <see cref="PolityWorld.Create"/> default).</summary>
  public const ulong CampaignSeed = 1001;

  public sealed record HubBinding(
    string SystemId,
    string Name,
    SystemRole Role,
    TransportHubId HubId,
    InventoryLocationId LocationId,
    TransportHub Hub,
    SystemProfile Profile);

  public sealed record BridgeResult(
    IReadOnlyList<HubBinding> Hubs,
    IReadOnlyList<TransportCorridor> Corridors,
    RouteGraph Graph,
    IReadOnlyDictionary<string, SystemRole> Roles,
    IReadOnlyDictionary<string, HubBinding> BySystemId,
    IReadOnlyDictionary<string, SystemProfile> Profiles);

  public static BridgeResult Build(StarCatalog catalog, EconomyWorldBuilder builder, ulong campaignSeed = CampaignSeed)
  {
    var cost = RangeBandCostModel.CreatePrototypeCompatible();
    var graph = RouteGraph.Build(catalog.All, MaxRangeLy, cost);
    var generator = new SystemProfileGenerator();
    var profiles = catalog.All.ToDictionary(
      s => s.Id.Value,
      s => generator.Generate(s, campaignSeed),
      StringComparer.OrdinalIgnoreCase);
    var roles = RoleAssigner.Assign(catalog, graph, profiles);

    // Only systems that appear on the hop graph become economic hubs.
    var onGraph = catalog.All
      .Where(s => graph.Adjacency.TryGetValue(s.Id.Value, out var edges) && edges.Count > 0)
      .OrderBy(s => s.Coords.DistanceFromOrigin)
      .ThenBy(s => s.Id.Value, StringComparer.Ordinal)
      .ToList();

    // Always include Sol even if somehow isolated.
    if (onGraph.All(s => !s.Id.Value.Equals("sol", StringComparison.OrdinalIgnoreCase)))
    {
      onGraph.Insert(0, catalog.GetRequired("sol"));
    }

    var hubs = new List<HubBinding>();
    var bySystem = new Dictionary<string, HubBinding>(StringComparer.OrdinalIgnoreCase);

    foreach (var s in onGraph)
    {
      var role = roles.GetValueOrDefault(s.Id.Value, SystemRole.Waypoint);
      var profile = profiles[s.Id.Value];
      var loc = InventoryLocationId.From(builder.NextGuid());
      var hubId = TransportHubId.From(builder.NextGuid());
      var (dwell, berths) = HubOps(role);
      var hub = new TransportHub(hubId, loc, s.Name, dwell, berths);
      var binding = new HubBinding(s.Id.Value, s.Name, role, hubId, loc, hub, profile);
      hubs.Add(binding);
      bySystem[s.Id.Value] = binding;
      builder.AddHub(hub);
    }

    var corridors = new List<TransportCorridor>();
    var seenUndirected = new HashSet<string>(StringComparer.Ordinal);

    foreach (var (fromId, edges) in graph.Adjacency.OrderBy(kv => kv.Key, StringComparer.Ordinal))
    {
      if (!bySystem.ContainsKey(fromId))
      {
        continue;
      }

      foreach (var edge in edges.OrderBy(e => e.To.Value, StringComparer.Ordinal))
      {
        if (!bySystem.ContainsKey(edge.To.Value))
        {
          continue;
        }

        var key = string.Compare(fromId, edge.To.Value, StringComparison.Ordinal) < 0
          ? $"{fromId}|{edge.To.Value}"
          : $"{edge.To.Value}|{fromId}";
        if (!seenUndirected.Add(key))
        {
          continue;
        }

        var a = bySystem[fromId];
        var b = bySystem[edge.To.Value];
        var hours = TransitHours(edge.DistanceLy);
        var difficulty = edge.BandTag is "long" ? 3m : 1m;
        var toll = Money.From(Math.Max(1m, (decimal)edge.DistanceLy * TollPerLy));

        var ab = new TransportCorridor(
          TransportCorridorId.From(builder.NextGuid()),
          a.HubId, b.HubId, hours, Quantity.From(CorridorMaxCargo), difficulty, toll);
        var ba = new TransportCorridor(
          TransportCorridorId.From(builder.NextGuid()),
          b.HubId, a.HubId, hours, Quantity.From(CorridorMaxCargo), difficulty, toll);
        builder.AddCorridor(ab);
        builder.AddCorridor(ba);
        corridors.Add(ab);
        corridors.Add(ba);
      }
    }

    return new BridgeResult(hubs, corridors, graph, roles, bySystem, profiles);
  }

  public static long TransitHours(double distanceLy) =>
    Math.Max(1, (long)Math.Ceiling(distanceLy / CruiseLyPerHour));

  /// <summary>Transit duration in whole/fractional days (for UI).</summary>
  public static double TransitDays(double distanceLy) =>
    TransitHours(distanceLy) / 24.0;

  private static (long Dwell, int Berths) HubOps(SystemRole role) => role switch
  {
    // Time capacity: Capital dwell/berths bind unload rate.
    SystemRole.Capital => (3, 5),
    SystemRole.Industrial => (2, 4),
    SystemRole.Inhabited => (2, 3),
    SystemRole.Mining => (3, 2),
    SystemRole.Transit => (1, 5),
    _ => (2, 2),
  };
}
