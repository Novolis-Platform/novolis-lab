using System.Drawing;
using System.Globalization;
using System.Numerics;
using Novolis.Agent.Core;
using Novolis.Raylib.Game;

namespace ArtillerySimulator.Game;

internal static partial class OttaMapFeatures
{
    private static GroundPolyline[]? _visible;

    public static IReadOnlyList<GroundPolyline> Visible => _visible ??= ClipAll();

    public static GroundPolyline? Longest(string name, string kind)
    {
        GroundPolyline? best = null;
        foreach (var line in Visible)
        {
            if (!string.Equals(line.Kind, kind, StringComparison.Ordinal))
                continue;
            if (!line.Name.StartsWith(name, StringComparison.Ordinal))
                continue;
            if (best is null || line.Length > best.Length)
                best = line;
        }

        return best;
    }

    public static void Draw(RayGameContext ctx, TerrainWorld terrain)
    {
        foreach (var line in Visible)
        {
            var color = line.Kind == GroundPolylineRiver
                ? Color.FromArgb(230, 86, 156, 196)
                : Color.FromArgb(220, 176, 156, 118);
            var lift = line.Kind == GroundPolylineRiver ? 2.2f : 3.4f;
            for (var i = 1; i < line.Points.Length; i++)
            {
                var a = line.Points[i - 1];
                var b = line.Points[i];
                ctx.DrawBolt(Place(terrain, a, lift), Place(terrain, b, lift), color);
            }
        }
    }

    public static AgentEntity[] MapEntities()
    {
        var list = new List<AgentEntity>(Visible.Count);
        foreach (var line in Visible)
        {
            var mid = line.At(line.Length * 0.5f);
            list.Add(new AgentEntity
            {
                Id = string.Create(CultureInfo.InvariantCulture, $"{line.Kind}:{line.Name}"),
                Kind = line.Kind == GroundPolylineRiver ? AgentEntity.River : AgentEntity.Road,
                Name = line.Name,
                State = "map",
                X = mid.X,
                Z = mid.Y,
                Summary = string.Create(CultureInfo.InvariantCulture, $"{line.Kind} {line.Name}, {line.Length / 1000f:F1} km"),
                Path = SamplePath(line, 24),
            });
        }

        return list.ToArray();
    }

    private const string GroundPolylineRiver = "river";

    private static Vector3 Place(TerrainWorld terrain, Vector2 point, float lift)
    {
        var y = terrain.Field.SampleHeight(point.X, point.Y) + lift;
        return new Vector3(point.X, y, point.Y);
    }

    private static double[] SamplePath(GroundPolyline line, int count)
    {
        var path = new double[count * 2];
        var steps = Math.Max(1, count - 1);
        for (var i = 0; i < count; i++)
        {
            var at = line.At(line.Length * i / steps);
            path[i * 2] = at.X;
            path[i * 2 + 1] = at.Y;
        }

        return path;
    }

    private static GroundPolyline[] ClipAll()
    {
        var east = OttaHeightSampler.EastMeters;
        var north = OttaHeightSampler.NorthMeters;
        var list = new List<GroundPolyline>();
        foreach (var line in Lines)
        {
            var clipped = Clip(line, east, north);
            if (clipped is not null && clipped.Points.Length >= 2 && clipped.Length > 800f)
                list.Add(clipped);
        }

        return list.ToArray();
    }

    private static GroundPolyline? Clip(GroundPolyline line, float east, float north)
    {
        var best = new List<Vector2>();
        var run = new List<Vector2>();
        foreach (var point in line.Points)
        {
            var inside = point.X >= -80f && point.Y >= -80f && point.X <= east + 80f && point.Y <= north + 80f;
            if (inside)
            {
                run.Add(point);
                continue;
            }

            if (run.Count > best.Count)
                best = run;
            run = [];
        }

        if (run.Count > best.Count)
            best = run;
        if (best.Count < 2)
            return null;

        return new GroundPolyline(line.Name, line.Kind, best.ToArray());
    }
}
