using System.Drawing;
using System.Numerics;
using Novolis.Math.Geometry;
using Novolis.Physics.Abstractions;
using SkiaSharp;

namespace ArtillerySimulator.Game;

/// <summary>
/// Drapes Norgeskart <c>roads.png</c> onto the NDH mesh.
/// 8-connected tracing at ~12 m, primary roads as a 3-wire ribbon, secondary as a single wire.
/// </summary>
internal static class KristiansandPdfRoads
{
    private static readonly Color PrimaryColor = Color.FromArgb(255, 228, 72, 58);
    private static readonly Color SecondaryColor = Color.FromArgb(220, 190, 150, 140);
    private const float StepMeters = 12f;
    private const float LiftMeters = 3.2f;
    private const float PrimaryWidthMeters = 9f;
    private const float DrapeMeters = 14f;

    private static RoadStroke[]? _strokes;
    private static bool _loadAttempted;

    /// <summary>Build draped road wires once after the heightfield is ready (not per frame).</summary>
    public static WorldLineVertex[] Build(IHeightSampler heights)
    {
        EnsureLoaded();
        if (_strokes is null || _strokes.Length == 0)
            return [];

        var batch = new SilkWireBatch();
        var east = KristiansandHeightSampler.EastMeters;
        var north = KristiansandHeightSampler.NorthMeters;
        foreach (var stroke in _strokes)
        {
            var color = stroke.Primary ? PrimaryColor : SecondaryColor;
            DrapePolyline(batch, heights, east, north, stroke.Points, color, stroke.Primary ? PrimaryWidthMeters : 0f);
        }

        return batch.AsSpan().ToArray();
    }

    public static void Draw(SilkWireBatch wires, TerrainWorld terrain)
    {
        // Prefer TerrainWorld's cached GPU mesh; kept for table / debug overlays.
        EnsureLoaded();
        if (_strokes is null || _strokes.Length == 0)
            return;

        foreach (var stroke in _strokes)
        {
            var color = stroke.Primary ? PrimaryColor : SecondaryColor;
            DrapePolyline(
                wires,
                new HeightAdapter(terrain),
                terrain.EastMeters,
                terrain.NorthMeters,
                stroke.Points,
                color,
                stroke.Primary ? PrimaryWidthMeters : 0f);
        }
    }

    /// <summary>Primary (Norgeskart red) road segments as world XZ pairs — for column routing.</summary>
    public static IReadOnlyList<(Vector2 A, Vector2 B)> PrimarySegments()
    {
        EnsureLoaded();
        if (_strokes is null || _strokes.Length == 0)
            return [];

        var list = new List<(Vector2, Vector2)>(_strokes.Length);
        foreach (var stroke in _strokes)
        {
            if (!stroke.Primary || stroke.Points.Length < 2)
                continue;
            list.Add((stroke.Points[0], stroke.Points[1]));
        }

        return list;
    }

