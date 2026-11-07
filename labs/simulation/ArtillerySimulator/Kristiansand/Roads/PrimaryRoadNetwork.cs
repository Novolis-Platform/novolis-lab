using System.Numerics;

namespace ArtillerySimulator;

/// <summary>
/// Graph of <strong>primary</strong> roads from <c>roads.png</c> (Norgeskart red ink).
/// Hostile columns pathfind on this network toward Kvadraturen.
/// </summary>
internal static class PrimaryRoadNetwork
{
    private const float QuantizeMeters = 20f;
    private const float BridgeMeters = 55f;
    private const float SampleMeters = 20f;

    private static bool _built;
    private static Node[] _nodes = [];
    private static List<(int To, float W)>[] _adj = [];
    private static bool[] _inMain = [];

    public static void Reset()
    {
        _built = false;
        _nodes = [];
        _adj = [];
        _inMain = [];
    }

    /// <summary>
    /// Shortest primary-road path from a far approach toward <paramref name="goal"/>.
    /// Points are world XZ (Vector2.Y = world Z). Returns null if unreachable.
    /// </summary>
    public static GroundPolyline? RouteToward(Vector2 goal, float approachBearingDegrees, string name, float minLengthMeters = 1_200f)
    {
        EnsureBuilt();
        if (_nodes.Length == 0)
            return null;

        var rad = approachBearingDegrees * (MathF.PI / 180f);
        var fromX = MathF.Cos(rad);
        var fromZ = MathF.Sin(rad);
        var east = KristiansandHeightSampler.EastMeters;
        var north = KristiansandHeightSampler.NorthMeters;

        // Snap downtown onto the main primary component (not a tiny disconnected stub).
        var end = NearestMainNode(goal);
        if (end < 0)
            return null;

        // One Dijkstra from downtown — pick the farthest reachable seed in the approach sector.
        var (distFromGoal, prev) = Dijkstra(end);
        var dir = new Vector2(fromX, fromZ);
        var start = -1;
        var bestScore = float.MaxValue;
        for (var i = 0; i < _nodes.Length; i++)
        {
            if (float.IsPositiveInfinity(distFromGoal[i]))
                continue;

            var n = _nodes[i].Pos;
            var along = Vector2.Dot(n - goal, dir);
            if (along < 1_800f)
                continue;

            var off = DistancePointToRay(n, goal, dir);
            if (off > 4_500f)
                continue;

            // Prefer ~6–10 km out, on-bearing.
            var score = off * 2.8f + MathF.Abs(along - 8_000f) * 0.1f - along * 0.02f;
            if (score >= bestScore)
                continue;

            bestScore = score;
            start = i;
        }

        if (start < 0)
            return null;

        var path = Reconstruct(prev, start, end);
        if (path is null || path.Count < 2)
            return null;

        var points = Decimate(path, 40f);
        if (Vector2.Distance(points[0], goal) < Vector2.Distance(points[^1], goal))
            Array.Reverse(points);

        var line = new GroundPolyline(name, "primary", points);
        return line.Length >= minLengthMeters ? line : null;
    }

    public static Vector2? Snap(Vector2 world)
    {
        EnsureBuilt();
        var i = NearestNode(world);
        return i < 0 ? null : _nodes[i].Pos;
    }

    private static void EnsureBuilt()
    {
        if (_built)
            return;
        _built = true;

        var primaries = KristiansandPdfRoads.PrimarySegments();
        if (primaries.Count == 0)
            return;

        var indexOf = new Dictionary<long, int>(65_536);
        var nodes = new List<Node>(32_768);
        var undirected = new HashSet<long>();
        var edges = new List<(int A, int B, float Len)>(131_072);

        int Ensure(Vector2 p)
        {
            var key = QuantKey(p);
            if (indexOf.TryGetValue(key, out var id))
                return id;
            id = nodes.Count;
            indexOf[key] = id;
            nodes.Add(new Node(Dequant(key)));
            return id;
        }

        void Link(int a, int b)
        {
            if (a == b)
                return;
            var lo = Math.Min(a, b);
            var hi = Math.Max(a, b);
            var key = ((long)lo << 32) ^ (uint)hi;
            if (!undirected.Add(key))
                return;
            var len = Vector2.Distance(nodes[a].Pos, nodes[b].Pos);
            if (len < 0.5f)
                return;
            edges.Add((a, b, len));
        }

        foreach (var (a, b) in primaries)
        {
            var dx = b.X - a.X;
            var dz = b.Y - a.Y;
            var len = MathF.Sqrt(dx * dx + dz * dz);
            if (len < 1f)
                continue;

            var samples = Math.Max(1, (int)MathF.Ceiling(len / SampleMeters));
            var prev = Ensure(a);
            for (var s = 1; s <= samples; s++)
            {
                var t = s / (float)samples;
                var p = new Vector2(a.X + dx * t, a.Y + dz * t);
                var next = Ensure(p);
                Link(prev, next);
                prev = next;
            }
        }

        // Bridge hairline gaps between traced components.
        var cell = new Dictionary<long, List<int>>();
        for (var i = 0; i < nodes.Count; i++)
        {
            var key = QuantKey(nodes[i].Pos);
            if (!cell.TryGetValue(key, out var bucket))
            {
                bucket = [];
                cell[key] = bucket;
            }

            bucket.Add(i);
        }

        var bridgeCells = Math.Max(1, (int)MathF.Ceiling(BridgeMeters / QuantizeMeters));
        for (var i = 0; i < nodes.Count; i++)
        {
            var p = nodes[i].Pos;
            var cx = (int)MathF.Round(p.X / QuantizeMeters);
            var cz = (int)MathF.Round(p.Y / QuantizeMeters);
            for (var dx = -bridgeCells; dx <= bridgeCells; dx++)
            {
                for (var dz = -bridgeCells; dz <= bridgeCells; dz++)
                {
                    var key = ((long)(cx + dx) << 32) ^ (uint)(cz + dz);
                    if (!cell.TryGetValue(key, out var bucket))
                        continue;
                    foreach (var j in bucket)
                    {
                        if (j <= i)
                            continue;
                        var d = Vector2.Distance(nodes[i].Pos, nodes[j].Pos);
                        if (d > 0.5f && d <= BridgeMeters)
                            Link(i, j);
                    }
                }
            }
        }

        _nodes = nodes.ToArray();
        _adj = new List<(int, float)>[_nodes.Length];
        for (var i = 0; i < _adj.Length; i++)
            _adj[i] = [];
        foreach (var (a, b, len) in edges)
        {
            _adj[a].Add((b, len));
            _adj[b].Add((a, len));
        }

        MarkLargestComponent();
        var mainCount = 0;
        for (var i = 0; i < _inMain.Length; i++)
        {
            if (_inMain[i])
                mainCount++;
        }

        _ = mainCount;
    }

