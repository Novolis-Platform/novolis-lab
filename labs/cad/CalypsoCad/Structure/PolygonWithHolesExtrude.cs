using System.Numerics;
using Novolis.Math.Geometry;

namespace CalypsoCad.Structure;

/// <summary>
/// Extrudes a planar polygon-with-holes into a watertight <see cref="EditableMesh"/>.
/// Plate plane is CAD (X=athwart Y, Y=up); extrusion is along CAD +Z (bow).
/// Fail-closed: never silently omit openings or fall back to a solid outer plate.
/// </summary>
internal static class PolygonWithHolesExtrude
{
    public sealed record ExtrudeReport(
        string PartId,
        int ExpectedOpenings,
        int AppliedOpenings,
        float MaterialAreaM2,
        float ThicknessM,
        float ExpectedVolumeM3,
        float MeshVolumeM3,
        int TriangleCount,
        int VertexCount,
        int OpenBoundaryEdges,
        int NonManifoldEdges,
        int DegenerateTriangles,
        IReadOnlyList<string> Checks);

    /// <summary>
    /// Builds a solid plate at <paramref name="zCad"/> ± half thickness along CAD Z.
    /// Outer loop and holes are (athwart Y, up). Outer should be CCW; holes CW preferred.
    /// </summary>
    public static EditableMesh ExtrudeAlongZ(
        IReadOnlyList<(float Y, float Up)> outer,
        IReadOnlyList<IReadOnlyList<(float Y, float Up)>> holes,
        float zCad,
        float halfThicknessZ,
        string? partId = null) =>
        ExtrudeAlongZ(outer, holes, zCad, halfThicknessZ, partId, out _);

    public static EditableMesh ExtrudeAlongZ(
        IReadOnlyList<(float Y, float Up)> outer,
        IReadOnlyList<IReadOnlyList<(float Y, float Up)>> holes,
        float zCad,
        float halfThicknessZ,
        string? partId,
        out ExtrudeReport report)
    {
        ArgumentNullException.ThrowIfNull(outer);
        ArgumentNullException.ThrowIfNull(holes);
        var id = string.IsNullOrWhiteSpace(partId) ? "bulkhead" : partId;
        if (outer.Count < 3)
            throw new ArgumentException($"{id}: outer loop needs ≥3 vertices.", nameof(outer));
        if (halfThicknessZ <= 1e-5f)
            throw new ArgumentOutOfRangeException(nameof(halfThicknessZ), $"{id}: plate thickness must be positive.");

        var outerPts = NormalizeRing(outer, wantCcw: true);
        if (outerPts.Count < 3)
            throw new InvalidOperationException($"{id}: bulkhead outer ring collapsed.");

        var holePts = new List<List<(float Y, float Up)>>(holes.Count);
        for (var hi = 0; hi < holes.Count; hi++)
        {
            var raw = holes[hi];
            if (raw is null || raw.Count < 3)
            {
                throw new InvalidOperationException(
                    $"{id}: opening[{hi}] has {raw?.Count ?? 0} vertices (need ≥3). Coords={FormatRing(raw)}.");
            }

            var normalized = NormalizeRing(raw, wantCcw: false);
            if (normalized.Count < 3)
            {
                throw new InvalidOperationException(
                    $"{id}: opening[{hi}] collapsed during normalize. Coords={FormatRing(raw)}.");
            }

            var clipped = ClipHoleToOuter(normalized, outerPts, id, hi);
            holePts.Add(clipped);
        }

        // Union overlapping axis-aligned openings (geometry), never drop a required opening.
        holePts = MergeRectHoles(holePts, id);
        var expectedOpenings = holePts.Count;

        // Exact rectangle punch — no bridge/keyhole, no silent hole skip, no solid-outer fallback.
        var (flat, tris) = TriangulateOuterMinusRectHoles(outerPts, holePts, id);
        ValidateMaterialCoverage(tris, flat, outerPts, holePts, id, out var materialArea);

        var thickness = halfThicknessZ * 2f;
        var z0 = zCad - halfThicknessZ;
        var z1 = zCad + halfThicknessZ;
        var mesh = BuildWatertightMesh(flat, tris, outerPts, holePts, z0, z1);

        mesh = MeshWeld.Apply(mesh, new WeldOptions(1e-4f, WeldPositionMode.Average, WeldScope.EntireMesh));
        StripDegenerateTriangles(mesh);
        ValidateClosedSolid(mesh, materialArea, thickness, id,
            out var meshVol, out var openEdges, out var nonManifold, out var degenerates, out var checks);

        report = new ExtrudeReport(
            id,
            expectedOpenings,
            holePts.Count,
            materialArea,
            thickness,
            materialArea * thickness,
            meshVol,
            mesh.TriangleCount,
            mesh.VertexCount,
            openEdges,
            nonManifold,
            degenerates,
            checks);
        return mesh;
    }

