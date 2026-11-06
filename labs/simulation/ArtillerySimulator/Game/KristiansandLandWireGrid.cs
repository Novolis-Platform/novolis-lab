using System.Drawing;
using System.Numerics;
using Novolis.Math.Geometry;

namespace ArtillerySimulator.Game;

/// <summary>Classic heightfield wires: green land, flat sea blue, elevated lake cyan.</summary>
internal static class KristiansandLandWireGrid
{
    private static readonly Rgba32 LandEast = new(58, 88, 52);
    private static readonly Rgba32 LandNorth = new(88, 132, 68);
    private static readonly Rgba32 SeaEast = new(48, 110, 210);
    private static readonly Rgba32 SeaNorth = new(81, 130, 255);
    private static readonly Rgba32 LakeEast = new(40, 160, 190);
    private static readonly Rgba32 LakeNorth = new(70, 200, 210);

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
        var kind = SegmentKind(a, b);
        var color = kind switch
        {
            KristiansandSurfaceKind.Sea => eastWest ? SeaEast : SeaNorth,
            KristiansandSurfaceKind.Lake => eastWest ? LakeEast : LakeNorth,
            _ => eastWest ? LandEast : LandNorth,
        };
        list.Add(new WorldLineVertex(a.X, a.Y, a.Z, color));
        list.Add(new WorldLineVertex(b.X, b.Y, b.Z, color));
    }

    private static KristiansandSurfaceKind SegmentKind(Vector3 a, Vector3 b)
    {
        var mid = (a + b) * 0.5f;
        var ka = KristiansandSurface.KindAt(a.X, a.Z);
        var kb = KristiansandSurface.KindAt(b.X, b.Z);
        var km = KristiansandSurface.KindAt(mid.X, mid.Z);
        if (ka != kb)
            return KristiansandSurfaceKind.Land;
        if (ka == KristiansandSurfaceKind.Sea && km == KristiansandSurfaceKind.Sea)
            return KristiansandSurfaceKind.Sea;
        if (ka == KristiansandSurfaceKind.Lake && km == KristiansandSurfaceKind.Lake)
            return KristiansandSurfaceKind.Lake;
        return KristiansandSurfaceKind.Land;
    }
}
