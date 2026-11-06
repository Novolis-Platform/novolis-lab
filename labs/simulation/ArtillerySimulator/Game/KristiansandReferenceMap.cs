using System.Drawing;
using System.Numerics;
using Novolis.Raylib.Game;

namespace ArtillerySimulator.Game;

/// <summary>North-up Norgeskart table (colored voxels on a fixed Y plane).</summary>
internal static class KristiansandReferenceMap
{
    public const float TableYMeters = 4f;
    private const int CellsX = 112;

    private static readonly KristiansandHeightSampler Sampler = new();

    public static bool IsActive => KristiansandHeightSampler.EastMeters > 0f;

    public static bool TryDraw(RayGameContext ctx)
    {
        var east = KristiansandHeightSampler.EastMeters;
        var north = KristiansandHeightSampler.NorthMeters;
        if (east <= 0f || north <= 0f)
            return false;

        var cellsZ = Math.Clamp((int)MathF.Round(CellsX * (north / east)), 56, 80);
        const float thickness = 0.55f;
        const float inset = 0.99f;

        for (var j = 0; j < cellsZ; j++)
        {
            for (var i = 0; i < CellsX; i++)
            {
                var x0 = i / (float)CellsX * east;
                var x1 = (i + 1) / (float)CellsX * east;
                var z0 = j / (float)cellsZ * north;
                var z1 = (j + 1) / (float)cellsZ * north;
                var cx = (x0 + x1) * 0.5f;
                var cz = (z0 + z1) * 0.5f;
                var color = ColorAt(cx, cz);
                var size = new Vector3((x1 - x0) * inset, thickness, (z1 - z0) * inset);
                ctx.DrawShipBox(new Vector3(cx, TableYMeters, cz), size, color);
            }
        }

        return true;
    }

    private static Color ColorAt(float x, float z)
    {
        if (KristiansandReferenceColors.TrySample(x, z, out var relief))
        {
            if (IsNorgeskartWater(relief))
                return KristiansandMapRaster.NorgeskartWater;

            return relief;
        }

        if (Sampler.SampleHeight(x, z) <= 0.05f)
            return KristiansandMapRaster.NorgeskartWater;

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
}
