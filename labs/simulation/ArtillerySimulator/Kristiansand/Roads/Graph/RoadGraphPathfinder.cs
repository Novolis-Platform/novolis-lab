using System.Numerics;

namespace ArtillerySimulator;

internal static class RoadGraphPathfinder
{
    public static (float[] Dist, int[] Prev) Dijkstra(int source)
    {
        var nodes = NvdbPrimaryRoadGraphBuilder.Nodes;
        var adj = NvdbPrimaryRoadGraphBuilder.Adjacency;
        var dist = new float[nodes.Length];
        var prev = new int[nodes.Length];
        Array.Fill(dist, float.PositiveInfinity);
        Array.Fill(prev, -1);
        dist[source] = 0f;

        var open = new PriorityQueue<int, float>();
        open.Enqueue(source, 0f);
        while (open.TryDequeue(out var u, out var priority))
        {
            if (priority > dist[u] + 1e-3f)
                continue;

            foreach (var (v, w) in adj[u])
            {
                var nd = dist[u] + w;
                if (nd + 1e-3f >= dist[v])
                    continue;
                dist[v] = nd;
                prev[v] = u;
                open.Enqueue(v, nd);
            }
        }

        return (dist, prev);
    }

    /// <summary>Walk prev[] from <paramref name="start"/> toward <paramref name="end"/> (prev rooted at end).</summary>
    public static List<int>? Reconstruct(int[] prevFromEnd, int start, int end)
    {
        var nodes = NvdbPrimaryRoadGraphBuilder.Nodes;
        if (start == end)
            return [start];

        var path = new List<int>();
        for (var at = start; at >= 0; at = prevFromEnd[at])
        {
            path.Add(at);
            if (at == end)
                break;
            if (path.Count > nodes.Length)
                return null;
        }

        if (path[^1] != end)
            return null;
        return path;
    }

    public static Vector2[] Decimate(List<int> path, float minStep)
    {
        var nodes = NvdbPrimaryRoadGraphBuilder.Nodes;
        var list = new List<Vector2>(path.Count);
        list.Add(nodes[path[0]].Pos);
        var last = list[0];
        for (var i = 1; i < path.Count - 1; i++)
        {
            var p = nodes[path[i]].Pos;
            if (Vector2.Distance(last, p) < minStep)
                continue;
            list.Add(p);
            last = p;
        }

        var end = nodes[path[^1]].Pos;
        if (Vector2.Distance(last, end) > 1f)
            list.Add(end);
        return list.ToArray();
    }

    public static int NearestNode(Vector2 p)
    {
        var nodes = NvdbPrimaryRoadGraphBuilder.Nodes;
        var best = -1;
        var bestD = float.MaxValue;
        for (var i = 0; i < nodes.Length; i++)
        {
            var d = Vector2.DistanceSquared(nodes[i].Pos, p);
            if (d >= bestD)
                continue;
            bestD = d;
            best = i;
        }

        return best;
    }

    public static int NearestMainNode(Vector2 p)
    {
        var nodes = NvdbPrimaryRoadGraphBuilder.Nodes;
        var inMain = NvdbPrimaryRoadGraphBuilder.InMainComponent;
        var best = -1;
        var bestD = float.MaxValue;
        for (var i = 0; i < nodes.Length; i++)
        {
            if (!inMain[i])
                continue;
            var d = Vector2.DistanceSquared(nodes[i].Pos, p);
            if (d >= bestD)
                continue;
            bestD = d;
            best = i;
        }

        return best;
    }
}