    /// <summary>
    /// Triangulate outer, then subtract each axis-aligned opening by partitioning each triangle
    /// into the four exterior half-plane regions (no weakly-simple bridges).
    /// </summary>
    private static (List<(float Y, float Up)> Verts, List<(int A, int B, int C)> Tris) TriangulateOuterMinusRectHoles(
        List<(float Y, float Up)> outer,
        List<List<(float Y, float Up)>> holes,
        string partId)
    {
        var verts = new List<(float Y, float Up)>(outer);
        var faces = TriangulateComplete(outer, $"{partId}/outer");

        for (var hi = 0; hi < holes.Count; hi++)
        {
            var hole = holes[hi];
            if (!TryAabb(hole, out var y0, out var y1, out var u0, out var u1))
            {
                throw new InvalidOperationException(
                    $"{partId}: opening[{hi}] is not an axis-aligned rectangle after normalize. " +
                    $"Hole={FormatRing(hole)}. Non-rect openings require a CDT; refuse silent skip.");
            }

            var next = new List<(int A, int B, int C)>();
            foreach (var (a, b, c) in faces)
            {
                var poly = new List<(float Y, float Up)> { verts[a], verts[b], verts[c] };
                foreach (var piece in SubtractAabbFromConvex(poly, y0, y1, u0, u1))
                {
                    if (piece.Count < 3)
                        continue;
                    var baseIdx = verts.Count;
                    verts.AddRange(piece);
                    for (var i = 1; i < piece.Count - 1; i++)
                        next.Add((baseIdx, baseIdx + i, baseIdx + i + 1));
                }
            }

            if (next.Count == 0 && faces.Count > 0)
            {
                throw new InvalidOperationException(
                    $"{partId}: opening[{hi}] ({y0:F3}..{y1:F3}, {u0:F3}..{u1:F3}) removed all plate triangles. " +
                    "Opening may be larger than the plate.");
            }

            faces = next;
        }

        if (faces.Count == 0)
            throw new InvalidOperationException($"{partId}: triangulation produced no material triangles.");

        return WeldPlanarVerts(verts, faces, partId);
    }

    /// <summary>Merge coincident planar vertices and drop degenerate faces before extrusion.</summary>
    private static (List<(float Y, float Up)> Verts, List<(int A, int B, int C)> Tris) WeldPlanarVerts(
        List<(float Y, float Up)> verts,
        List<(int A, int B, int C)> faces,
        string partId)
    {
        const float q = 1e-4f;
        var map = new Dictionary<long, int>();
        var welded = new List<(float Y, float Up)>();
        var remap = new int[verts.Count];
        for (var i = 0; i < verts.Count; i++)
        {
            var (y, up) = verts[i];
            var key = ((long)MathF.Round(y / q) << 32) ^ ((long)MathF.Round(up / q) & 0xffffffffL);
            if (!map.TryGetValue(key, out var idx))
            {
                idx = welded.Count;
                map[key] = idx;
                welded.Add((y, up));
            }

            remap[i] = idx;
        }

        var tris = new List<(int A, int B, int C)>(faces.Count);
        foreach (var (a, b, c) in faces)
        {
            var ia = remap[a];
            var ib = remap[b];
            var ic = remap[c];
            if (ia == ib || ib == ic || ic == ia)
                continue;
            var area = MathF.Abs(SignedArea([welded[ia], welded[ib], welded[ic]]));
            if (area < 1e-10f)
                continue;
            tris.Add((ia, ib, ic));
        }

        if (tris.Count == 0)
            throw new InvalidOperationException($"{partId}: all faces degenerate after planar weld.");

        return (welded, tris);
    }

