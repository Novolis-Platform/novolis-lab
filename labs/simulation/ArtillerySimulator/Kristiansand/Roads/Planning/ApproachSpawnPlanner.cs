using System.Numerics;

namespace ArtillerySimulator;

internal static class ApproachSpawnPlanner
{
    /// <summary>
    /// Builds up to <paramref name="count"/> primary-road approach polylines whose outer tips sit
    /// near <paramref name="ringMeters"/> from the gun and path to <paramref name="goal"/>.
    /// Spawns are angularly spread; never straight-line bearings.
    /// </summary>
    public static IReadOnlyList<GroundPolyline> BuildApproachSpawns(
        Vector2 goal,
        Vector2 gun,
        float ringMeters,
        int count,
        float minLengthMeters = 1_200f,
        float mapEastMeters = float.PositiveInfinity,
        float mapNorthMeters = float.PositiveInfinity,
        float mapMarginMeters = 120f)
    {
        NvdbPrimaryRoadGraphBuilder.EnsureBuilt();
        var nodes = NvdbPrimaryRoadGraphBuilder.Nodes;
        var inMain = NvdbPrimaryRoadGraphBuilder.InMainComponent;
        if (nodes.Length == 0 || count <= 0)
            return [];

        var end = RoadGraphPathfinder.NearestMainNode(goal);
        if (end < 0)
            return [];

        var (distFromGoal, prev) = RoadGraphPathfinder.Dijkstra(end);
        var bandInner = ringMeters * 0.92f;
        var bandOuter = ringMeters * 1.4f;
        var candidates = new List<(int Node, float Angle, float GunDist)>(512);

        bool InMap(Vector2 p) =>
            p.X >= mapMarginMeters
            && p.Y >= mapMarginMeters
            && p.X <= mapEastMeters - mapMarginMeters
            && p.Y <= mapNorthMeters - mapMarginMeters;

        for (var i = 0; i < nodes.Length; i++)
        {
            if (!inMain[i] || float.IsPositiveInfinity(distFromGoal[i]))
                continue;

            var p = nodes[i].Pos;
            if (!InMap(p))
                continue;

            var gd = Vector2.Distance(p, gun);
            if (gd < bandInner || gd > bandOuter)
                continue;

            if (Vector2.Distance(p, goal) < 1_200f)
                continue;

            var angle = MathF.Atan2(p.Y - gun.Y, p.X - gun.X);
            candidates.Add((i, angle, gd));
        }

        if (candidates.Count == 0)
            return [];

        candidates.Sort((a, b) => MathF.Abs(a.GunDist - ringMeters).CompareTo(MathF.Abs(b.GunDist - ringMeters)));

        var picked = new List<(int Node, float Angle)>(count);
        foreach (var c in candidates)
        {
            if (picked.Count >= count)
                break;

            var ok = true;
            foreach (var p in picked)
            {
                if (BearingMath.AngularDistance(c.Angle, p.Angle) < MathF.Tau / (count * 1.35f))
                {
                    ok = false;
                    break;
                }
            }

            if (!ok && picked.Count > 0)
                continue;

            picked.Add((c.Node, c.Angle));
        }

        if (picked.Count < count)
        {
            foreach (var c in candidates)
            {
                if (picked.Count >= count)
                    break;
                if (picked.Exists(p => p.Node == c.Node))
                    continue;
                picked.Add((c.Node, c.Angle));
            }
        }

        var routes = new List<GroundPolyline>(picked.Count);
        for (var i = 0; i < picked.Count; i++)
        {
            var path = RoadGraphPathfinder.Reconstruct(prev, picked[i].Node, end);
            if (path is null || path.Count < 2)
                continue;

            var points = RoadGraphPathfinder.Decimate(path, 40f);
            if (Vector2.Distance(points[0], goal) < Vector2.Distance(points[^1], goal))
                Array.Reverse(points);

            points = RoadPolylineClip.LongestInMapRun(points, InMap);
            if (points.Length < 2)
                continue;

            var line = new GroundPolyline($"Approach {i + 1}", "primary", points);
            if (line.Length < minLengthMeters)
                continue;
            if (!InMap(line.Points[0]))
                continue;

            routes.Add(line);
        }

        return routes;
    }
}