    public static void Reset()
    {
        _strokes = null;
        _loadAttempted = false;
        PrimaryRoadNetwork.Reset();
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
            var metersPerPixel = east / Math.Max(1, w - 1);
            var stepPx = Math.Max(1, (int)MathF.Round(StepMeters / metersPerPixel));

            var road = BuildRoadMask(bitmap, w, h);
            _strokes = TraceStrokes(road, w, h, stepPx, east, north);
        }
        catch
        {
            _strokes = [];
        }
    }

    /// <summary>0 = none, 1 = secondary, 2 = primary.</summary>
    private static byte[] BuildRoadMask(SKBitmap bitmap, int w, int h)
    {
        var road = new byte[w * h];
        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                road[y * w + x] = (byte)Classify(bitmap.GetPixel(x, y));
            }
        }

        return road;
    }

    private static RoadStroke[] TraceStrokes(byte[] road, int w, int h, int stepPx, float east, float north)
    {
        // Emit undirected edges once: E, SE, S, SW from each road sample.
        var edges = new List<(int Ax, int Ay, int Bx, int By, bool Primary)>(65_536);
        ReadOnlySpan<int> dx = [stepPx, stepPx, 0, -stepPx];
        ReadOnlySpan<int> dy = [0, stepPx, stepPx, stepPx];

        for (var py = 0; py < h; py += stepPx)
        {
            for (var px = 0; px < w; px += stepPx)
            {
                if (road[py * w + px] == 0)
                    continue;

                for (var d = 0; d < 4; d++)
                {
                    var qx = px + dx[d];
                    var qy = py + dy[d];
                    if ((uint)qx >= (uint)w || (uint)qy >= (uint)h)
                        continue;
                    if (road[qy * w + qx] == 0)
                        continue;

                    // Require a road sample near the midpoint so gaps / noise don't bridge.
                    var mx = (px + qx) / 2;
                    var my = (py + qy) / 2;
                    if (road[my * w + mx] == 0 && !HasRoadNear(road, w, h, mx, my, stepPx))
                        continue;

                    var primary = road[py * w + px] >= (byte)RoadKind.Primary
                        || road[qy * w + qx] >= (byte)RoadKind.Primary;
                    edges.Add((px, py, qx, qy, primary));
                }
            }
        }

        var strokes = new RoadStroke[edges.Count];
        for (var i = 0; i < edges.Count; i++)
        {
            var e = edges[i];
            var a = PixelToWorld(e.Ax, e.Ay, w, h, east, north);
            var b = PixelToWorld(e.Bx, e.By, w, h, east, north);
            strokes[i] = new RoadStroke(e.Primary, [a, b]);
        }

        return strokes;
    }

    private static bool HasRoadNear(byte[] road, int w, int h, int x, int y, int radius)
    {
        var r = Math.Max(1, radius / 2);
        for (var dy = -r; dy <= r; dy++)
        {
            var yy = y + dy;
            if ((uint)yy >= (uint)h)
                continue;
            for (var dx = -r; dx <= r; dx++)
            {
                var xx = x + dx;
                if ((uint)xx >= (uint)w)
                    continue;
                if (road[yy * w + xx] != 0)
                    return true;
            }
        }

        return false;
    }

    private static void DrapePolyline(
        SilkWireBatch wires,
        IHeightSampler heights,
        float east,
        float north,
        Vector2[] points,
        Color color,
        float widthMeters)
    {
        if (points.Length < 2)
            return;

        for (var i = 1; i < points.Length; i++)
        {
            var a2 = points[i - 1];
            var b2 = points[i];
            var dx = b2.X - a2.X;
            var dz = b2.Y - a2.Y;
            var len = MathF.Sqrt(dx * dx + dz * dz);
            if (len < 0.5f)
                continue;

            var samples = Math.Max(2, (int)MathF.Ceiling(len / DrapeMeters) + 1);
            Vector3? prev = null;
            Vector3? prevL = null;
            Vector3? prevR = null;
            var inv = 1f / (samples - 1);
            var forward = new Vector3(dx / len, 0f, dz / len);
            var perp = new Vector3(-forward.Z, 0f, forward.X);
            var half = widthMeters * 0.5f;

            for (var s = 0; s < samples; s++)
            {
                var t = s * inv;
                var x = a2.X + dx * t;
                var z = a2.Y + dz * t;
                var p = Place(heights, east, north, x, z);
                if (prev is { } p0)
                    wires.Line(p0, p, color);

                if (widthMeters > 0.5f)
                {
                    var l = p + perp * half;
                    var r = p - perp * half;
                    // Keep sides on terrain too.
                    l = Place(heights, east, north, l.X, l.Z);
                    r = Place(heights, east, north, r.X, r.Z);
                    if (prevL is { } pl)
                        wires.Line(pl, l, color);
                    if (prevR is { } pr)
                        wires.Line(pr, r, color);
                    prevL = l;
                    prevR = r;
                }

                prev = p;
            }
        }
    }

    private static Vector3 Place(IHeightSampler heights, float east, float north, float x, float z)
    {
        x = Math.Clamp(x, 0f, east);
        z = Math.Clamp(z, 0f, north);
        return new Vector3(x, heights.SampleHeight(x, z) + LiftMeters, z);
    }

    private static RoadKind Classify(SKColor c)
    {
        if (c.Alpha < 20)
            return RoadKind.None;

        // Solid Norgeskart red ink.
        if (c.Red >= 150 && c.Red > c.Green + 40 && c.Red > c.Blue + 40)
            return RoadKind.Primary;

        // Pink / dashed wash still red-dominant.
        if (c.Red >= 120 && c.Red >= c.Green - 5 && c.Red >= c.Blue - 5)
            return RoadKind.Secondary;

        return RoadKind.None;
    }

    private static Vector2 PixelToWorld(int px, int py, int w, int h, float east, float north)
    {
        var dataX = px / (float)Math.Max(1, w - 1) * east;
        var worldX = KristiansandGeoref.RasterColumn0IsEast ? east - dataX : dataX;
        var worldZ = north - py / (float)Math.Max(1, h - 1) * north;
        return new Vector2(worldX, worldZ);
    }

    private enum RoadKind : byte
    {
        None = 0,
        Primary = 1,
        Secondary = 2,
    }

    private readonly record struct RoadStroke(bool Primary, Vector2[] Points);

    private sealed class HeightAdapter(TerrainWorld terrain) : IHeightSampler
    {
        public float SampleHeight(float x, float z) => terrain.Field.SampleHeight(x, z);
    }
}
