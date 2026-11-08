using System.Numerics;

namespace ArtillerySimulator;

/// <summary>
/// Lazy-built quantized graph of NVDB primary road segments (E/R/S/F) with largest-component marking.
/// </summary>
internal static class NvdbPrimaryRoadGraphBuilder
{
    internal const float QuantizeMeters = 20f;
    private const float BridgeMeters = 55f;
    private const float SampleMeters = 20f;

    internal readonly record struct RoadGraphNode(Vector2 Pos);

    private static bool _built;
    private static RoadGraphNode[] _nodes = [];
    private static List<(int To, float W)>[] _adj = [];
    private static bool[] _inMain = [];

    internal static RoadGraphNode[] Nodes => _nodes;
    internal static List<(int To, float W)>[] Adjacency => _adj;
    internal static bool[] InMainComponent => _inMain;

    public static void Reset()
    {
        _built = false;
        _nodes = [];
        _adj = [];
        _inMain = [];
    }

    public static void EnsureBuilt()
    {
        if (_built)
            return;
        _built = true;

        var primaries = KristiansandNvdbRoads.PrimarySegments();
        if (primaries.Count == 0)
            return;

        var indexOf = new Dictionary<long, int>(65_536);
        var nodes = new List<RoadGraphNode>(32_768);
        var undirected = new HashSet<long>();
        var edges = new List<(int A, int B, float Len)>(131_072);

        int Ensure(Vector2 p)
        {
            var key = QuantKey(p);
            if (indexOf.TryGetValue(key, out var id))
                return id;
            id = nodes.Count;
            indexOf[key] = id;
            nodes.Add(new RoadGraphNode(Dequant(key)));
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

    internal static long QuantKey(Vector2 p)
    {
        var x = (long)MathF.Round(p.X / QuantizeMeters);
        var z = (long)MathF.Round(p.Y / QuantizeMeters);
        return (x << 32) ^ (uint)z;
    }

    internal static Vector2 Dequant(long key)
    {
        var x = (int)(key >> 32);
        var z = unchecked((int)(uint)key);
        return new Vector2(x * QuantizeMeters, z * QuantizeMeters);
    }
}
