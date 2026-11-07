using System.Numerics;
using Novolis.Math.Geometry;

namespace CalypsoCad.Structure;

/// <summary>
/// Extrudes a planar polygon-with-holes into a watertight <see cref="EditableMesh"/>.
/// Plate plane is CAD (X=athwart Y, Y=up); extrusion is along CAD +Z (bow).
/// </summary>
internal static class PolygonWithHolesExtrude
{
    /// <summary>
    /// Builds a solid plate at <paramref name="zCad"/> ± half thickness along CAD Z.
    /// Outer loop and holes are (athwart Y, up). Outer should be CCW; holes CW preferred.
    /// </summary>
    public static EditableMesh ExtrudeAlongZ(
        IReadOnlyList<(float Y, float Up)> outer,
        IReadOnlyList<IReadOnlyList<(float Y, float Up)>> holes,
        float zCad,
        float halfThicknessZ)
    {
        ArgumentNullException.ThrowIfNull(outer);
        ArgumentNullException.ThrowIfNull(holes);
        if (outer.Count < 3)
            throw new ArgumentException("Outer loop needs ≥3 vertices.", nameof(outer));

        var outerPts = NormalizeRing(outer, wantCcw: true);
        if (outerPts.Count < 3)
            throw new InvalidOperationException("Bulkhead outer ring collapsed.");

        var holePts = MergeRectHoles(
                holes
                    .Where(h => h.Count >= 3)
                    .Select(h => NormalizeRing(h, wantCcw: false))
                    .Where(h => h.Count >= 3)
                    .ToList())
            .Select(h => ClipHoleToOuter(h, outerPts))
            .Where(h => h.Count >= 3)
            .ToList();

        var (flat, appliedHoles) = BridgeHoles(outerPts, holePts);
        var tris = Triangulate(flat);
        if (tris.Count == 0)
        {
            // Last resort: solid outer only (still polygonal — never AABB voxels).
            flat = outerPts;
            appliedHoles = [];
            tris = Triangulate(flat);
        }

        if (tris.Count == 0)
            throw new InvalidOperationException("Bulkhead plate triangulation produced no triangles.");

        var z0 = zCad - halfThicknessZ;
        var z1 = zCad + halfThicknessZ;
        var mesh = new EditableMesh();

        var front = new int[flat.Count];
        var rear = new int[flat.Count];
        for (var i = 0; i < flat.Count; i++)
        {
            var (y, up) = flat[i];
            front[i] = mesh.AddVertex(new Vector3(y, up, z0));
            rear[i] = mesh.AddVertex(new Vector3(y, up, z1));
        }

        foreach (var (a, b, c) in tris)
        {
            mesh.AddTriangle(front[a], front[c], front[b]);
            mesh.AddTriangle(rear[a], rear[b], rear[c]);
        }

        AddNamedLoopWalls(mesh, outerPts, z0, z1, inward: false);
        foreach (var hole in appliedHoles)
            AddNamedLoopWalls(mesh, hole, z0, z1, inward: true);

        return mesh;
    }

    private static void AddLoopWalls(EditableMesh mesh, int[] front, int[] rear, int count, bool reverse)
    {
        for (var i = 0; i < count; i++)
        {
            var j = (i + 1) % count;
            var a = front[i];
            var b = front[j];
            var c = rear[j];
            var d = rear[i];
            if (reverse)
            {
                mesh.AddTriangle(a, c, b);
                mesh.AddTriangle(a, d, c);
            }
            else
            {
                mesh.AddTriangle(a, b, c);
                mesh.AddTriangle(a, c, d);
            }
        }
    }

    private static void AddNamedLoopWalls(
        EditableMesh mesh,
        List<(float Y, float Up)> ring,
        float z0,
        float z1,
        bool inward)
    {
        var n = ring.Count;
        var f = new int[n];
        var r = new int[n];
        for (var i = 0; i < n; i++)
        {
            var (y, up) = ring[i];
            f[i] = mesh.AddVertex(new Vector3(y, up, z0));
            r[i] = mesh.AddVertex(new Vector3(y, up, z1));
        }

        AddLoopWalls(mesh, f, r, n, reverse: inward);
    }

