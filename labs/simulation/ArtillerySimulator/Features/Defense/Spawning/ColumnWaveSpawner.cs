using System.Numerics;

namespace ArtillerySimulator;

internal static class ColumnWaveSpawner
{
    public static void SpawnWave(DefenseBattlefieldState state)
    {
        if (state.SpawnRoutes.Count == 0)
            return;

        state.WaveSerial++;
        var columns = OpforDifficultyCurve.ColumnsThisWave(state);
        var vehicles = OpforDifficultyCurve.VehiclesPerColumn(state);
        var spawned = 0;
        for (var c = 0; c < columns; c++)
        {
            var idx = OpforSpawnCatalog.DrawSpawnIndex(state);
            if (idx < 0)
                break;
            if (TrySpawnEngageableColumn(state, state.SpawnRoutes[idx], vehicles))
                spawned++;
        }

        for (var guard = 0; spawned < columns && guard < state.SpawnRoutes.Count; guard++)
        {
            var idx = OpforSpawnCatalog.DrawSpawnIndex(state);
            if (idx < 0)
                break;
            if (TrySpawnEngageableColumn(state, state.SpawnRoutes[idx], vehicles))
                spawned++;
        }
    }

    public static bool TrySpawnEngageableColumn(DefenseBattlefieldState state, GroundPolyline template, int count)
    {
        var line = new GroundPolyline(template.Name, template.Kind, (Vector2[])template.Points.Clone());
        var gun = new Vector2(state.GunX, state.GunZ);
        var goal = new Vector2(state.Objective.X, state.Objective.Z);
        var ring = SimulationUnits.MinOpeningEngagementMeters(state.Piece);
        var minOpen = ring * 0.92f;
        var maxOpen = MathF.Min(SimulationUnits.MaxCityApproachMeters, ring * 1.4f);
        var minCity = DefenseBattlefieldState.ObjectiveRadiusMeters + 900f;

        var legal = new List<(float Arc, float RingErr, float ObjDist)>();
        for (var arc = 40f; arc <= line.Length - 40f; arc += 30f)
        {
            var p = line.At(arc);
            if (!PlayableMapBounds.InPlayableMap(state.Terrain, p.X, p.Y))
                continue;
            if (KristiansandSurface.KindAt(p.X, p.Y) == KristiansandSurfaceKind.Water)
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

        var aPlus = Math.Clamp(leadArc + 80f, 40f, line.Length - 40f);
        var aMinus = Math.Clamp(leadArc - 80f, 40f, line.Length - 40f);
        var away = Vector2.Distance(line.At(aPlus), goal) >= Vector2.Distance(line.At(aMinus), goal) ? 1 : -1;

        var spacing = MathF.Max(45f, DefenseBattlefieldState.SpacingMeters - (state.Round - 1) * 4f);
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
            Ribbon = DefenseSceneDraw.BuildRibbon(state.Terrain, line),
        });
        if (!state.Approaches.Contains(line.Name))
            state.Approaches.Add(line.Name);

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
            if (!PlayableMapBounds.InPlayableMap(state.Terrain, point.X, point.Y))
                continue;
            if (KristiansandSurface.KindAt(point.X, point.Y) == KristiansandSurfaceKind.Water)
                continue;
            if (Vector2.Distance(point, gun) < minOpen * 0.75f)
                continue;

            var unit = new ColumnUnit
            {
                Id = nextId++,
                Lane = lane,
                Arc = arc,
                Alive = true,
                Posture = "advancing",
            };
            ColumnConvoySimulator.PlaceOnLane(state, state.Lanes[lane], ref unit);
            state.Units.Add(unit);
        }

        return state.Units.Count > before;
    }
}
