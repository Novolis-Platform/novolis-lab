using System.Drawing;
using System.Numerics;
using SkiaSharp;

namespace ArtillerySimulator.Game;

/// <summary>
/// Drapes red ink from the Norgeskart roads PDF layer onto the NDH wire mesh.
/// Same georef as the land/sea mask (<see cref="KristiansandGeoref.WorldToDataX"/>).
/// </summary>
internal static class KristiansandPdfRoads
{
    private static readonly Color RoadColor = Color.FromArgb(235, 198, 172, 118);
    private static WorldLineSegment[]? _segments;
    private static bool _loadAttempted;

    public static void Draw(SilkWireBatch wires, TerrainWorld terrain)
    {
        EnsureLoaded();
        if (_segments is null || _segments.Length == 0)
            return;

        foreach (var seg in _segments)
        {
            var ay = terrain.Field.SampleHeight(seg.Ax, seg.Az) + 2.8f;
            var by = terrain.Field.SampleHeight(seg.Bx, seg.Bz) + 2.8f;
            wires.Line(new Vector3(seg.Ax, ay, seg.Az), new Vector3(seg.Bx, by, seg.Bz), RoadColor);
        }
    }

    public static void Reset()
    {
        _segments = null;
        _loadAttempted = false;
    }

    private static void EnsureLoaded()
    {
        if (_loadAttempted)
            return;
        _loadAttempted = true;

        var path = Path.Combine(AppContext.BaseDirectory, "map", "layers", "roads.png");
        if (!File.Exists(path))
            path = Path.Combine(AppContext.BaseDirectory, "map", "kristiansand-roads-reference.png");
        if (!File.Exists(path))
            return;

        try
        {
            using var bitmap = SKBitmap.Decode(path);
            if (bitmap is null)
                return;

            var east = KristiansandHeightSampler.EastMeters;
            var north = KristiansandHeightSampler.NorthMeters;
            var w = bitmap.Width;
            var h = bitmap.Height;
            // Step in pixels ≈ 40 m — enough for readable wires without millions of segments.
            var stepPx = Math.Max(2, (int)MathF.Round(40f / (east / Math.Max(1, w - 1))));
            var list = new List<WorldLineSegment>(8_192);

            for (var py = 0; py < h; py += stepPx)
            {
                for (var px = 0; px < w - stepPx; px += stepPx)
                {
                    if (!IsRoad(bitmap, px, py) || !IsRoad(bitmap, px + stepPx, py))
                        continue;

                    var a = PixelToWorld(px, py, w, h, east, north);
                    var b = PixelToWorld(px + stepPx, py, w, h, east, north);
                    list.Add(new WorldLineSegment(a.X, a.Y, b.X, b.Y));
                }
            }

            for (var px = 0; px < w; px += stepPx)
            {
                for (var py = 0; py < h - stepPx; py += stepPx)
                {
                    if (!IsRoad(bitmap, px, py) || !IsRoad(bitmap, px, py + stepPx))
                        continue;

                    var a = PixelToWorld(px, py, w, h, east, north);
                    var b = PixelToWorld(px, py + stepPx, w, h, east, north);
                    list.Add(new WorldLineSegment(a.X, a.Y, b.X, b.Y));
                }
            }

            _segments = list.ToArray();
        }
        catch
        {
            _segments = [];
        }
    }

    private static bool IsRoad(SKBitmap bitmap, int x, int y)
    {
        var c = bitmap.GetPixel(x, y);
        if (c.Alpha < 20)
            return false;
        // Red road ink from Norgeskart (solid + dashed).
        return c.Red >= 140 && c.Red > c.Green + 25 && c.Red > c.Blue + 25;
    }

    private static Vector2 PixelToWorld(int px, int py, int w, int h, float east, float north)
    {
        // Raster left → data X 0. Invert only if RasterColumn0IsEast (via WorldToDataX inverse).
        var dataX = px / (float)Math.Max(1, w - 1) * east;
        var worldX = KristiansandGeoref.RasterColumn0IsEast ? east - dataX : dataX;
        var worldZ = north - py / (float)Math.Max(1, h - 1) * north;
        return new Vector2(worldX, worldZ);
    }

    private readonly record struct WorldLineSegment(float Ax, float Az, float Bx, float Bz);
}
