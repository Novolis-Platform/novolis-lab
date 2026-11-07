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

        // Worst case: every cell straddles shore → 2×2 subdivide (8 tris × 3 verts).
        var tris = new WorldSolidVertex[cellsX * cellsZ * 24];
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
                var ca = ColorAt(a);
                var cb = ColorAt(b);
                var cc = ColorAt(c);
                var cd = ColorAt(d);

                // Same-kind cell: two tris. Mixed land/water: 2×2 subdivide so the shore
                // follows the mask more closely than the coarse heightfield grid.
                if (ca.Equals(cb) && cb.Equals(cc) && cc.Equals(cd))
                {
                    Write(tris, ref o, a, d, c, ca, cd, cc);
                    Write(tris, ref o, a, c, b, ca, cc, cb);
                    continue;
                }

                var ab = Mid(a, b);
                var bc = Mid(b, c);
                var cdMid = Mid(c, d);
                var da = Mid(d, a);
                var m = Mid(a, c);
                var cab = ColorAt(ab);
                var cbc = ColorAt(bc);
                var ccd = ColorAt(cdMid);
                var cda = ColorAt(da);
                var cm = ColorAt(m);

                Write(tris, ref o, a, da, m, ca, cda, cm);
                Write(tris, ref o, a, m, ab, ca, cm, cab);
                Write(tris, ref o, ab, m, bc, cab, cm, cbc);
                Write(tris, ref o, ab, bc, b, cab, cbc, cb);
                Write(tris, ref o, da, d, cdMid, cda, cd, ccd);
                Write(tris, ref o, da, cdMid, m, cda, ccd, cm);
                Write(tris, ref o, m, cdMid, c, cm, ccd, cc);
                Write(tris, ref o, m, c, bc, cm, cc, cbc);
            }
        }

        // Mixed cells use 8 tris; allocate for worst case then trim.
        if (o == tris.Length)
            return tris;
        var tight = new WorldSolidVertex[o];
        tris.AsSpan(0, o).CopyTo(tight);
        return tight;
    }

    private static Vector3 Mid(Vector3 p, Vector3 q) =>
        new((p.X + q.X) * 0.5f, (p.Y + q.Y) * 0.5f, (p.Z + q.Z) * 0.5f);

    private static Rgba32 ColorAt(Vector3 p) =>
        KristiansandSurface.KindAt(p.X, p.Z) == KristiansandSurfaceKind.Water ? Water : Land;

    private static void Write(
        WorldSolidVertex[] dest,
        ref int o,
        Vector3 p0,
        Vector3 p1,
        Vector3 p2,
        Rgba32 c0,
        Rgba32 c1,
        Rgba32 c2)
    {
        dest[o++] = new WorldSolidVertex(p0.X, p0.Y, p0.Z, c0);
        dest[o++] = new WorldSolidVertex(p1.X, p1.Y, p1.Z, c1);
        dest[o++] = new WorldSolidVertex(p2.X, p2.Y, p2.Z, c2);
    }
}
