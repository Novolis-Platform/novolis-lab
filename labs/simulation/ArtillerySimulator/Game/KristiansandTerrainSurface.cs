using System.Drawing;
using System.Numerics;
using Novolis.Physics.Abstractions;
using Novolis.Raylib.Game;

namespace ArtillerySimulator.Game;

/// <summary>Draped shaded terrain mesh (Norgeskart relief tint + bilinear DEM). Replaces the flat voxel table.</summary>
internal static class KristiansandTerrainSurface
{
    private const int CellsX = 448;

    private static TerrainTriangle[]? _triangles;
    private static float _cachedEast;
    private static float _cachedNorth;

    public static void Invalidate() => _triangles = null;

    public static void Draw(RayGameContext ctx, IHeightSampler field)
    {
        var east = KristiansandHeightSampler.EastMeters;
        var north = KristiansandHeightSampler.NorthMeters;
        if (_triangles is null || east != _cachedEast || north != _cachedNorth)
        {
            _cachedEast = east;
            _cachedNorth = north;
            _triangles = Build(field);
        }
        foreach (var tri in _triangles)
            ctx.DrawTriangle(tri.A, tri.B, tri.C, tri.Color);
    }

    private static TerrainTriangle[] Build(IHeightSampler field)
    {
        var east = KristiansandHeightSampler.EastMeters;
        var north = KristiansandHeightSampler.NorthMeters;
        var cellsZ = Math.Max(32, (int)MathF.Round(CellsX * (north / east)));
        var list = new List<TerrainTriangle>(CellsX * cellsZ * 2);
        for (var j = 0; j < cellsZ; j++)
        {
            for (var i = 0; i < CellsX; i++)
            {
                var x0 = i / (float)CellsX * east;
                var x1 = (i + 1) / (float)CellsX * east;
                var z0 = j / (float)cellsZ * north;
                var z1 = (j + 1) / (float)cellsZ * north;
                AddCell(list, field, x0, z0, x1, z0, x0, z1, x1, z1);
            }
        }

        return list.ToArray();
    }

    private static void AddCell(
        List<TerrainTriangle> list,
        IHeightSampler field,
        float x0,
        float z0,
        float x1,
        float z0End,
        float x0End,
        float z1,
        float x1End,
        float z1End)
    {
        var cx = (x0 + x1End) * 0.5f;
        var cz = (z0 + z1End) * 0.5f;
        if (KristiansandLandSeaMask.IsWater(cx, cz))
        {
            const float seaY = 0.25f;
            var w = KristiansandMapRaster.NorgeskartWater;
            var v00 = new Vector3(x0, seaY, z0);
            var v10 = new Vector3(x1End, seaY, z0);
            var v01 = new Vector3(x0, seaY, z1End);
            var v11 = new Vector3(x1End, seaY, z1End);
            list.Add(new TerrainTriangle(v00, v10, v01, w));
            list.Add(new TerrainTriangle(v10, v11, v01, w));
            return;
        }

        var h00 = field.SampleHeight(x0, z0);
        var h10 = field.SampleHeight(x1End, z0);
        var h01 = field.SampleHeight(x0, z1End);
        var h11 = field.SampleHeight(x1End, z1End);
        var c00 = LandColorAt(x0, z0);
        var v00l = new Vector3(x0, h00 + 0.35f, z0);
        var v10l = new Vector3(x1End, h10 + 0.35f, z0);
        var v01l = new Vector3(x0, h01 + 0.35f, z1End);
        var v11l = new Vector3(x1End, h11 + 0.35f, z1End);
        list.Add(new TerrainTriangle(v00l, v10l, v01l, c00));
        list.Add(new TerrainTriangle(v10l, v11l, v01l, LandColorAt(x1End, z1End)));
    }

    private static Color LandColorAt(float x, float z)
    {
        if (KristiansandReferenceColors.TrySample(x, z, out var relief) && !IsNorgeskartWater(relief))
            return relief;

        return KristiansandMapRaster.TerrainColorAt(x, z);
    }

    private static bool IsNorgeskartWater(Color c)
    {
        if (c.R + c.G + c.B < 36)
            return true;

        const int tr = 0x51;
        const int tg = 0x82;
        const int tb = 0xFF;
        var dr = c.R - tr;
        var dg = c.G - tg;
        var db = c.B - tb;
        return dr * dr + dg * dg + db * db <= 40 * 40;
    }

    private readonly record struct TerrainTriangle(Vector3 A, Vector3 B, Vector3 C, Color Color);
}