    private static List<(float Y, float Up)> NormalizeRing(
        IReadOnlyList<(float Y, float Up)> ring, bool wantCcw)
    {
        var pts = new List<(float Y, float Up)>(ring.Count);
        for (var i = 0; i < ring.Count; i++)
        {
            var p = ring[i];
            if (pts.Count > 0)
            {
                var prev = pts[^1];
                if (MathF.Abs(prev.Y - p.Y) < 1e-5f && MathF.Abs(prev.Up - p.Up) < 1e-5f)
                    continue;
            }

            pts.Add(p);
        }

        if (pts.Count >= 2)
        {
            var a = pts[0];
            var b = pts[^1];
            if (MathF.Abs(a.Y - b.Y) < 1e-5f && MathF.Abs(a.Up - b.Up) < 1e-5f)
                pts.RemoveAt(pts.Count - 1);
        }

        if (pts.Count < 3)
            return pts;

        var area = SignedArea(pts);
        var isCcw = area > 0f;
        if (wantCcw != isCcw)
            pts.Reverse();
        return pts;
    }

    private static float SignedArea(IReadOnlyList<(float Y, float Up)> pts)
    {
        double a = 0;
        for (var i = 0; i < pts.Count; i++)
        {
            var p = pts[i];
            var q = pts[(i + 1) % pts.Count];
            a += (double)p.Y * q.Up - (double)q.Y * p.Up;
        }

        return (float)(0.5 * a);
    }

    /// <summary>Merge overlapping axis-aligned hole rectangles into fewer CW rings.</summary>
    private static List<List<(float Y, float Up)>> MergeRectHoles(List<List<(float Y, float Up)>> holes)
    {
        var boxes = new List<(float Y0, float Y1, float U0, float U1)>();
        foreach (var h in holes)
        {
            if (!TryAabb(h, out var y0, out var y1, out var u0, out var u1))
            {
                // Keep non-rect holes as-is.
                continue;
            }

            boxes.Add((y0, y1, u0, u1));
        }

        var nonRect = holes.Where(h => !TryAabb(h, out _, out _, out _, out _)).ToList();
        var merged = true;
        while (merged)
        {
            merged = false;
            for (var i = 0; i < boxes.Count && !merged; i++)
            for (var j = i + 1; j < boxes.Count; j++)
            {
                var a = boxes[i];
                var b = boxes[j];
                var overlapY = a.Y0 <= b.Y1 + 0.05f && b.Y0 <= a.Y1 + 0.05f;
                var overlapU = a.U0 <= b.U1 + 0.05f && b.U0 <= a.U1 + 0.05f;
                if (!overlapY || !overlapU)
                    continue;
                boxes[i] = (
                    MathF.Min(a.Y0, b.Y0), MathF.Max(a.Y1, b.Y1),
                    MathF.Min(a.U0, b.U0), MathF.Max(a.U1, b.U1));
                boxes.RemoveAt(j);
                merged = true;
                break;
            }
        }

        var result = nonRect;
        foreach (var (y0, y1, u0, u1) in boxes)
        {
            if (y1 - y0 < 0.08f || u1 - u0 < 0.08f)
                continue;
            // CW rectangle for hole.
            result.Add([(y0, u0), (y0, u1), (y1, u1), (y1, u0)]);
        }

        return result;
    }

    private static bool TryAabb(
        List<(float Y, float Up)> ring,
        out float y0, out float y1, out float u0, out float u1)
    {
        y0 = float.PositiveInfinity;
        y1 = float.NegativeInfinity;
        u0 = float.PositiveInfinity;
        u1 = float.NegativeInfinity;
        foreach (var (y, up) in ring)
        {
            y0 = MathF.Min(y0, y);
            y1 = MathF.Max(y1, y);
            u0 = MathF.Min(u0, up);
            u1 = MathF.Max(u1, up);
        }

        if (ring.Count is < 4 or > 6)
            return false;
        // Nearly rectangular: all verts near AABB corners.
        foreach (var (y, up) in ring)
        {
            var nearY = MathF.Abs(y - y0) < 0.04f || MathF.Abs(y - y1) < 0.04f;
            var nearU = MathF.Abs(up - u0) < 0.04f || MathF.Abs(up - u1) < 0.04f;
            if (!nearY || !nearU)
                return false;
        }

        return y1 > y0 + 0.08f && u1 > u0 + 0.08f;
    }

