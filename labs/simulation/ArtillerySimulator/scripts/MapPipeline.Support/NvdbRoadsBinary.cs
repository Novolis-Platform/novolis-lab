using System.Buffers.Binary;
using System.Text;

namespace ArtillerySimulator.MapPipeline;

/// <summary>Versioned stroke cache — sole road authority for Silk / mission (never PNG).</summary>
public static class NvdbRoadsBinary
{
    public const string Magic = "NVDBROAD";
    public const uint Version = 1;
    public const byte FlagMain = 1;

    public readonly record struct Segment(byte Vegkategori, bool IsMain, (float X, float Z)[] Points);

    public static void Write(string path, IReadOnlyList<Segment> segments)
    {
        using var fs = File.Create(path);
        using var bw = new BinaryWriter(fs, Encoding.ASCII, leaveOpen: false);
        bw.Write(Encoding.ASCII.GetBytes(Magic));
        bw.Write(Version);
        bw.Write((uint)segments.Count);
        foreach (var seg in segments)
        {
            if (seg.Points.Length is < 2 or > ushort.MaxValue)
                continue;
            bw.Write(seg.Vegkategori);
            bw.Write(seg.IsMain ? FlagMain : (byte)0);
            bw.Write((ushort)seg.Points.Length);
            foreach (var (x, z) in seg.Points)
            {
                bw.Write(x);
                bw.Write(z);
            }
        }
    }

    public static List<Segment> Read(string path)
    {
        var bytes = File.ReadAllBytes(path);
        if (bytes.Length < 16)
            throw new InvalidDataException("nvdb-roads.bin too short.");
        var magic = Encoding.ASCII.GetString(bytes, 0, 8);
        if (magic != Magic)
            throw new InvalidDataException($"Bad nvdb-roads magic '{magic}'.");
        var version = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(8));
        if (version != Version)
            throw new InvalidDataException($"Unsupported nvdb-roads version {version}.");
        var count = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(12));
        var list = new List<Segment>((int)count);
        var o = 16;
        for (var i = 0; i < count; i++)
        {
            if (o + 4 > bytes.Length)
                break;
            var kat = bytes[o++];
            var flags = bytes[o++];
            var n = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(o));
            o += 2;
            if (o + n * 8 > bytes.Length)
                break;
            var pts = new (float X, float Z)[n];
            for (var p = 0; p < n; p++)
            {
                pts[p] = (
                    BitConverter.ToSingle(bytes, o),
                    BitConverter.ToSingle(bytes, o + 4));
                o += 8;
            }

            if (n >= 2)
                list.Add(new Segment(kat, (flags & FlagMain) != 0, pts));
        }

        return list;
    }
}
