using System.Drawing;
using System.Numerics;
using Novolis.Raylib.Game;

namespace ArtillerySimulator.Game;

/// <summary>Heightfield wires on land only; open water has no wire grid.</summary>
internal static class KristiansandLandWireGrid
{
    private static readonly Color WireLow = Color.FromArgb(255, 58, 88, 52);
    private static readonly Color WireHigh = Color.FromArgb(255, 88, 132, 68);

    public static void Draw(RayGameContext ctx, ReadOnlySpan<Vector3> vertices, int cellsX, int cellsZ)
    {
        var stride = cellsX + 1;
        for (var z = 0; z < cellsZ; z++)
        {
            for (var x = 0; x < cellsX; x++)
            {
                var i00 = z * stride + x;
                var i10 = i00 + 1;
                var i01 = i00 + stride;
                if (SegmentOnLand(vertices[i00], vertices[i10]))
                    ctx.DrawBolt(vertices[i00], vertices[i10], WireLow);
                if (SegmentOnLand(vertices[i00], vertices[i01]))
                    ctx.DrawBolt(vertices[i00], vertices[i01], WireLow);
            }
        }

        for (var x = 0; x <= cellsX; x++)
        {
            for (var z = 0; z < cellsZ; z++)
            {
                var a = vertices[z * stride + x];
                var b = vertices[(z + 1) * stride + x];
                if (SegmentOnLand(a, b))
                    ctx.DrawBolt(a, b, WireHigh);
            }
        }
    }

    private static bool SegmentOnLand(Vector3 a, Vector3 b)
    {
        if (KristiansandLandSeaMask.IsWater(a.X, a.Z) || KristiansandLandSeaMask.IsWater(b.X, b.Z))
            return false;

        var mid = (a + b) * 0.5f;
        return !KristiansandLandSeaMask.IsWater(mid.X, mid.Z);
    }
}