    private static List<(float Y, float Up)> ClipHoleToOuter(
        List<(float Y, float Up)> hole,
        List<(float Y, float Up)> outer)
    {
        // Keep holes whose centroid is inside outer; shrink slightly so bridges stay clear.
        var cy = hole.Average(p => p.Y);
        var cu = hole.Average(p => p.Up);
        if (!PointInPolygon((cy, cu), outer))
            return [];

        if (!TryAabb(hole, out var y0, out var y1, out var u0, out var u1))
            return hole;

        const float inset = 0.04f;
        y0 += inset;
        y1 -= inset;
        u0 += inset;
        u1 -= inset;
        if (y1 <= y0 + 0.1f || u1 <= u0 + 0.1f)
            return [];
        return [(y0, u0), (y0, u1), (y1, u1), (y1, u0)];
    }

    private static bool PointInPolygon((float Y, float Up) p, List<(float Y, float Up)> ring)
    {
        var inside = false;
        for (int i = 0, j = ring.Count - 1; i < ring.Count; j = i++)
        {
            var pi = ring[i];
            var pj = ring[j];
            if (((pi.Up > p.Up) != (pj.Up > p.Up))
                && (p.Y < (pj.Y - pi.Y) * (p.Up - pi.Up) / (pj.Up - pi.Up + 1e-12f) + pi.Y))
                inside = !inside;
        }

        return inside;
    }

    private static (List<(float Y, float Up)> Flat, List<List<(float Y, float Up)>> Applied) BridgeHoles(
        List<(float Y, float Up)> outer,
        List<List<(float Y, float Up)>> holes)
    {
        var poly = new List<(float Y, float Up)>(outer);
        var applied = new List<List<(float Y, float Up)>>();
        foreach (var hole in holes.OrderByDescending(h => MathF.Abs(SignedArea(h))))
        {
            if (!TryBridge(poly, hole, out var bridged))
                continue;
            var tris = Triangulate(bridged);
            if (tris.Count == 0)
                continue; // skip hole that breaks triangulation
            poly = bridged;
            applied.Add(hole);
        }

        return (poly, applied);
    }

    private static bool TryBridge(
        List<(float Y, float Up)> outer,
        List<(float Y, float Up)> hole,
        out List<(float Y, float Up)> bridged)
    {
        bridged = [];
        var bestDist = float.PositiveInfinity;
        var bestOi = -1;
        var bestHi = -1;
        for (var oi = 0; oi < outer.Count; oi++)
        for (var hi = 0; hi < hole.Count; hi++)
        {
            var o = outer[oi];
            var h = hole[hi];
            var d = (o.Y - h.Y) * (o.Y - h.Y) + (o.Up - h.Up) * (o.Up - h.Up);
            if (d >= bestDist)
                continue;
            if (BridgeCrosses(outer, oi, hole, hi))
                continue;
            bestDist = d;
            bestOi = oi;
            bestHi = hi;
        }

        if (bestOi < 0)
            return false;

        bridged = new List<(float Y, float Up)>(outer.Count + hole.Count + 2);
        for (var i = 0; i <= bestOi; i++)
            bridged.Add(outer[i]);
        for (var k = 0; k <= hole.Count; k++)
            bridged.Add(hole[(bestHi + k) % hole.Count]);
        for (var i = bestOi; i < outer.Count; i++)
            bridged.Add(outer[i]);
        return bridged.Count >= 3;
    }

    private static bool BridgeCrosses(
        List<(float Y, float Up)> outer, int oi,
        List<(float Y, float Up)> hole, int hi)
    {
        var a = outer[oi];
        var b = hole[hi];
        for (var i = 0; i < outer.Count; i++)
        {
            if (i == oi || (i + 1) % outer.Count == oi)
                continue;
            if (SegmentsIntersect(a, b, outer[i], outer[(i + 1) % outer.Count]))
                return true;
        }

        for (var i = 0; i < hole.Count; i++)
        {
            if (i == hi || (i + 1) % hole.Count == hi)
                continue;
            if (SegmentsIntersect(a, b, hole[i], hole[(i + 1) % hole.Count]))
                return true;
        }

        return false;
    }

