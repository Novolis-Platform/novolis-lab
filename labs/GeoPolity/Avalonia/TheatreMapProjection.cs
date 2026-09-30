using Novolis.Avalonia.StarMap;
using Novolis.Geopolitics.Core;

namespace GeoPolity.AvaloniaUi;

/// <summary>
/// App-owned StarMap layout: deterministic cluster grid from polity id + continent.
/// Core has no presentation coordinates.
/// </summary>
public static class TheatreMapProjection
{
    private const double ClusterSpacing = 90.0;
    private const double CellSpacing = 12.0;

    public static (IReadOnlyList<StarMapPoint> Points, IReadOnlyList<StarMapEdge> Edges) Project(WorldState world)
    {
        var layout = BuildLayout(world);
        var points = world.Polities
            .Select(p =>
            {
                var (x, y) = layout[p.Id.Value];
                return new StarMapPoint
                {
                    Id = p.Id.Value.ToString(),
                    Label = p.Name,
                    X = x,
                    Y = y,
                };
            })
            .ToList();

        var edges = new List<StarMapEdge>();
        var seen = new HashSet<long>();
        foreach (var pr in world.Provinces)
        {
            foreach (var n in pr.Neighbors)
            {
                var other = world.Province(n);
                if (other.OwnerId == pr.OwnerId)
                {
                    continue;
                }

                var a = pr.OwnerId.Value;
                var b = other.OwnerId.Value;
                var key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                if (!seen.Add(key))
                {
                    continue;
                }

                edges.Add(new StarMapEdge
                {
                    FromId = a.ToString(),
                    ToId = b.ToString(),
                    BandTag = world.Polity(pr.OwnerId).Continent == world.Polity(other.OwnerId).Continent
                        ? "cluster"
                        : "bridge",
                });
            }
        }

        return (points, edges);
    }

    public static (double X, double Y) PositionOf(WorldState world, PolityId id)
    {
        var layout = BuildLayout(world);
        return layout.TryGetValue(id.Value, out var xy) ? xy : (0, 0);
    }

    private static Dictionary<int, (double X, double Y)> BuildLayout(WorldState world)
    {
        var continents = world.Polities
            .Select(p => p.Continent)
            .Distinct()
            .OrderBy(c => c, StringComparer.Ordinal)
            .ToList();
        var byContinent = world.Polities
            .GroupBy(p => p.Continent)
            .ToDictionary(g => g.Key, g => g.OrderBy(p => p.Id.Value).ToList());

        var map = new Dictionary<int, (double X, double Y)>(world.Polities.Count);
        for (var c = 0; c < continents.Count; c++)
        {
            var name = continents[c];
            var peers = byContinent[name];
            var cols = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(peers.Count)));
            var ox = (c % 3) * ClusterSpacing;
            var oy = (c / 3) * ClusterSpacing;
            for (var i = 0; i < peers.Count; i++)
            {
                var row = i / cols;
                var col = i % cols;
                map[peers[i].Id.Value] = (ox + col * CellSpacing, oy + row * CellSpacing);
            }
        }

        return map;
    }
}
