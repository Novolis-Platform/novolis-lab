using System.Drawing;
using System.Numerics;
using Novolis.Math.Geometry;

namespace ArtillerySimulator.Game;

/// <summary>
/// Meter-aligned topographic raster (same rules as extract-artillery-map preview PNG).
/// Shared by Raylib terrain mesh and the Silk map lab blit path.
/// </summary>
internal static class KristiansandMapRaster
{
    public static readonly Color NorgeskartWater = Color.FromArgb(255, 81, 130, 255);

    public static short MinElevationMeters { get; }
    public static short MaxElevationMeters { get; }

    static KristiansandMapRaster()
    {
        var min = short.MaxValue;
        var max = short.MinValue;
        foreach (var h in KristiansandElevation.Meters)
        {
            if (h < min)
                min = h;
            if (h > max)
                max = h;
        }

        MinElevationMeters = min;
        MaxElevationMeters = max;
    }

    public static float SampleElevationBilinear(float xMeters, float zMeters)
    {
        var east = KristiansandHeightSampler.EastMeters;
        var north = KristiansandHeightSampler.NorthMeters;
        var cells = KristiansandElevation.Count - 1;
        var u = Math.Clamp(xMeters / east, 0f, 1f) * cells;
        var v = Math.Clamp(zMeters / north, 0f, 1f) * cells;
        var i0 = (int)MathF.Floor(u);
        var j0 = (int)MathF.Floor(v);
        var i1 = Math.Min(i0 + 1, cells);
        var j1 = Math.Min(j0 + 1, cells);
        var tx = u - i0;
        var tz = v - j0;
        var h00 = At(i0, j0);
        var h10 = At(i1, j0);
        var h01 = At(i0, j1);
        var h11 = At(i1, j1);
        var south = h00 + (h10 - h00) * tx;
        var northH = h01 + (h11 - h01) * tx;
        return south + (northH - south) * tz;
    }

    public static Color TerrainColorAt(float xMeters, float zMeters)
    {
        if (KristiansandLandSeaMask.IsWater(xMeters, zMeters))
            return NorgeskartWater;
        return TerrainColorLand(SampleElevationBilinear(xMeters, zMeters));
    }

    public static Color TerrainColor(float heightMeters) => TerrainColorLand(heightMeters);

    private static Color TerrainColorLand(float heightMeters)
    {
        var span = Math.Max(1, MaxElevationMeters - MinElevationMeters);
        var t = (heightMeters - MinElevationMeters) / span;
        t = Math.Clamp(t, 0f, 1f);
        var hue = 95f - t * 55f;
        var sat = 0.42f;
        var val = 0.38f + t * 0.58f;
        return ColorFromHsv(hue, sat, val);
    }

    public static void FillTerrainPixels(Span<Rgba32> pixels, int width, int height)
    {
        var east = KristiansandHeightSampler.EastMeters;
        var north = KristiansandHeightSampler.NorthMeters;
        for (var py = 0; py < height; py++)
        {
            var v = 1f - py / Math.Max(1, height - 1f);
            var z = v * north;
            for (var px = 0; px < width; px++)
            {
                var u = px / Math.Max(1, width - 1f);
                var x = u * east;
                pixels[py * width + px] = ToRgba(TerrainColorAt(x, z));
            }
        }
    }

    public static Rgba32[] BuildFrame(int width, int height, bool drawLinework)
    {
        var pixels = new Rgba32[width * height];
        FillTerrainPixels(pixels, width, height);
        if (!KristiansandLandSeaMask.IsAvailable)
            StampWaterPolygons(pixels, width, height);
        if (drawLinework)
            StampLinework(pixels, width, height);
        return pixels;
    }

    public static void StampWaterPolygons(Span<Rgba32> pixels, int width, int height)
    {
        var east = KristiansandHeightSampler.EastMeters;
        var north = KristiansandHeightSampler.NorthMeters;
        var fill = Color.FromArgb(150, 48, 118, 188);
        foreach (var poly in VisibleWater())
        {
            if (poly.Ring.Length < 3)
                continue;
            RasterPolygonFill(pixels, width, height, poly.Ring, east, north, fill);
        }
    }

    public static void StampLinework(Span<Rgba32> pixels, int width, int height)
    {
        var east = KristiansandHeightSampler.EastMeters;
        var north = KristiansandHeightSampler.NorthMeters;
        foreach (var line in VisibleLines())
        {
            if (line.Points.Length < 2)
                continue;
            var color = line.Kind switch
            {
                "coastline" => Color.FromArgb(255, 120, 210, 255),
                "river" => Color.FromArgb(255, 90, 170, 230),
                _ => Color.FromArgb(255, 210, 188, 140),
            };
            var thickness = line.Kind switch
            {
                "coastline" => 3,
                "river" => 2,
                _ => 2,
            };
            for (var i = 1; i < line.Points.Length; i++)
                RasterSegment(pixels, width, height, line.Points[i - 1], line.Points[i], east, north, color, thickness);
        }
    }

    private static GroundPolyline[]? _visibleLines;
    private static GroundPolygon[]? _visibleWater;

