using System.Numerics;
using Novolis.Math.Geometry;

namespace ArtillerySimulator;

/// <summary>
/// Solid heightfield fill: flat land green / water blue. Geometry uses NDH heights; color is mask-only.
/// </summary>
internal static class KristiansandLandFillMesh
{
    private static readonly Rgba32 Land = new(62, 110, 58, 255);
    private static readonly Rgba32 Water = new(42, 98, 178, 255);

    /// <summary>
    /// Builds triangle vertices for a <paramref name="cellsX"/>×<paramref name="cellsZ"/> heightfield grid
    /// (<paramref name="vertices"/> length = (cellsX+1)*(cellsZ+1), row-major Z then X).
    /// </summary>
    public static WorldSolidVertex[] Build(ReadOnlySpan<Vector3> vertices, int cellsX, int cellsZ)
    {
        if (cellsX < 1 || cellsZ < 1)
            return [];

        var stride = cellsX + 1;
        if (vertices.Length < stride * (cellsZ + 1))
            throw new ArgumentException("Vertex buffer shorter than cells imply.", nameof(vertices));

        var tris = new WorldSolidVertex[cellsX * cellsZ * 6];
        var o = 0;
        for (var z = 0; z < cellsZ; z++)
        {
            for (var x = 0; x < cellsX; x++)
            {
                var i00 = z * stride + x;
                var i10 = i00 + 1;
                var i01 = i00 + stride;
                var i11 = i01 + 1;
                var a = vertices[i00];
                var b = vertices[i10];
                var c = vertices[i11];
                var d = vertices[i01];

                // Cell-center mask pick — no slope / elevation tint.
                var cx = (a.X + b.X + c.X + d.X) * 0.25f;
                var cz = (a.Z + b.Z + c.Z + d.Z) * 0.25f;
                var color = KristiansandSurface.KindAt(cx, cz) == KristiansandSurfaceKind.Water
                    ? Water
                    : Land;

                // CCW from above (+Y outward): a→d→c and a→c→b.
                Write(tris, ref o, a, d, c, color);
                Write(tris, ref o, a, c, b, color);
            }
        }

        return tris;
    }

    private static void Write(
        WorldSolidVertex[] dest,
        ref int o,
        Vector3 p0,
        Vector3 p1,
        Vector3 p2,
        Rgba32 color)
    {
        dest[o++] = new WorldSolidVertex(p0.X, p0.Y, p0.Z, color);
        dest[o++] = new WorldSolidVertex(p1.X, p1.Y, p1.Z, color);
        dest[o++] = new WorldSolidVertex(p2.X, p2.Y, p2.Z, color);
    }
}