    /// <summary>
    /// Partition convex poly ∩ complement(AABB) into ≤4 convex pieces (left/right/bottom/top bands).
    /// </summary>
    private static List<List<(float Y, float Up)>> SubtractAabbFromConvex(
        List<(float Y, float Up)> poly,
        float y0, float y1, float u0, float u1)
    {
        var parts = new List<List<(float Y, float Up)>>(4);

        void Add(List<(float Y, float Up)>? piece)
        {
            if (piece is null || piece.Count < 3)
                return;
            if (MathF.Abs(SignedArea(piece)) < 1e-10f)
                return;
            if (SignedArea(piece) < 0f)
                piece.Reverse();
            parts.Add(piece);
        }

        // Closed complement of open hole interior (y0,y1)×(u0,u1), partitioned without overlap:
        //   Y ≤ y0  |  Y ≥ y1  |  (y0,y1)×(−∞,u0]  |  (y0,y1)×[u1,+∞)
        Add(ClipHalfPlaneY(poly, maxY: y0));
        Add(ClipHalfPlaneY(poly, minY: y1));
        Add(ClipBandBottom(poly, y0, y1, u0));
        Add(ClipBandTop(poly, y0, y1, u1));
        return parts;
    }

    private static List<(float Y, float Up)>? ClipHalfPlaneY(
        List<(float Y, float Up)> poly, float? minY = null, float? maxY = null)
    {
        var cur = poly;
        if (maxY is float yMax)
            cur = ClipConvex(cur, p => p.Y <= yMax + 1e-7f, (a, b) => LerpAtY(a, b, yMax));
        if (cur is null)
            return null;
        if (minY is float yMin)
            cur = ClipConvex(cur, p => p.Y >= yMin - 1e-7f, (a, b) => LerpAtY(a, b, yMin));
        return cur;
    }

    private static List<(float Y, float Up)>? ClipBandBottom(
        List<(float Y, float Up)> poly, float y0, float y1, float u0)
    {
        // Open in Y so vertical hole edges belong only to left/right partitions.
        var cur = ClipConvex(poly, p => p.Y >= y0 + 1e-6f, (a, b) => LerpAtY(a, b, y0 + 1e-6f));
        if (cur is null)
            return null;
        cur = ClipConvex(cur, p => p.Y <= y1 - 1e-6f, (a, b) => LerpAtY(a, b, y1 - 1e-6f));
        if (cur is null)
            return null;
        return ClipConvex(cur, p => p.Up <= u0 + 1e-7f, (a, b) => LerpAtUp(a, b, u0));
    }

    private static List<(float Y, float Up)>? ClipBandTop(
        List<(float Y, float Up)> poly, float y0, float y1, float u1)
    {
        var cur = ClipConvex(poly, p => p.Y >= y0 + 1e-6f, (a, b) => LerpAtY(a, b, y0 + 1e-6f));
        if (cur is null)
            return null;
        cur = ClipConvex(cur, p => p.Y <= y1 - 1e-6f, (a, b) => LerpAtY(a, b, y1 - 1e-6f));
        if (cur is null)
            return null;
        return ClipConvex(cur, p => p.Up >= u1 - 1e-7f, (a, b) => LerpAtUp(a, b, u1));
    }

