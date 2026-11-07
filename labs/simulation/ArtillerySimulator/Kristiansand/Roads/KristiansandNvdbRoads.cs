using System.Drawing;
using System.Numerics;
using Novolis.Math.Geometry;
using Novolis.Physics.Abstractions;

namespace ArtillerySimulator;

/// <summary>
/// Drapes committed <c>map/layers/nvdb-roads.bin</c> onto the NDH mesh.
/// Main = E/R/S/F (thick red); local-local = K/P/… (thin gray). Never reads PNG.
/// </summary>
internal static class KristiansandNvdbRoads
{
    private static readonly Color PrimaryColor = Color.FromArgb(255, 228, 72, 58);
    private static readonly Color SecondaryColor = Color.FromArgb(220, 168, 168, 168);
    private const float LiftMeters = 3.2f;
    private const float PrimaryWidthMeters = 9f;
    private const float DrapeMeters = 14f;

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
            var color = stroke.Primary ? PrimaryColor : SecondaryColor;
            DrapePolyline(batch, heights, east, north, stroke.Points, color, stroke.Primary ? PrimaryWidthMeters : 0f);
        }

        return batch.AsSpan().ToArray();
    }

    /// <summary>Primary (E/R/S/F) consecutive vertex pairs for column routing.</summary>
    public static IReadOnlyList<(Vector2 A, Vector2 B)> PrimarySegments()
    {
        EnsureLoaded();
        if (_strokes is null || _strokes.Length == 0)
            return [];

        var list = new List<(Vector2, Vector2)>(65_536);
        foreach (var stroke in _strokes)
        {
            if (!stroke.Primary || stroke.Points.Length < 2)
                continue;
            for (var i = 1; i < stroke.Points.Length; i++)
                list.Add((stroke.Points[i - 1], stroke.Points[i]));
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
            _strokes = new RoadStroke[segments.Count];
            for (var i = 0; i < segments.Count; i++)
            {
                var s = segments[i];
                var pts = new Vector2[s.Points.Length];
                for (var p = 0; p < s.Points.Length; p++)
                    pts[p] = new Vector2(s.Points[p].X, s.Points[p].Z);
                _strokes[i] = new RoadStroke(s.IsMain, pts);
            }

            Console.WriteLine($"NVDB roads: {_strokes.Length} segments from {path}");
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
                    var l = Place(heights, east, north, p.X + perp.X * half, p.Z + perp.Z * half);
                    var r = Place(heights, east, north, p.X - perp.X * half, p.Z - perp.Z * half);
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

    private readonly record struct RoadStroke(bool Primary, Vector2[] Points);
}
