using System.Numerics;

namespace ArtillerySimulator;

internal static class OpforSpawnCatalog
{
    public static void BuildSpawnCatalog(DefenseBattlefieldState state)
    {
        var goal = new Vector2(state.Objective.X, state.Objective.Z);
        var gun = new Vector2(state.GunX, state.GunZ);
        var ring = SimulationUnits.MinOpeningEngagementMeters(state.Piece);
        var routes = PrimaryRoadNetwork.BuildApproachSpawns(
            goal,
            gun,
            ring,
            SimulationUnits.ApproachSpawnCount,
            mapEastMeters: state.Terrain.EastMeters,
            mapNorthMeters: state.Terrain.NorthMeters,
            mapMarginMeters: PlayableMapBounds.MapMarginMeters);

        foreach (var route in routes)
        {
            if (IsUsableApproach(state.Terrain, route))
                state.SpawnRoutes.Add(route);
        }

        if (state.SpawnRoutes.Count == 0)
        {
            foreach (var name in new[] { "Strai", "Hellemyr", "Søm", "Vågsbygd", "Møvik" })
            {
                var line = PrimaryRoadNetwork.RouteFromSeed(goal, ApproachSeed(name), name)
                    ?? PrimaryRoadNetwork.RouteToward(goal, ApproachBearing(name), name);
                if (line is not null && IsUsableApproach(state.Terrain, line))
                    state.SpawnRoutes.Add(line);
            }
        }

        Console.WriteLine(
            $"OPFOR spawn catalog: {state.SpawnRoutes.Count} on-map primary approaches at ~{ring / 1000f:F2} km");
        ReshuffleSpawnDeck(state);
    }

    public static bool IsUsableApproach(TerrainWorld terrain, GroundPolyline line)
    {
        if (line.Points.Length < 2 || line.Length < 1_000f)
            return false;
        if (!PlayableMapBounds.InPlayableMap(terrain, line.Points[0].X, line.Points[0].Y))
            return false;
        if (KristiansandSurface.KindAt(line.Points[0].X, line.Points[0].Y) == KristiansandSurfaceKind.Water)
            return false;
        return true;
    }

    public static void ReshuffleSpawnDeck(DefenseBattlefieldState state)
    {
        state.SpawnDeck.Clear();
        for (var i = 0; i < state.SpawnRoutes.Count; i++)
            state.SpawnDeck.Add(i);
        for (var i = state.SpawnDeck.Count - 1; i > 0; i--)
        {
            var j = state.Rng.Next(i + 1);
            (state.SpawnDeck[i], state.SpawnDeck[j]) = (state.SpawnDeck[j], state.SpawnDeck[i]);
        }
    }

    public static int DrawSpawnIndex(DefenseBattlefieldState state)
    {
        if (state.SpawnRoutes.Count == 0)
            return -1;
        if (state.SpawnDeck.Count == 0)
            ReshuffleSpawnDeck(state);
        var last = state.SpawnDeck.Count - 1;
        var idx = state.SpawnDeck[last];
        state.SpawnDeck.RemoveAt(last);
        return idx;
    }

    private static float ApproachBearing(string roadToken) => roadToken switch
    {
        "Strai" => 128f,
        "Hellemyr" => 165f,
        "Søm" => 12f,
        "Møvik" or "Movik" => 235f,
        "Vågsbygd" or "Vagsbygd" => 245f,
        _ => 90f,
    };

    private static Vector2 ApproachSeed(string roadToken)
    {
        var (lat, lon) = roadToken switch
        {
            "Strai" => (58.1893, 7.92826),
            "Hellemyr" => (58.1680, 7.9450),
            "Søm" => (58.1520, 8.0780),
            "Møvik" or "Movik" => (58.1250, 7.9550),
            "Vågsbygd" or "Vagsbygd" => (58.1320, 7.9480),
            _ => (58.1700, 7.9600),
        };
        return KristiansandHeightSampler.ToWorldMeters(lat, lon);
    }
}