    private static List<(float Y, float Up)>? ClipConvex(
        List<(float Y, float Up)> poly,
        Func<(float Y, float Up), bool> inside,
        Func<(float Y, float Up), (float Y, float Up), (float Y, float Up)> intersect)
    {
        if (poly.Count == 0)
            return null;
        var output = new List<(float Y, float Up)>(poly.Count + 2);
        var prev = poly[^1];
        var prevIn = inside(prev);
        foreach (var cur in poly)
        {
            var curIn = inside(cur);
            if (curIn)
            {
                if (!prevIn)
                    output.Add(intersect(prev, cur));
                output.Add(cur);
            }
            else if (prevIn)
            {
                output.Add(intersect(prev, cur));
            }

            prev = cur;
            prevIn = curIn;
        }

        return output.Count >= 3 ? output : null;
    }

    private static (float Y, float Up) LerpAtY((float Y, float Up) a, (float Y, float Up) b, float y)
    {
        var dy = b.Y - a.Y;
        var t = MathF.Abs(dy) < 1e-12f ? 0f : (y - a.Y) / dy;
        t = Math.Clamp(t, 0f, 1f);
        return (y, a.Up + (b.Up - a.Up) * t);
    }

    private static (float Y, float Up) LerpAtUp((float Y, float Up) a, (float Y, float Up) b, float up)
    {
        var du = b.Up - a.Up;
        var t = MathF.Abs(du) < 1e-12f ? 0f : (up - a.Up) / du;
        t = Math.Clamp(t, 0f, 1f);
        return (a.Y + (b.Y - a.Y) * t, up);
    }

    private static EditableMesh BuildWatertightMesh(
        List<(float Y, float Up)> flat,
        List<(int A, int B, int C)> tris,
        List<(float Y, float Up)> outerPts,
        List<List<(float Y, float Up)>> holes,
        float z0,
        float z1)
    {
        _ = outerPts;
        _ = holes;
        var mesh = new EditableMesh();

        var flatFront = new int[flat.Count];
        var flatRear = new int[flat.Count];
        for (var i = 0; i < flat.Count; i++)
        {
            var (y, up) = flat[i];
            flatFront[i] = mesh.AddVertex(new Vector3(y, up, z0));
            flatRear[i] = mesh.AddVertex(new Vector3(y, up, z1));
        }

        // tris are CCW in Y-Up (material). Front at z0 needs outward −Z ⇒ CW when viewed from +Z.
        foreach (var (a, b, c) in tris)
        {
            mesh.AddTriangle(flatFront[a], flatFront[c], flatFront[b]);
            mesh.AddTriangle(flatRear[a], flatRear[b], flatRear[c]);
        }

        // Boundary walls from CCW material edges (material on the left of a→b).
        var frontEdgeCount = new Dictionary<(int, int), int>();
        foreach (var (a, b, c) in tris)
        {
            CountEdge(frontEdgeCount, a, b);
            CountEdge(frontEdgeCount, b, c);
            CountEdge(frontEdgeCount, c, a);
        }

        foreach (var (a, b, c) in tris)
        {
            EmitWallIfBoundary(mesh, flatFront, flatRear, frontEdgeCount, a, b);
            EmitWallIfBoundary(mesh, flatFront, flatRear, frontEdgeCount, b, c);
            EmitWallIfBoundary(mesh, flatFront, flatRear, frontEdgeCount, c, a);
        }

        return mesh;
    }

    private static void CountEdge(Dictionary<(int, int), int> counts, int a, int b)
    {
        var key = a < b ? (a, b) : (b, a);
        counts[key] = counts.TryGetValue(key, out var n) ? n + 1 : 1;
    }

