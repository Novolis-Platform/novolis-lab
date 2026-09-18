using Novolis.Astro.Assessment;
using Novolis.Economy;
using Novolis.Economy.Logistics;
using Novolis.Economy.Simulation;

namespace NearSolPolity;

/// <summary>Seed-time invariants for potential-gated NearSol geography.</summary>
internal static class SeedInvariants
{
  public static void Assert(PolityWorld.Ids ids, EconomySimulation sim)
  {
    var failures = new List<string>();

    foreach (var hub in ids.Bridge.Hubs)
    {
      var agri = hub.Profile.Potential.Agriculture;
      var mining = hub.Profile.Potential.Mining;

      if (hub.Role == SystemRole.Mining && mining < RoleAssigner.MiningThreshold)
      {
        failures.Add($"Mining hub {hub.SystemId} has Mining={mining:0.###} < {RoleAssigner.MiningThreshold}");
      }

      if (hub.Role is SystemRole.Capital or SystemRole.Inhabited or SystemRole.Industrial)
      {
        if (agri <= 0)
        {
          failures.Add($"Settlement hub {hub.SystemId} ({hub.Role}) has Agriculture={agri:0.###}");
        }
      }
    }

    // Map cohort area → hub via facility Area bindings created at seed.
    var areaToHub = new Dictionary<GeographicAreaId, AstroEconomyBridge.HubBinding>();
    foreach (var site in ids.Sites.Values)
    {
      foreach (var fac in sim.State.World.Facilities.Values)
      {
        if (fac.Area is { } area && fac.StorageLocation.Equals(site.Hub.LocationId))
        {
          areaToHub[area] = site.Hub;
        }
      }
    }

    foreach (var cohort in sim.State.World.Cohorts)
    {
      if (!areaToHub.TryGetValue(cohort.Definition.Area, out var hub))
      {
        failures.Add($"Cohort {cohort.Definition.Id.Value:N} area has no hub binding");
        continue;
      }

      if (hub.Profile.Potential.Agriculture == 0
          && hub.Role is not SystemRole.Mining)
      {
        failures.Add($"Cohort on barren system {hub.SystemId}");
      }
    }

    if (failures.Count > 0)
    {
      throw new InvalidOperationException(
        "NearSol seed invariants failed:" + Environment.NewLine + string.Join(Environment.NewLine, failures));
    }
  }
}
