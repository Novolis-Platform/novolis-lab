using System.Numerics;

namespace ArtillerySimulator;

/// <summary>Spawns invasion ships on invisible fairways (water-only).</summary>
internal static class NavalWaveSpawner
{
    public const float BaseShipSpeedMetersPerSecond = 7.5f; // ~14.5 kn — channel pace
    public const float ShipSpacingMeters = 180f;

    public static void ApplyRoundDifficulty(DefenseBattlefieldState state) =>
        state.ColumnSpeed = BaseShipSpeedMetersPerSecond * (1f + 0.12f * (state.Round - 1));

    public static float WaveIntervalSeconds(DefenseBattlefieldState state) =>
        Math.Clamp(110f - (state.Round - 1) * 10f, 45f, 110f);

    public static int ColumnsThisWave(DefenseBattlefieldState state) =>
        Math.Clamp(1 + (state.Round - 1) / 3 + (state.WaveSerial % 4 == 0 ? 1 : 0), 1, 3);

    public static int ShipsPerColumn(DefenseBattlefieldState state) =>
        Math.Clamp(2 + state.Round / 2, 2, 5);

    public static void SeedFairways(DefenseBattlefieldState state)
    {
        state.SpawnRoutes.Clear();
        state.Approaches.Clear();
        foreach (var fairway in OslofjordenShipLanes.BuildFairways())
        {
            if (fairway.Points.Length < 2 || fairway.Length < 2_000f)
                continue;
            state.SpawnRoutes.Add(fairway);
            if (!state.Approaches.Contains(fairway.Name))
                state.Approaches.Add(fairway.Name);
        }

        OpforSpawnCatalog.ReshuffleSpawnDeck(state);
        Console.WriteLine($"Naval fairways: {state.SpawnRoutes.Count} invisible ship paths (roads are decorative only)");
    }

    public static void SpawnWave(DefenseBattlefieldState state)
    {
        if (state.SpawnRoutes.Count == 0)
            return;

        state.WaveSerial++;
        var columns = ColumnsThisWave(state);
        var ships = ShipsPerColumn(state);
        var spawned = 0;
        for (var c = 0; c < columns; c++)
        {
            var idx = OpforSpawnCatalog.DrawSpawnIndex(state);
            if (idx < 0)
                break;
            if (TrySpawnColumn(state, state.SpawnRoutes[idx], ships))
                spawned++;
        }

        for (var guard = 0; spawned < columns && guard < state.SpawnRoutes.Count; guard++)
        {
            var idx = OpforSpawnCatalog.DrawSpawnIndex(state);
            if (idx < 0)
                break;
            if (TrySpawnColumn(state, state.SpawnRoutes[idx], ships))
                spawned++;
        }
    }

