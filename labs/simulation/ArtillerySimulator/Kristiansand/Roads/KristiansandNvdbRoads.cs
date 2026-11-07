using System.Drawing;
using System.Numerics;
using Novolis.Math.Geometry;
using Novolis.Physics.Abstractions;

namespace ArtillerySimulator;

/// <summary>
/// Drapes committed <c>map/layers/nvdb-roads.bin</c> onto the NDH mesh.
/// Main = E/R/S/F (single dark-gray centerline); local K/P/… optional thin gray. Never reads PNG.
/// Geometry outside the heightfield cutout is discarded — nothing is clamped onto the border.
/// </summary>
internal static class KristiansandNvdbRoads
{
    private static readonly Color PrimaryColor = Color.FromArgb(255, 36, 36, 36);
    private static readonly Color SecondaryColor = Color.FromArgb(220, 48, 48, 48);
    private const float LiftMeters = 8f;
    /// <summary>Drape spacing — dense enough that roads follow relief and stay continuous.</summary>
    private const float DrapeMeters = 40f;
    private const float SecondaryDrapeMeters = 70f;
    /// <summary>Skip local K/P strokes in the runtime mesh — primary roads carry OPFOR + map intent.</summary>
    private const bool DrawSecondaryRoads = false;

    private static RoadStroke[]? _strokes;
    private static bool _loadAttempted;

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
            if (!stroke.Primary && !DrawSecondaryRoads)
                continue;
            var color = stroke.Primary ? PrimaryColor : SecondaryColor;
            var drape = stroke.Primary ? DrapeMeters : SecondaryDrapeMeters;
            DrapePolyline(batch, heights, east, north, stroke.Points, color, drape);
        }

        return batch.AsSpan().ToArray();
    }

    /// <summary>Primary (E/R/S/F) consecutive vertex pairs for column routing — on-map only.</summary>
    public static IReadOnlyList<(Vector2 A, Vector2 B)> PrimarySegments()
    {
        EnsureLoaded();
        if (_strokes is null || _strokes.Length == 0)
            return [];

        var east = KristiansandHeightSampler.EastMeters;
        var north = KristiansandHeightSampler.NorthMeters;
        var list = new List<(Vector2, Vector2)>(65_536);
        foreach (var stroke in _strokes)
        {
            if (!stroke.Primary || stroke.Points.Length < 2)
                continue;
            for (var i = 1; i < stroke.Points.Length; i++)
            {
                var a = stroke.Points[i - 1];
                var b = stroke.Points[i];
                if (!ClipSegmentToMap(ref a, ref b, east, north))
                    continue;
                list.Add((a, b));
            }
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

        var path = ResolveBinPath();
        if (path is null)
            throw new FileNotFoundException(
                "Missing map/layers/nvdb-roads.bin — run fetch-nvdb-roads.cs or build-kristiansand-map.cs.");

        try
        {
            var segments = ReadBin(path);
            var east = KristiansandHeightSampler.EastMeters;
            var north = KristiansandHeightSampler.NorthMeters;
            var kept = new List<RoadStroke>(segments.Count);
            for (var i = 0; i < segments.Count; i++)
            {
                var s = segments[i];
                var pts = new Vector2[s.Points.Length];
                for (var p = 0; p < s.Points.Length; p++)
                    pts[p] = new Vector2(s.Points[p].X, s.Points[p].Z);
                pts = ClipStrokeToMap(pts, east, north);
                if (pts.Length >= 2)
                    kept.Add(new RoadStroke(s.IsMain, pts));
            }

            _strokes = kept.ToArray();
            Console.WriteLine($"NVDB roads: {_strokes.Length} on-map segments from {path}");
        }
        catch (Exception ex) when (ex is not FileNotFoundException)
        {
            throw new InvalidDataException($"Failed to load nvdb-roads.bin: {ex.Message}", ex);
        }
    }

    private static string? ResolveBinPath()
    {
        foreach (var path in new[]
                 {
                     Path.Combine(AppContext.BaseDirectory, "map", "layers", "nvdb-roads.bin"),
                     Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "map", "layers", "nvdb-roads.bin")),
                     @"d:\novolis\novolis-lab\labs\simulation\ArtillerySimulator\map\layers\nvdb-roads.bin",
                 })
        {
            if (File.Exists(path))
                return path;
        }

        return null;
    }

    private static List<(bool IsMain, (float X, float Z)[] Points)> ReadBin(string path)
    {
        // Inline reader — runtime must not reference MapPipeline.Support.
        var bytes = File.ReadAllBytes(path);
        if (bytes.Length < 16 || System.Text.Encoding.ASCII.GetString(bytes, 0, 8) != "NVDBROAD")
            throw new InvalidDataException("Bad nvdb-roads.bin magic.");
        var version = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(8));
        if (version != 1)
            throw new InvalidDataException($"Unsupported nvdb-roads version {version}.");
        var count = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(12));
        var list = new List<(bool, (float, float)[])>((int)count);
        var o = 16;
        for (var i = 0; i < count; i++)
        {
            if (o + 4 > bytes.Length)
                break;
            o++; // vegkategori
            var flags = bytes[o++];
            var n = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(o));
            o += 2;
            if (o + n * 8 > bytes.Length)
                break;
            var pts = new (float X, float Z)[n];
            for (var p = 0; p < n; p++)
            {
                pts[p] = (BitConverter.ToSingle(bytes, o), BitConverter.ToSingle(bytes, o + 4));
                o += 8;
            }

            if (n >= 2)
                list.Add(((flags & 1) != 0, pts));
        }

        return list;
    }

    /// <summary>Keep only on-map vertices; cut edge-crossing chords at the boundary.</summary>
    private static Vector2[] ClipStrokeToMap(Vector2[] points, float east, float north)
    {
        if (points.Length < 2)
            return [];

        var outPts = new List<Vector2>(points.Length);
        for (var i = 1; i < points.Length; i++)
        {
            var a = points[i - 1];
            var b = points[i];
            if (!ClipSegmentToMap(ref a, ref b, east, north))
                continue;

            if (outPts.Count == 0 || Vector2.DistanceSquared(outPts[^1], a) > 0.01f)
                outPts.Add(a);
            outPts.Add(b);
        }

        return outPts.Count >= 2 ? outPts.ToArray() : [];
    }

    /// <summary>
    /// Liang–Barsky style clip of segment AB against [0,east]×[0,north].
    /// Returns false when the segment lies entirely outside.
    /// </summary>
    internal static bool ClipSegmentToMap(ref Vector2 a, ref Vector2 b, float east, float north)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        var t0 = 0f;
        var t1 = 1f;

        bool Clip(float p, float q)
        {
            if (MathF.Abs(p) < 1e-8f)
                return q >= 0f;
            var r = q / p;
            if (p < 0f)
            {
                if (r > t1)
                    return false;
                if (r > t0)
                    t0 = r;
            }
            else
            {
                if (r < t0)
                    return false;
                if (r < t1)
                    t1 = r;
            }

            return true;
        }

        if (!Clip(-dx, a.X) || !Clip(dx, east - a.X) || !Clip(-dy, a.Y) || !Clip(dy, north - a.Y))
            return false;
        if (t1 < t0)
            return false;

        var na = new Vector2(a.X + t0 * dx, a.Y + t0 * dy);
        var nb = new Vector2(a.X + t1 * dx, a.Y + t1 * dy);
        a = na;
        b = nb;
        return Vector2.DistanceSquared(a, b) >= 0.25f;
    }

    private static void DrapePolyline(
        SilkWireBatch wires,
        IHeightSampler heights,
        float east,
        float north,
        Vector2[] points,
        Color color,
        float drapeMeters)
    {
        if (points.Length < 2)
            return;

        for (var i = 1; i < points.Length; i++)
        {
            var a2 = points[i - 1];
            var b2 = points[i];
            if (!ClipSegmentToMap(ref a2, ref b2, east, north))
                continue;

            var dx = b2.X - a2.X;
            var dz = b2.Y - a2.Y;
            var len = MathF.Sqrt(dx * dx + dz * dz);
            if (len < 0.5f)
                continue;

            if (len <= drapeMeters)
            {
                wires.Line(
                    Place(heights, a2.X, a2.Y),
                    Place(heights, b2.X, b2.Y),
                    color);
                continue;
            }

            var samples = Math.Max(2, (int)MathF.Ceiling(len / drapeMeters) + 1);
            Vector3? prev = null;
            var inv = 1f / (samples - 1);

            for (var s = 0; s < samples; s++)
            {
                var t = s * inv;
                var x = a2.X + dx * t;
                var z = a2.Y + dz * t;
                var p = Place(heights, x, z);
                if (prev is { } p0)
                    wires.Line(p0, p, color);
                prev = p;
            }
        }
    }

    private static Vector3 Place(IHeightSampler heights, float x, float z) =>
        new(x, heights.SampleHeight(x, z) + LiftMeters, z);

    private readonly record struct RoadStroke(bool Primary, Vector2[] Points);
}
