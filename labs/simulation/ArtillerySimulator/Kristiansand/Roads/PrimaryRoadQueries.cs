using System.Numerics;

namespace ArtillerySimulator;

internal static class PrimaryRoadQueries
{
    /// <summary>
    /// Shortest primary-road path from a geographic approach seed toward <paramref name="goal"/>.
    /// Points are world XZ (Vector2.Y = world Z). Returns null if unreachable.
    /// </summary>
    public static GroundPolyline? RouteFromSeed(
        Vector2 goal,
        Vector2 approachSeed,
        string name,
        float minLengthMeters = 1_200f,
        float maxSeedSnapMeters = 2_400f)
    {
        NvdbPrimaryRoadGraphBuilder.EnsureBuilt();
        var nodes = NvdbPrimaryRoadGraphBuilder.Nodes;
        if (nodes.Length == 0)
            return null;

        var end = RoadGraphPathfinder.NearestMainNode(goal);
        var start = RoadGraphPathfinder.NearestMainNode(approachSeed);
        if (end < 0 || start < 0 || start == end)
            return null;

        if (Vector2.Distance(nodes[start].Pos, approachSeed) > maxSeedSnapMeters)
            return null;

        if (Vector2.Distance(nodes[start].Pos, goal) < 1_400f)
            return null;

        var (distFromGoal, prev) = RoadGraphPathfinder.Dijkstra(end);
        if (float.IsPositiveInfinity(distFromGoal[start]))
            return null;

        var path = RoadGraphPathfinder.Reconstruct(prev, start, end);
        if (path is null || path.Count < 2)
            return null;

        var points = RoadGraphPathfinder.Decimate(path, 40f);
        if (Vector2.Distance(points[0], goal) < Vector2.Distance(points[^1], goal))
            Array.Reverse(points);

        var line = new GroundPolyline(name, "primary", points);
        return line.Length >= minLengthMeters ? line : null;
    }

    /// <summary>
    /// Shortest primary-road path from a far approach bearing toward <paramref name="goal"/>.
    /// Fallback when no geographic seed is available.
    /// </summary>
    public static GroundPolyline? RouteToward(Vector2 goal, float approachBearingDegrees, string name, float minLengthMeters = 1_200f)
    {
        NvdbPrimaryRoadGraphBuilder.EnsureBuilt();
        var nodes = NvdbPrimaryRoadGraphBuilder.Nodes;
        if (nodes.Length == 0)
            return null;

        var rad = approachBearingDegrees * (MathF.PI / 180f);
        var dir = new Vector2(MathF.Cos(rad), MathF.Sin(rad));

        var end = RoadGraphPathfinder.NearestMainNode(goal);
        if (end < 0)
            return null;

        var (distFromGoal, prev) = RoadGraphPathfinder.Dijkstra(end);
        var start = -1;
        var bestScore = float.MaxValue;
        for (var i = 0; i < nodes.Length; i++)
        {
            if (float.IsPositiveInfinity(distFromGoal[i]))
                continue;

            var n = nodes[i].Pos;
            var along = Vector2.Dot(n - goal, dir);
            if (along < 1_800f)
                continue;

            var off = BearingMath.DistancePointToRay(n, goal, dir);
            if (off > 3_200f)
                continue;

            var score = off * 3.2f + MathF.Abs(along - 6_000f) * 0.12f - along * 0.02f;
            if (score >= bestScore)
                continue;

            bestScore = score;
            start = i;
        }

        if (start < 0)
            return null;

        var path = RoadGraphPathfinder.Reconstruct(prev, start, end);
        if (path is null || path.Count < 2)
            return null;

        var points = RoadGraphPathfinder.Decimate(path, 40f);
        if (Vector2.Distance(points[0], goal) < Vector2.Distance(points[^1], goal))
            Array.Reverse(points);

        var line = new GroundPolyline(name, "primary", points);
        return line.Length >= minLengthMeters ? line : null;
    }

    public static Vector2? Snap(Vector2 world)
    {
        NvdbPrimaryRoadGraphBuilder.EnsureBuilt();
        var nodes = NvdbPrimaryRoadGraphBuilder.Nodes;
        var i = RoadGraphPathfinder.NearestNode(world);
        return i < 0 ? null : nodes[i].Pos;
    }
}