    private static IReadOnlyList<GroundPolyline> VisibleLines() =>
        _visibleLines ??= MapFeaturesClip.ClipLines(
            KristiansandMapFeatures.Lines,
            KristiansandHeightSampler.EastMeters,
            KristiansandHeightSampler.NorthMeters);

    private static IReadOnlyList<GroundPolygon> VisibleWater() =>
        _visibleWater ??= MapFeaturesClip.ClipWater(
            KristiansandMapFeatures.Water,
            KristiansandHeightSampler.EastMeters,
            KristiansandHeightSampler.NorthMeters);

    private static short At(int i, int j) => KristiansandElevation.Meters[j * KristiansandElevation.Count + i];

    private static void RasterPolygonFill(
        Span<Rgba32> pixels,
        int width,
        int height,
        Vector2[] ring,
        float eastMeters,
        float northMeters,
        Color fill)
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

        var px0 = (int)MathF.Floor(minX / eastMeters * width);
        var px1 = (int)MathF.Ceiling(maxX / eastMeters * width);
        var py0 = (int)MathF.Floor((1f - maxZ / northMeters) * height);
        var py1 = (int)MathF.Ceiling((1f - minZ / northMeters) * height);
        px0 = Math.Clamp(px0, 0, width - 1);
        px1 = Math.Clamp(px1, 0, width - 1);
        py0 = Math.Clamp(py0, 0, height - 1);
        py1 = Math.Clamp(py1, 0, height - 1);
        var rgba = ToRgba(fill);
        for (var py = py0; py <= py1; py++)
        {
            var v = 1f - py / Math.Max(1, height - 1f);
            var z = v * northMeters;
            for (var px = px0; px <= px1; px++)
            {
                var u = px / Math.Max(1, width - 1f);
                var x = u * eastMeters;
                if (!Contains(ring, new Vector2(x, z)))
                    continue;
                var idx = py * width + px;
                pixels[idx] = BlendOver(pixels[idx], rgba);
            }
        }
    }

    private static void RasterSegment(
        Span<Rgba32> pixels,
        int width,
        int height,
        Vector2 a,
        Vector2 b,
        float eastMeters,
        float northMeters,
        Color color,
        int thickness)
    {
        var ax = a.X / eastMeters * width;
        var ay = height - a.Y / northMeters * height;
        var bx = b.X / eastMeters * width;
        var by = height - b.Y / northMeters * height;
        var steps = (int)MathF.Ceiling(Vector2.Distance(new Vector2(ax, ay), new Vector2(bx, by)));
        steps = Math.Clamp(steps, 1, 16_384);
        var rgba = ToRgba(color);
        for (var i = 0; i <= steps; i++)
        {
            var t = i / (float)steps;
            var px = (int)MathF.Round(ax + (bx - ax) * t);
            var py = (int)MathF.Round(ay + (by - ay) * t);
            for (var oy = -thickness; oy <= thickness; oy++)
            {
                for (var ox = -thickness; ox <= thickness; ox++)
                {
                    if (ox * ox + oy * oy > thickness * thickness + 1)
                        continue;
                    var x = px + ox;
                    var y = py + oy;
                    if (x < 0 || y < 0 || x >= width || y >= height)
                        continue;
                    pixels[y * width + x] = rgba;
                }
            }
        }
    }

    private static bool Contains(Vector2[] ring, Vector2 point)
    {
        var inside = false;
        for (var i = 0; i < ring.Length; i++)
        {
            var j = (i + ring.Length - 1) % ring.Length;
            var pi = ring[i];
            var pj = ring[j];
            if ((pi.Y > point.Y) == (pj.Y > point.Y))
                continue;
            var slope = (pj.X - pi.X) / (pj.Y - pi.Y);
            if (point.X < slope * (point.Y - pi.Y) + pi.X)
                inside = !inside;
        }

        return inside;
    }

    private static Rgba32 ToRgba(Color c) => new(c.R, c.G, c.B, c.A);

    private static Rgba32 BlendOver(Rgba32 under, Rgba32 over)
    {
        var a = over.A / 255f;
        if (a <= 0f)
            return under;
        var inv = 1f - a;
        return new Rgba32(
            (byte)Math.Clamp(over.R * a + under.R * inv, 0, 255),
            (byte)Math.Clamp(over.G * a + under.G * inv, 0, 255),
            (byte)Math.Clamp(over.B * a + under.B * inv, 0, 255),
            (byte)Math.Clamp(over.A + under.A * inv, 0, 255));
    }

    private static Color ColorFromHsv(float hue, float saturation, float value)
    {
        var hi = (int)(hue / 60f) % 6;
        var f = hue / 60f - (int)(hue / 60f);
        var p = value * (1f - saturation);
        var q = value * (1f - f * saturation);
        var t = value * (1f - (1f - f) * saturation);
        (float r, float g, float b) = hi switch
        {
            0 => (value, t, p),
            1 => (q, value, p),
            2 => (p, value, t),
            3 => (p, q, value),
            4 => (t, p, value),
            _ => (value, p, q),
        };
        return Color.FromArgb(
            255,
            (int)Math.Clamp(r * 255f, 0, 255),
            (int)Math.Clamp(g * 255f, 0, 255),
            (int)Math.Clamp(b * 255f, 0, 255));
    }
}
