using System.Numerics;

namespace CalypsoCad.Drawing;

/// <summary>Generic plane ∩ triangle mesh sectioning — produces cut edges from actual geometry.</summary>
internal static class MeshSectioner
{
    public readonly record struct SectionEdge(
        Vector3 A,
        Vector3 B,
        Guid SourceEntityId,
        string SourceName);

    public static IReadOnlyList<SectionEdge> Section(
        IReadOnlyList<DrawableGeometry> geometry,
        DrawingPlane plane)
    {
        var edges = new List<SectionEdge>();
        foreach (var g in geometry)
        {
            if (g.Vertices is not { Length: > 0 } verts || g.Indices is not { Length: >= 3 } idx)
                continue;

            for (var i = 0; i + 2 < idx.Length; i += 3)
            {
                var i0 = idx[i];
                var i1 = idx[i + 1];
                var i2 = idx[i + 2];
                if (i0 < 0 || i1 < 0 || i2 < 0
                    || i0 >= verts.Length || i1 >= verts.Length || i2 >= verts.Length)
                    continue;

                if (!TryIntersectTriangle(plane, verts[i0], verts[i1], verts[i2], out var a, out var b))
                    continue;
                if ((a - b).LengthSquared() < 1e-12f)
                    continue;
                edges.Add(new SectionEdge(a, b, g.SourceEntityId, g.SourceName));
            }
        }

        return edges;
    }

    private static bool TryIntersectTriangle(
        DrawingPlane plane,
        Vector3 p0,
        Vector3 p1,
        Vector3 p2,
        out Vector3 a,
        out Vector3 b)
    {
        a = default;
        b = default;
        var d0 = plane.SignedDistance(p0);
        var d1 = plane.SignedDistance(p1);
        var d2 = plane.SignedDistance(p2);

        // All on one side (strict) → no cut.
        if ((d0 > 1e-5f && d1 > 1e-5f && d2 > 1e-5f)
            || (d0 < -1e-5f && d1 < -1e-5f && d2 < -1e-5f))
            return false;

        // Coplanar triangle → emit edges lying in plane.
        if (Math.Abs(d0) <= 1e-5f && Math.Abs(d1) <= 1e-5f && Math.Abs(d2) <= 1e-5f)
        {
            a = p0;
            b = p1;
            return true;
        }

        Span<Vector3> hits = stackalloc Vector3[3];
        var n = 0;
        TryEdge(plane, p0, p1, d0, d1, hits, ref n);
        TryEdge(plane, p1, p2, d1, d2, hits, ref n);
        TryEdge(plane, p2, p0, d2, d0, hits, ref n);
        if (n < 2)
            return false;
        a = hits[0];
        b = hits[1];
        return true;
    }

    private static void TryEdge(
        DrawingPlane plane,
        Vector3 p,
        Vector3 q,
        float dp,
        float dq,
        Span<Vector3> hits,
        ref int n)
    {
        if (n >= 3)
            return;
        if (Math.Abs(dp) <= 1e-5f)
        {
            hits[n++] = p;
            return;
        }

        if (Math.Abs(dq) <= 1e-5f)
            return; // q captured when visited as start of next edge

        if (dp * dq >= 0)
            return;

        var t = dp / (dp - dq);
        hits[n++] = Vector3.Lerp(p, q, t);
        _ = plane;
    }
}
