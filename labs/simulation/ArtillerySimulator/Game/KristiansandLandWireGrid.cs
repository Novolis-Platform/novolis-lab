using System.Drawing;
using System.Numerics;
using Novolis.Raylib.Game;

namespace ArtillerySimulator.Game;

/// <summary>Classic heightfield wires: green land, blue water, no filled surface.</summary>
internal static class KristiansandLandWireGrid
{
    private static readonly Color LandLow = Color.FromArgb(255, 58, 88, 52);
    private static readonly Color LandHigh = Color.FromArgb(255, 88, 132, 68);
    private static readonly Color WaterLow = Color.FromArgb(255, 48, 110, 210);
    private static readonly Color WaterHigh = Color.FromArgb(255, 81, 130, 255);

    public static void Draw(RayGameContext ctx, ReadOnlySpan<Vector3> vertices, int cellsX, int cellsZ)
    {
        var stride = cellsX + 1;
        for (var z = 0; z <= cellsZ; z++)
        {
            for (var x = 0; x < cellsX; x++)
            {
                var i0 = z * stride + x;
                DrawSegment(ctx, vertices[i0], vertices[i0 + 1], eastWest: true);
            }
        }

        for (var x = 0; x <= cellsX; x++)
        {
            for (var z = 0; z < cellsZ; z++)
            {
                var i0 = z * stride + x;
                DrawSegment(ctx, vertices[i0], vertices[i0 + stride], eastWest: false);
            }
        }
    }

    private static void DrawSegment(RayGameContext ctx, Vector3 a, Vector3 b, bool eastWest)
    {
        var water = SegmentOnWater(a, b);
        var color = water
            ? (eastWest ? WaterLow : WaterHigh)
            : (eastWest ? LandLow : LandHigh);
        ctx.DrawBolt(a, b, color);
    }

    private static bool SegmentOnWater(Vector3 a, Vector3 b)
    {
        var mid = (a + b) * 0.5f;
        var aWater = KristiansandLandSeaMask.IsWater(a.X, a.Z);
        var bWater = KristiansandLandSeaMask.IsWater(b.X, b.Z);
        var midWater = KristiansandLandSeaMask.IsWater(mid.X, mid.Z);
        // Coast edges count as land so the shoreline reads green.
        if (aWater != bWater)
            return false;
        return aWater && midWater;
    }
}