    private static void MarkLargestComponent()
    {
        _inMain = new bool[_nodes.Length];
        var seen = new bool[_nodes.Length];
        var bestRoot = -1;
        var bestSize = 0;
        var bestMembers = Array.Empty<int>();

        for (var root = 0; root < _nodes.Length; root++)
        {
            if (seen[root])
                continue;

            var stack = new Stack<int>();
            var members = new List<int>();
            stack.Push(root);
            seen[root] = true;
            while (stack.Count > 0)
            {
                var u = stack.Pop();
                members.Add(u);
                foreach (var (v, _) in _adj[u])
                {
                    if (seen[v])
                        continue;
                    seen[v] = true;
                    stack.Push(v);
                }
            }

            if (members.Count <= bestSize)
                continue;
            bestSize = members.Count;
            bestRoot = root;
            bestMembers = members.ToArray();
        }

        _ = bestRoot;
        foreach (var i in bestMembers)
            _inMain[i] = true;
    }

    private static Vector2[] Decimate(List<int> path, float minStep)
    {
        var list = new List<Vector2>(path.Count);
        list.Add(_nodes[path[0]].Pos);
        var last = list[0];
        for (var i = 1; i < path.Count - 1; i++)
        {
            var p = _nodes[path[i]].Pos;
            if (Vector2.Distance(last, p) < minStep)
                continue;
            list.Add(p);
            last = p;
        }

        var end = _nodes[path[^1]].Pos;
        if (Vector2.Distance(last, end) > 1f)
            list.Add(end);
        return list.ToArray();
    }

    private static int NearestNode(Vector2 p)
    {
        var best = -1;
        var bestD = float.MaxValue;
        for (var i = 0; i < _nodes.Length; i++)
        {
            var d = Vector2.DistanceSquared(_nodes[i].Pos, p);
            if (d >= bestD)
                continue;
            bestD = d;
            best = i;
        }

        return best;
    }

    private static int NearestMainNode(Vector2 p)
    {
        var best = -1;
        var bestD = float.MaxValue;
        for (var i = 0; i < _nodes.Length; i++)
        {
            if (!_inMain[i])
                continue;
            var d = Vector2.DistanceSquared(_nodes[i].Pos, p);
            if (d >= bestD)
                continue;
            bestD = d;
            best = i;
        }

        return best;
    }

    private static (float[] Dist, int[] Prev) Dijkstra(int source)
    {
        var dist = new float[_nodes.Length];
        var prev = new int[_nodes.Length];
        Array.Fill(dist, float.PositiveInfinity);
        Array.Fill(prev, -1);
        dist[source] = 0f;

        var open = new PriorityQueue<int, float>();
        open.Enqueue(source, 0f);
        while (open.TryDequeue(out var u, out var priority))
        {
            if (priority > dist[u] + 1e-3f)
                continue;

            foreach (var (v, w) in _adj[u])
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
    private static List<int>? Reconstruct(int[] prevFromEnd, int start, int end)
    {
        if (start == end)
            return [start];

        var path = new List<int>();
        for (var at = start; at >= 0; at = prevFromEnd[at])
        {
            path.Add(at);
            if (at == end)
                break;
            if (path.Count > _nodes.Length)
                return null;
        }

        if (path[^1] != end)
            return null;
        return path;
    }

    private static long QuantKey(Vector2 p)
    {
        var x = (long)MathF.Round(p.X / QuantizeMeters);
        var z = (long)MathF.Round(p.Y / QuantizeMeters);
        return (x << 32) ^ (uint)z;
    }

    private static Vector2 Dequant(long key)
    {
        var x = (int)(key >> 32);
        var z = unchecked((int)(uint)key);
        return new Vector2(x * QuantizeMeters, z * QuantizeMeters);
    }

    private static float DistancePointToRay(Vector2 point, Vector2 origin, Vector2 dirUnit)
    {
        var rel = point - origin;
        var along = Vector2.Dot(rel, dirUnit);
        if (along < 0f)
            return rel.Length();
        var closest = origin + dirUnit * along;
        return Vector2.Distance(point, closest);
    }

    private readonly record struct Node(Vector2 Pos);
}
