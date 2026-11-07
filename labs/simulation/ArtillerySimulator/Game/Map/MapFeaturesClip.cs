using System.Numerics;

namespace ArtillerySimulator.Game;

/// <summary>Scenario bbox clipping for baked OSM features (no rendering dependencies).</summary>
internal static class MapFeaturesClip
{
    private const string RiverKind = "river";
    private const string CoastlineKind = "coastline";

    public static GroundPolyline[] ClipLines(GroundPolyline[] lines, float east, float north)
    {
        var list = new List<GroundPolyline>();
        foreach (var line in lines)
        {
            var clipped = Clip(line, east, north);
            if (clipped is not null && clipped.Points.Length >= 2 && clipped.Length > MinVisibleLength(clipped.Kind))
                list.Add(clipped);
        }

        return list.ToArray();
    }

    public static GroundPolygon[] ClipWater(IReadOnlyList<GroundPolygon> water, float east, float north)
    {
        var list = new List<GroundPolygon>();
        foreach (var poly in water)
        {
            if (ClipWaterPolygon(poly, east, north, forSolidFill: false) is { } clipped)
                list.Add(clipped);
        }

        return list.ToArray();
    }

    public static GroundPolygon[] ClipWaterForFill(IReadOnlyList<GroundPolygon> water, float east, float north)
    {
        var list = new List<GroundPolygon>();
        foreach (var poly in water)
        {
            if (ClipWaterPolygon(poly, east, north, forSolidFill: true) is { } kept)
                list.Add(kept);
        }

        return list.ToArray();
    }

    private static float MinVisibleLength(string kind) =>
        kind switch
        {
            CoastlineKind => 60f,
            RiverKind => 100f,
            _ => 120f,
        };

    private static (float MinX, float MaxX, float MinZ, float MaxZ) Bounds(Vector2[] ring, float east, float north)
    {
        var minX = ring[0].X;
        var maxX = ring[0].X;
        var minZ = ring[0].Y;
        var maxZ = ring[0].Y;
        foreach (var p in ring)
        {
            minX = MathF.Min(minX, p.X);
            maxX = MathF.Max(maxX, p.X);
            minZ = MathF.Min(minZ, p.Y);
            maxZ = MathF.Max(maxZ, p.Y);
        }

        return (
            MathF.Max(0f, minX),
            MathF.Min(east, maxX),
            MathF.Max(0f, minZ),
            MathF.Min(north, maxZ));
    }

    private static GroundPolygon? ClipWaterPolygon(GroundPolygon poly, float east, float north, bool forSolidFill)
    {
        if (poly.Ring.Length < 3)
            return null;

        var insideCount = 0;
        foreach (var p in poly.Ring)
        {
            if (p.X >= 0f && p.Y >= 0f && p.X <= east && p.Y <= north)
                insideCount++;
        }

        if (insideCount == 0)
            return null;

        var bounds = Bounds(poly.Ring, east, north);
        var width = bounds.MaxX - bounds.MinX;
        var height = bounds.MaxZ - bounds.MinZ;
        var minSpan = forSolidFill ? 48f : 120f;
        if (width < minSpan || height < minSpan)
            return null;

        if (!forSolidFill)
        {
            var mapArea = east * north;
            if (width * height > mapArea * 0.42f)
                return null;
        }

        return poly;
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