    private static bool SegmentsIntersect(
        (float Y, float Up) a, (float Y, float Up) b,
        (float Y, float Up) c, (float Y, float Up) d)
    {
        static float Cross((float Y, float Up) u, (float Y, float Up) v) => u.Y * v.Up - u.Up * v.Y;
        static (float Y, float Up) Sub((float Y, float Up) p, (float Y, float Up) q) => (p.Y - q.Y, p.Up - q.Up);

        var ab = Sub(b, a);
        var ac = Sub(c, a);
        var ad = Sub(d, a);
        var cd = Sub(d, c);
        var ca = Sub(a, c);
        var cb = Sub(b, c);
        var d1 = Cross(ab, ac);
        var d2 = Cross(ab, ad);
        var d3 = Cross(cd, ca);
        var d4 = Cross(cd, cb);
        return ((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0))
               && ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0));
    }

    private static List<(int A, int B, int C)> Triangulate(List<(float Y, float Up)> poly)
    {
        if (poly.Count < 3)
            return [];
        // Convex outer (octagon): fan is exact and stable.
        if (IsConvex(poly))
        {
            var fan = new List<(int, int, int)>(poly.Count - 2);
            for (var i = 1; i < poly.Count - 1; i++)
                fan.Add((0, i, i + 1));
            return fan;
        }

        return EarClip(poly);
    }

    private static bool IsConvex(List<(float Y, float Up)> poly)
    {
        var n = poly.Count;
        var sign = 0;
        for (var i = 0; i < n; i++)
        {
            var a = poly[i];
            var b = poly[(i + 1) % n];
            var c = poly[(i + 2) % n];
            var cross = (b.Y - a.Y) * (c.Up - b.Up) - (b.Up - a.Up) * (c.Y - b.Y);
            if (MathF.Abs(cross) < 1e-8f)
                continue;
            var s = cross > 0 ? 1 : -1;
            if (sign == 0)
                sign = s;
            else if (s != sign)
                return false;
        }

        return sign != 0;
    }

    private static List<(int A, int B, int C)> EarClip(List<(float Y, float Up)> poly)
    {
        var n = poly.Count;
        var tris = new List<(int, int, int)>();
        if (n < 3)
            return tris;

        var idx = Enumerable.Range(0, n).ToList();
        var guard = 0;
        while (idx.Count > 3 && guard++ < n * n + 32)
        {
            var clipped = false;
            for (var i = 0; i < idx.Count; i++)
            {
                var i0 = idx[(i + idx.Count - 1) % idx.Count];
                var i1 = idx[i];
                var i2 = idx[(i + 1) % idx.Count];
                if (!IsEar(poly, idx, i0, i1, i2))
                    continue;
                tris.Add((i0, i1, i2));
                idx.RemoveAt(i);
                clipped = true;
                break;
            }

            if (!clipped)
                break;
        }

        if (idx.Count == 3)
            tris.Add((idx[0], idx[1], idx[2]));
        return tris;
    }

    private static bool IsEar(
        List<(float Y, float Up)> poly,
        List<int> idx,
        int i0, int i1, int i2)
    {
        var a = poly[i0];
        var b = poly[i1];
        var c = poly[i2];
        var cross = (b.Y - a.Y) * (c.Up - b.Up) - (b.Up - a.Up) * (c.Y - b.Y);
        if (cross <= 1e-8f)
            return false;

        foreach (var k in idx)
        {
            if (k == i0 || k == i1 || k == i2)
                continue;
            if (PointInTriangle(poly[k], a, b, c))
                return false;
        }

        return true;
    }

    private static bool PointInTriangle(
        (float Y, float Up) p,
        (float Y, float Up) a,
        (float Y, float Up) b,
        (float Y, float Up) c)
    {
        static float Sign((float Y, float Up) p1, (float Y, float Up) p2, (float Y, float Up) p3) =>
            (p1.Y - p3.Y) * (p2.Up - p3.Up) - (p2.Y - p3.Y) * (p1.Up - p3.Up);

        var d1 = Sign(p, a, b);
        var d2 = Sign(p, b, c);
        var d3 = Sign(p, c, a);
        var hasNeg = d1 < 0 || d2 < 0 || d3 < 0;
        var hasPos = d1 > 0 || d2 > 0 || d3 > 0;
        return !(hasNeg && hasPos);
    }
}
