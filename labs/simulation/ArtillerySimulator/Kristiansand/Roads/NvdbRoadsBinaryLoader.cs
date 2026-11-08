using System.Buffers.Binary;
using System.Numerics;
using System.Text;

namespace ArtillerySimulator;

/// <summary>Reads committed <c>nvdb-roads.bin</c> and clips strokes to the heightfield cutout.</summary>
internal static class NvdbRoadsBinaryLoader
{
    internal readonly record struct RoadStroke(bool Primary, Vector2[] Points);

    public static RoadStroke[] LoadOnMapStrokes(float eastMeters, float northMeters)
    {
        var path = ResolveBinPath()
            ?? throw new FileNotFoundException(
                "Missing map/layers/nvdb-roads.bin — run fetch-nvdb-roads.cs or build-kristiansand-map.cs.");

        try
        {
            var segments = ReadBin(path);
            var kept = new List<RoadStroke>(segments.Count);
            for (var i = 0; i < segments.Count; i++)
            {
                var s = segments[i];
                var pts = new Vector2[s.Points.Length];
                for (var p = 0; p < s.Points.Length; p++)
                    pts[p] = new Vector2(s.Points[p].X, s.Points[p].Z);
                pts = ClipStrokeToMap(pts, eastMeters, northMeters);
                if (pts.Length >= 2)
                    kept.Add(new RoadStroke(s.IsMain, pts));
            }

            Console.WriteLine($"NVDB roads: {kept.Count} on-map segments from {path}");
            return kept.ToArray();
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
                     Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "map", "layers", "nvdb-roads.bin")),
                 })
        {
            if (File.Exists(path))
                return path;
        }

        return null;
    }

    private static List<(bool IsMain, (float X, float Z)[] Points)> ReadBin(string path)
    {
        var bytes = File.ReadAllBytes(path);
        if (bytes.Length < 16 || Encoding.ASCII.GetString(bytes, 0, 8) != "NVDBROAD")
            throw new InvalidDataException("Bad nvdb-roads.bin magic.");
        var version = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(8));
        if (version != 1)
            throw new InvalidDataException($"Unsupported nvdb-roads version {version}.");
        var count = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(12));
        var list = new List<(bool, (float, float)[])>((int)count);
        var o = 16;
        for (var i = 0; i < count; i++)
        {
            if (o + 4 > bytes.Length)
                break;
            o++; // vegkategori
            var flags = bytes[o++];
            var n = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(o));
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

    private static Vector2[] ClipStrokeToMap(Vector2[] points, float east, float north)
    {
        if (points.Length < 2)
            return [];

        var outPts = new List<Vector2>(points.Length);
        for (var i = 1; i < points.Length; i++)
        {
            var a = points[i - 1];
            var b = points[i];
            if (!NvdbMapClip.ClipSegmentToMap(ref a, ref b, east, north))
                continue;

            if (outPts.Count == 0 || Vector2.DistanceSquared(outPts[^1], a) > 0.01f)
                outPts.Add(a);
            outPts.Add(b);
        }

        return outPts.Count >= 2 ? outPts.ToArray() : [];
    }
}
