using System.Drawing;
using System.Globalization;
using System.Numerics;
using Novolis.Agent.Core;

namespace ArtillerySimulator.Game;

internal static class MapFeaturesCatalog
{
    private const string RiverKind = "river";
    private const string CoastlineKind = "coastline";

    public static GroundPolyline[] ClipLines(GroundPolyline[] lines, float east, float north) =>
        MapFeaturesClip.ClipLines(lines, east, north);

    public static void DrawLines(
        SilkWireBatch wires,
        TerrainWorld terrain,
        IReadOnlyList<GroundPolyline> lines,
        bool skipCoastline = false)
    {
        foreach (var line in lines)
        {
            if (skipCoastline && line.Kind == CoastlineKind)
                continue;

            var color = line.Kind switch
            {
                RiverKind => Color.FromArgb(245, 55, 145, 225),
                CoastlineKind => Color.FromArgb(255, 100, 200, 255),
                _ => Color.FromArgb(235, 198, 172, 118),
            };
            var lift = line.Kind switch
            {
                RiverKind => 2.2f,
                CoastlineKind => 2.6f,
                _ => 3.4f,
            };
            var width = line.Kind switch
            {
                RiverKind => 28f,
                CoastlineKind => 48f,
                _ => 20f,
            };
            for (var i = 1; i < line.Points.Length; i++)
            {
                var a = line.Points[i - 1];
                var b = line.Points[i];
                DrawMapSegment(wires, terrain, a, b, lift, width, color);
            }
        }
    }

    public static void DrawLinesOnTable(
        SilkWireBatch wires,
        IReadOnlyList<GroundPolyline> lines,
        float yMeters,
        bool skipCoastline = false)
    {
        foreach (var line in lines)
        {
            if (skipCoastline && line.Kind == CoastlineKind)
                continue;

            var color = line.Kind switch
            {
                RiverKind => Color.FromArgb(230, 55, 145, 225),
                _ => Color.FromArgb(220, 198, 172, 118),
            };
            for (var i = 1; i < line.Points.Length; i++)
            {
                var a = line.Points[i - 1];
                var b = line.Points[i];
                wires.Line(new Vector3(a.X, yMeters, a.Y), new Vector3(b.X, yMeters, b.Y), color);
            }
        }
    }

    public static GroundPolygon[] ClipWater(IReadOnlyList<GroundPolygon> water, float east, float north) =>
        MapFeaturesClip.ClipWater(water, east, north);

    public const float SeaLevelMeters = 2.0f;

    public static GroundPolygon[] ClipWaterForFill(IReadOnlyList<GroundPolygon> water, float east, float north) =>
        MapFeaturesClip.ClipWaterForFill(water, east, north);

    public static void DrawWaterOutlines(
        SilkWireBatch wires,
        TerrainWorld terrain,
        IReadOnlyList<GroundPolygon> water,
        float east,
        float north)
    {
        var edge = Color.FromArgb(210, 160, 220, 255);
        foreach (var poly in water)
        {
            if (poly.Ring.Length < 3)
                continue;

            for (var i = 0; i < poly.Ring.Length; i++)
            {
                var a = poly.Ring[i];
                var b = poly.Ring[(i + 1) % poly.Ring.Length];
                DrawMapSegment(wires, terrain, a, b, 2.8f, 14f, edge);
            }
        }
    }

    public static AgentEntity[] MapEntities(IReadOnlyList<GroundPolyline> lines)
    {
        var list = new List<AgentEntity>(lines.Count);
        foreach (var line in lines)
        {
            var mid = line.At(line.Length * 0.5f);
            list.Add(new AgentEntity
            {
                Id = string.Create(CultureInfo.InvariantCulture, $"{line.Kind}:{line.Name}"),
                Kind = line.Kind == RiverKind ? AgentEntity.River : AgentEntity.Road,
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

    public static GroundPolyline? Longest(IReadOnlyList<GroundPolyline> lines, string namePrefix, string kind)
    {
        GroundPolyline? best = null;
        foreach (var line in lines)
        {
            if (!string.Equals(line.Kind, kind, StringComparison.Ordinal))
                continue;
            if (!line.Name.StartsWith(namePrefix, StringComparison.OrdinalIgnoreCase)
                && !line.Name.Contains(namePrefix, StringComparison.OrdinalIgnoreCase))
                continue;
            if (best is null || line.Length > best.Length)
                best = line;
        }

        return best;
    }

    private static void DrawMapSegment(
        SilkWireBatch wires,
        TerrainWorld terrain,
        Vector2 a2,
        Vector2 b2,
        float lift,
        float widthMeters,
        Color color)
    {
        var a = Place(terrain, a2, lift);
        var b = Place(terrain, b2, lift);
        var dir = b - a;
        var len = dir.Length();
        if (len < 0.5f)
            return;

        var forward = dir / len;
        var perp = Vector3.Normalize(new Vector3(-forward.Z, 0f, forward.X));
        var half = widthMeters * 0.5f;
        wires.Line(a, b, color);
        wires.Line(a + perp * half, b + perp * half, color);
        wires.Line(a - perp * half, b - perp * half, color);
    }

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
}