    private static void EmitWallIfBoundary(
        EditableMesh mesh,
        int[] flatFront,
        int[] flatRear,
        Dictionary<(int, int), int> frontEdgeCount,
        int a,
        int b)
    {
        var key = a < b ? (a, b) : (b, a);
        if (!frontEdgeCount.TryGetValue(key, out var n) || n != 1)
            return;

        // CCW a→b: outward from material is to the right. Quad fa→fb→rb→ra.
        var fa = flatFront[a];
        var fb = flatFront[b];
        var ra = flatRear[a];
        var rb = flatRear[b];
        mesh.AddTriangle(fa, fb, rb);
        mesh.AddTriangle(fa, rb, ra);
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

    /// <summary>Merge overlapping axis-aligned hole rectangles; non-rects kept. Never drops a hole silently.</summary>
    private static List<List<(float Y, float Up)>> MergeRectHoles(
        List<List<(float Y, float Up)>> holes,
        string partId)
    {
        var boxes = new List<(float Y0, float Y1, float U0, float U1, int Src)>();
        var nonRect = new List<List<(float Y, float Up)>>();
        for (var i = 0; i < holes.Count; i++)
        {
            var h = holes[i];
            if (!TryAabb(h, out var y0, out var y1, out var u0, out var u1))
            {
                nonRect.Add(h);
                continue;
            }

            boxes.Add((y0, y1, u0, u1, i));
        }

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
                    MathF.Min(a.U0, b.U0), MathF.Max(a.U1, b.U1),
                    a.Src);
                boxes.RemoveAt(j);
                merged = true;
                break;
            }
        }

        var result = nonRect;
        foreach (var (y0, y1, u0, u1, src) in boxes)
        {
            if (y1 - y0 < 0.08f || u1 - u0 < 0.08f)
            {
                throw new InvalidOperationException(
                    $"{partId}: opening from source[{src}] collapsed below fabrication minimum after merge " +
                    $"({y1 - y0:F3}×{u1 - u0:F3}).");
            }

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
        List<(float Y, float Up)> outer,
        string partId,
        int holeIndex)
    {
        var cy = hole.Average(p => p.Y);
        var cu = hole.Average(p => p.Up);
        if (!PointInPolygon((cy, cu), outer))
        {
            throw new InvalidOperationException(
                $"{partId}: opening[{holeIndex}] centroid ({cy:F3},{cu:F3}) lies outside the outer plate. " +
                $"Hole={FormatRing(hole)}.");
        }

        if (!TryAabb(hole, out var y0, out var y1, out var u0, out var u1))
            return hole;

        const float inset = 0.04f;
        y0 += inset;
        y1 -= inset;
        u0 += inset;
        u1 -= inset;
        if (y1 <= y0 + 0.1f || u1 <= u0 + 0.1f)
        {
            throw new InvalidOperationException(
                $"{partId}: opening[{holeIndex}] shrunk below minimum after inset. " +
                $"Hole={FormatRing(hole)}.");
        }

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
        List<List<(float Y, float Up)>> holes,
        string partId)
    {
        var poly = new List<(float Y, float Up)>(outer);
        var applied = new List<List<(float Y, float Up)>>();
        var ordered = holes
            .Select((h, i) => (Hole: h, Index: i, Area: MathF.Abs(SignedArea(h))))
            .OrderByDescending(x => x.Area)
            .ToList();

        foreach (var item in ordered)
        {
            if (!TryBridge(poly, item.Hole, out var bridged))
            {
                throw new InvalidOperationException(
                    $"{partId}: failed to bridge opening[{item.Index}] into the plate polygon. " +
                    $"Hole={FormatRing(item.Hole)}. No non-crossing bridge edge found.");
            }

            // Require a complete triangulation of the intermediate polygon — reject partial ears.
            _ = TriangulateComplete(bridged, $"{partId}/bridge-opening[{item.Index}]");
            poly = bridged;
            applied.Add(item.Hole);
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

    private static List<(int A, int B, int C)> TriangulateComplete(
        List<(float Y, float Up)> poly,
        string context)
    {
        if (poly.Count < 3)
            throw new InvalidOperationException($"{context}: cannot triangulate polygon with {poly.Count} verts.");

        List<(int A, int B, int C)> tris;
        if (IsConvex(poly))
        {
            tris = new List<(int, int, int)>(poly.Count - 2);
            for (var i = 1; i < poly.Count - 1; i++)
                tris.Add((0, i, i + 1));
        }
        else
        {
            tris = EarClipComplete(poly, context);
        }

        var expected = poly.Count - 2;
        if (tris.Count != expected)
        {
            throw new InvalidOperationException(
                $"{context}: incomplete triangulation — got {tris.Count} triangles, expected {expected} " +
                $"(polygon verts={poly.Count}). Partial ear-clip output is rejected.");
        }

        return tris;
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

    private static List<(int A, int B, int C)> EarClipComplete(
        List<(float Y, float Up)> poly,
        string context)
    {
        var n = poly.Count;
        var tris = new List<(int, int, int)>(n - 2);
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
            {
                throw new InvalidOperationException(
                    $"{context}: ear-clip stalled with {idx.Count} vertices remaining " +
                    $"(produced {tris.Count} triangles). Polygon may be self-intersecting or numerically degenerate. " +
                    $"Remaining={FormatIndexed(poly, idx)}.");
            }
        }

        if (idx.Count != 3)
        {
            throw new InvalidOperationException(
                $"{context}: ear-clip terminated with {idx.Count} vertices (need exactly 3). " +
                $"Partial triangulation ({tris.Count} tris) rejected.");
        }

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

    private static void ValidateMaterialCoverage(
        List<(int A, int B, int C)> tris,
        List<(float Y, float Up)> flat,
        List<(float Y, float Up)> outer,
        List<List<(float Y, float Up)>> holes,
        string partId,
        out float materialArea)
    {
        var outerArea = MathF.Abs(SignedArea(outer));
        var holeArea = holes.Sum(h => MathF.Abs(SignedArea(h)));
        materialArea = outerArea - holeArea;
        if (materialArea <= 1e-4f)
            throw new InvalidOperationException($"{partId}: non-positive material area ({materialArea:F6}).");

        double triArea = 0;
        for (var t = 0; t < tris.Count; t++)
        {
            var (ia, ib, ic) = tris[t];
            var a = flat[ia];
            var b = flat[ib];
            var c = flat[ic];
            var area = MathF.Abs(SignedArea([a, b, c]));
            if (area < 1e-10f)
                throw new InvalidOperationException($"{partId}: degenerate face triangle[{t}].");

            var cy = (a.Y + b.Y + c.Y) / 3f;
            var cu = (a.Up + b.Up + c.Up) / 3f;
            if (!PointInPolygon((cy, cu), outer))
            {
                throw new InvalidOperationException(
                    $"{partId}: triangle[{t}] centroid ({cy:F3},{cu:F3}) outside outer plate.");
            }

            for (var hi = 0; hi < holes.Count; hi++)
            {
                if (PointInPolygon((cy, cu), holes[hi]))
                {
                    throw new InvalidOperationException(
                        $"{partId}: triangle[{t}] occupies opening[{hi}] at ({cy:F3},{cu:F3}). " +
                        "Passage material is forbidden.");
                }
            }

            triArea += area;
        }

        var rel = MathF.Abs((float)triArea - materialArea) / MathF.Max(materialArea, 1e-6f);
        if (rel > 0.06f)
        {
            throw new InvalidOperationException(
                $"{partId}: triangle area {(float)triArea:F4} m² vs material {materialArea:F4} m² " +
                $"(rel err {rel:P1}). Incomplete or overlapping coverage.");
        }
    }

    private static void ValidateClosedSolid(
        EditableMesh mesh,
        float materialArea,
        float thickness,
        string partId,
        out float meshVolume,
        out int openEdges,
        out int nonManifold,
        out int degenerates,
        out IReadOnlyList<string> checks)
    {
        var edgeCounts = new Dictionary<(int, int), int>();
        degenerates = 0;
        for (var i = 0; i < mesh.Indices.Count; i += 3)
        {
            var ia = mesh.Indices[i];
            var ib = mesh.Indices[i + 1];
            var ic = mesh.Indices[i + 2];
            var a = mesh.Vertices[ia];
            var b = mesh.Vertices[ib];
            var c = mesh.Vertices[ic];
            var area = Vector3.Cross(b - a, c - a).Length() * 0.5f;
            if (area < 1e-10f)
                degenerates++;

            AddEdge(edgeCounts, ia, ib);
            AddEdge(edgeCounts, ib, ic);
            AddEdge(edgeCounts, ic, ia);
        }

        openEdges = edgeCounts.Count(kv => kv.Value == 1);
        nonManifold = edgeCounts.Count(kv => kv.Value > 2);
        meshVolume = MathF.Abs(SignedMeshVolume(mesh));
        var expected = materialArea * thickness;
        var volRel = MathF.Abs(meshVolume - expected) / MathF.Max(expected, 1e-6f);

        var fail = new List<string>();
        if (openEdges != 0)
            fail.Add($"openBoundaryEdges={openEdges} (expected 0 for closed solid)");
        if (nonManifold != 0)
            fail.Add($"nonManifoldEdges={nonManifold}");
        if (degenerates != 0)
            fail.Add($"degenerateTriangles={degenerates}");
        if (volRel > 0.10f)
            fail.Add($"volume {meshVolume:F5} vs expected {expected:F5} (rel {volRel:P1})");

        if (fail.Count > 0)
        {
            throw new InvalidOperationException(
                $"{partId}: watertight solid validation failed — {string.Join("; ", fail)}.");
        }

        checks =
        [
            "manifold: every edge incident to exactly 2 faces",
            "no degenerate triangles",
            $"volume {meshVolume:F5} m³ ≈ materialArea×thickness {expected:F5} m³",
            $"openings preserved in plate faces",
        ];
    }

    private static void StripDegenerateTriangles(EditableMesh mesh)
    {
        var kept = new List<int>(mesh.Indices.Count);
        for (var i = 0; i < mesh.Indices.Count; i += 3)
        {
            var ia = mesh.Indices[i];
            var ib = mesh.Indices[i + 1];
            var ic = mesh.Indices[i + 2];
            if (ia == ib || ib == ic || ic == ia)
                continue;
            var a = mesh.Vertices[ia];
            var b = mesh.Vertices[ib];
            var c = mesh.Vertices[ic];
            if (Vector3.Cross(b - a, c - a).LengthSquared() < 1e-16f)
                continue;
            kept.Add(ia);
            kept.Add(ib);
            kept.Add(ic);
        }

        if (kept.Count == mesh.Indices.Count)
            return;

        mesh.ReplaceContents(mesh.Vertices.ToList(), kept);
    }

    private static void AddEdge(Dictionary<(int, int), int> counts, int a, int b)
    {
        var key = a < b ? (a, b) : (b, a);
        counts[key] = counts.TryGetValue(key, out var n) ? n + 1 : 1;
    }

    private static float SignedMeshVolume(EditableMesh mesh)
    {
        double acc = 0;
        for (var i = 0; i < mesh.Indices.Count; i += 3)
        {
            var a = mesh.Vertices[mesh.Indices[i]];
            var b = mesh.Vertices[mesh.Indices[i + 1]];
            var c = mesh.Vertices[mesh.Indices[i + 2]];
            acc += Vector3.Dot(a, Vector3.Cross(b, c));
        }

        return (float)(acc / 6.0);
    }

    private static string FormatRing(IReadOnlyList<(float Y, float Up)>? ring)
    {
        if (ring is null || ring.Count == 0)
            return "[]";
        return "[" + string.Join(", ", ring.Select(p => $"({p.Y:F3},{p.Up:F3})")) + "]";
    }

    private static string FormatIndexed(List<(float Y, float Up)> poly, List<int> idx) =>
        "[" + string.Join(", ", idx.Select(i => $"#{i}({poly[i].Y:F3},{poly[i].Up:F3})")) + "]";
}