    public static bool TrySpawnColumn(DefenseBattlefieldState state, GroundPolyline template, int count)
    {
        var line = new GroundPolyline(template.Name, template.Kind, (Vector2[])template.Points.Clone());
        var gun = new Vector2(state.GunX, state.GunZ);
        var goal = new Vector2(state.Objective.X, state.Objective.Z);
        // Southern invasion: fix the engagement band to the Sound geometry (not howitzer HE-min × 2.25).
        float minOpen;
        float maxOpen;
        float ring;
        if (state.SouthernApproachesOnly)
        {
            // Longer southern box: start the fleet farther out so Oscarsborg has more clock.
            minOpen = 3_200f;
            maxOpen = 16_500f;
            ring = 9_000f;
        }
        else
        {
            ring = SimulationUnits.MinOpeningEngagementMeters(state.Piece);
            minOpen = ring * 0.85f;
            maxOpen = MathF.Min(SimulationUnits.MaxCityApproachMeters, ring * 1.55f);
        }

        var minCity = DefenseBattlefieldState.ObjectiveRadiusMeters + 1_200f;
        // Stay south of the fort so columns steam north through the pinch.
        var southGate = state.SouthernApproachesOnly ? gun.Y - 600f : float.PositiveInfinity;

        var legal = new List<(float Arc, float RingErr, float ObjDist)>();
        for (var arc = 40f; arc <= line.Length - 40f; arc += 40f)
        {
            var p = line.At(arc);
            if (!PlayableMapBounds.InPlayableMap(state.Terrain, p.X, p.Y))
                continue;
            // Ships live on water — opposite of the Kristiansand road rule.
            if (!state.Terrain.Profile.IsWater(p.X, p.Y))
                continue;
            if (state.SouthernApproachesOnly && p.Y > southGate)
                continue;
            var gd = Vector2.Distance(p, gun);
            if (gd < minOpen || gd > maxOpen)
                continue;
            var od = Vector2.Distance(p, goal);
            if (od < minCity)
                continue;
            legal.Add((arc, MathF.Abs(gd - ring), od));
        }

        if (legal.Count < 2)
            return false;

        legal.Sort(static (a, b) =>
        {
            var c = a.RingErr.CompareTo(b.RingErr);
            return c != 0 ? c : a.ObjDist.CompareTo(b.ObjDist);
        });
        var leadArc = legal[0].Arc;

        var aPlus = Math.Clamp(leadArc + 120f, 40f, line.Length - 40f);
        var aMinus = Math.Clamp(leadArc - 120f, 40f, line.Length - 40f);
        // Southern approaches: Away=+1 follows increasing arc (south → north along the fairway).
        var away = state.SouthernApproachesOnly
            ? 1
            : (Vector2.Distance(line.At(aPlus), goal) >= Vector2.Distance(line.At(aMinus), goal) ? 1 : -1);

        var spacing = MathF.Max(140f, ShipSpacingMeters - (state.Round - 1) * 8f);
        var pack = (count - 1) * spacing;
        var tail = leadArc + away * pack;
        if (tail < 40f || tail > line.Length - 40f)
            leadArc = Math.Clamp(leadArc - away * pack, 40f, line.Length - 40f);

        var end = line.Points[^1];
        var start = line.Points[0];
        var crossingArc = Vector2.Distance(end, goal) <= Vector2.Distance(start, goal)
            ? line.Length - 40f
            : 40f;

        var lane = state.Lanes.Count;
        state.Lanes.Add(new RouteLane
        {
            Line = line,
            CrossingArc = crossingArc,
            Away = away,
            Name = line.Name,
            Ribbon = [], // invisible fairways — never draw ribbons
        });

        var before = state.Units.Count;
        var used = new HashSet<int>();
        var nextId = state.Units.Count + state.Wrecks.Count + 1;
        for (var i = 0; i < count; i++)
        {
            var arc = Math.Clamp(leadArc + away * i * spacing, 40f, line.Length - 40f);
            var key = (int)MathF.Round(arc);
            if (!used.Add(key))
                continue;

            var point = line.At(arc);
            if (!state.Terrain.Profile.IsWater(point.X, point.Y))
                continue;
            if (Vector2.Distance(point, gun) < minOpen * 0.7f)
                continue;

            var unit = new ColumnUnit
            {
                Id = nextId++,
                Lane = lane,
                Arc = arc,
                Alive = true,
                Posture = "advancing",
            };
            PlaceOnWater(state, state.Lanes[lane], ref unit);
            state.Units.Add(unit);
        }

        return state.Units.Count > before;
    }

    public static void PlaceOnWater(DefenseBattlefieldState state, RouteLane lane, ref ColumnUnit unit)
    {
        var point = lane.Line.At(unit.Arc);
        var probe = lane.Line.At(unit.Arc - lane.Away * 24f);
        var forward = probe - point;
        if (forward.LengthSquared() < 1f)
            forward = new Vector2(lane.Away, 0f);
        forward = Vector2.Normalize(forward);
        var side = new Vector2(-forward.Y, forward.X);
        var placed = point + side * unit.Offset;
        unit.X = PlayableMapBounds.ClampX(state.Terrain, placed.X);
        unit.Z = PlayableMapBounds.ClampZ(state.Terrain, placed.Y);
    }
}
