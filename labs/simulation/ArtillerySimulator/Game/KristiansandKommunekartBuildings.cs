using System.Drawing;
using System.Numerics;
using Novolis.Math.Geometry;

namespace ArtillerySimulator.Game;

/// <summary>
/// Wireframe buildings from Kommunekart 3D / Norkart Cesium 3D Tiles (FKB-derived LOD).
/// Built by <c>scripts/fetch-kommunekart-buildings.cs</c> → <c>map/layers/kommunekart-buildings.wires.bin</c>.
/// </summary>
internal static class KristiansandKommunekartBuildings
{
    private static readonly Rgba32 Wire = new(236, 236, 240);
    private static readonly Color WireColor = Color.FromArgb(255, 236, 236, 240);

    private static WorldLineVertex[]? _wires;
    private static bool _loadAttempted;

    /// <summary>Hard-off: Norkart b3dm decode still produces garbage wires.</summary>
    public const bool Enabled = false;

    public static bool IsAvailable => Enabled && EnsureLoaded() && _wires is { Length: > 0 };

    public static void Draw(SilkWireBatch wires, TerrainWorld terrain)
    {
        _ = terrain;
        if (!EnsureLoaded() || _wires is null || _wires.Length == 0)
            return;

        // Already world-space segments; append into the dynamic batch via Line for color consistency.
        for (var i = 0; i + 1 < _wires.Length; i += 2)
        {
            var a = _wires[i];
            var b = _wires[i + 1];
            wires.Line(
                new Vector3(a.X, a.Y, a.Z),
                new Vector3(b.X, b.Y, b.Z),
                WireColor);
        }
    }

    /// <summary>Cached vertices for a one-shot DrawWorldLines call (preferred when large).</summary>
    public static ReadOnlySpan<WorldLineVertex> CachedWires
    {
        get
        {
            EnsureLoaded();
            return _wires ?? [];
        }
    }

    private static bool EnsureLoaded()
    {
        if (_loadAttempted)
            return _wires is not null;
        _loadAttempted = true;

        foreach (var path in CandidatePaths())
        {
            if (!File.Exists(path))
                continue;
            try
            {
                _wires = Load(path);
                return _wires.Length > 0;
            }
            catch
            {
                _wires = null;
            }
        }

        return false;
    }

    private static IEnumerable<string> CandidatePaths()
    {
        yield return Path.Combine(AppContext.BaseDirectory, "map", "layers", "kommunekart-buildings.wires.bin");
        yield return Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "map", "layers", "kommunekart-buildings.wires.bin"));
        yield return @"d:\novolis\novolis-lab\labs\simulation\ArtillerySimulator\map\layers\kommunekart-buildings.wires.bin";
    }

    private static WorldLineVertex[] Load(string path)
    {
        using var fs = File.OpenRead(path);
        using var r = new BinaryReader(fs);
        if (r.ReadUInt32() != 0x57424B4Bu || r.ReadUInt16() != 1)
            throw new InvalidDataException(path);
        var count = r.ReadInt32();
        if (count <= 0 || count > 5_000_000)
            throw new InvalidDataException($"edge count {count}");
        var verts = new WorldLineVertex[count * 2];
        for (var i = 0; i < count; i++)
        {
            var ax = r.ReadSingle();
            var ay = r.ReadSingle();
            var az = r.ReadSingle();
            var bx = r.ReadSingle();
            var by = r.ReadSingle();
            var bz = r.ReadSingle();
            verts[i * 2] = new WorldLineVertex(ax, ay, az, Wire);
            verts[i * 2 + 1] = new WorldLineVertex(bx, by, bz, Wire);
        }

        return verts;
    }
}
