using System.Drawing;
using System.Numerics;
using Novolis.Math.Geometry;

namespace ArtillerySimulator.Game;

/// <summary>Heightfield wires: green land, one blue for all water.</summary>
internal static class KristiansandLandWireGrid
{
    private static readonly Rgba32 LandEast = new(58, 88, 52);
    private static readonly Rgba32 LandNorth = new(88, 132, 68);
    private static readonly Rgba32 Water = new(48, 110, 210);

    /// <summary>Build once after mesh rebuild — never call KindAt per frame.</summary>
    public static WorldLineVertex[] Build(ReadOnlySpan<Vector3> vertices, int cellsX, int cellsZ)
    {
        var stride = cellsX + 1;
        var list = new List<WorldLineVertex>(cellsX * stride * 4);
        for (var z = 0; z <= cellsZ; z++)
        {
            for (var x = 0; x < cellsX; x++)
            {
                var i0 = z * stride + x;
                Add(list, vertices[i0], vertices[i0 + 1], eastWest: true);
            }
        }

        for (var x = 0; x <= cellsX; x++)
        {
            for (var z = 0; z < cellsZ; z++)
            {
                var i0 = z * stride + x;
                Add(list, vertices[i0], vertices[i0 + stride], eastWest: false);
            }
        }

        return list.ToArray();
    }

    private static void Add(List<WorldLineVertex> list, Vector3 a, Vector3 b, bool eastWest)
    {
        var water = IsWaterSegment(a, b);
        var color = water
            ? Water
            : eastWest ? LandEast : LandNorth;
        list.Add(new WorldLineVertex(a.X, a.Y, a.Z, color));
        list.Add(new WorldLineVertex(b.X, b.Y, b.Z, color));
    }

    private static bool IsWaterSegment(Vector3 a, Vector3 b)
    {
        var ka = KristiansandSurface.KindAt(a.X, a.Z);
        var kb = KristiansandSurface.KindAt(b.X, b.Z);
        // Both ends water → water. Mixed shore edges stay land green
        // so land→water drops are not drawn as tall blue walls.
        return ka == KristiansandSurfaceKind.Water && kb == KristiansandSurfaceKind.Water;
    }
}
