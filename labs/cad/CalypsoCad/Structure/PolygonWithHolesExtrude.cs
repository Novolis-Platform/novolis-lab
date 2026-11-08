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

            holePts.Add(ClipHoleToOuter(normalized, outerPts, id, hi));
        }

        holePts = MergeRectHoles(holePts, id);
        var expectedOpenings = holePts.Count;

        var (flat, tris) = TriangulateOuterMinusRectHoles(outerPts, holePts, id);
        ValidateMaterialCoverage(tris, flat, outerPts, holePts, id, out var materialArea);

        var thickness = halfThicknessZ * 2f;
        var z0 = zCad - halfThicknessZ;
        var z1 = zCad + halfThicknessZ;
        var mesh = BuildWatertightMesh(flat, tris, z0, z1);

        mesh = MeshWeld.Apply(mesh, new WeldOptions(1e-4f, WeldPositionMode.Average, WeldScope.EntireMesh));
        StripDegenerateTriangles(mesh);
        if (mesh.FindBoundaryEdges().Count > 0)
            mesh = MeshWeld.Apply(mesh, new WeldOptions(5e-4f, WeldPositionMode.Average, WeldScope.EntireMesh));
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
    /// Triangulate outer, split faces across each opening edge, delete faces inside openings.
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

            var next = new List<(int A, int B, int C)>(faces.Count * 2);
            foreach (var (a, b, c) in faces)
            {
                var poly = new List<(float Y, float Up)> { verts[a], verts[b], verts[c] };
                if (!TriangleOverlapsAabb(poly, y0, y1, u0, u1))
                {
                    next.Add((a, b, c));
                    continue;
                }

                foreach (var piece in SubtractAabbFromConvex(poly, y0, y1, u0, u1))
                {
                    if (piece.Count < 3)
                        continue;
                    var baseIdx = verts.Count;
                    verts.AddRange(piece);
                    // Force CCW fan.
                    if (SignedArea(piece) < 0f)
                    {
                        for (var i = 1; i < piece.Count - 1; i++)
                            next.Add((baseIdx, baseIdx + i + 1, baseIdx + i));
                    }
                    else
                    {
                        for (var i = 1; i < piece.Count - 1; i++)
                            next.Add((baseIdx, baseIdx + i, baseIdx + i + 1));
                    }
                }
            }

            if (next.Count == 0)
            {
                throw new InvalidOperationException(
                    $"{partId}: opening[{hi}] ({y0:F3}..{y1:F3}, {u0:F3}..{u1:F3}) removed all plate triangles.");
            }

            faces = next;
        }

        if (faces.Count == 0)
            throw new InvalidOperationException($"{partId}: triangulation produced no material triangles.");

        return WeldPlanarVerts(verts, faces, partId);
    }

    private static bool RectTouchesOuter(
        List<(float Y, float Up)> outer, float y0, float y1, float u0, float u1)
    {
        // Corner samples + edge midpoints: if any lies outside / on the outer ring, it's a notch.
        ReadOnlySpan<(float Y, float Up)> samples =
        [
            (y0, u0), (y0, u1), (y1, u1), (y1, u0),
            ((y0 + y1) * 0.5f, u0), ((y0 + y1) * 0.5f, u1),
            (y0, (u0 + u1) * 0.5f), (y1, (u0 + u1) * 0.5f),
        ];
        foreach (var p in samples)
        {
            if (!PointInPolygon(p, outer))
                return true;
            if (DistanceToBoundary(p, outer) < 0.05f)
                return true;
        }

        return false;
    }

    private static float DistanceToBoundary((float Y, float Up) p, List<(float Y, float Up)> ring)
    {
        var best = float.PositiveInfinity;
        for (var i = 0; i < ring.Count; i++)
        {
            var a = ring[i];
            var b = ring[(i + 1) % ring.Count];
            best = MathF.Min(best, DistPointToSegment(p, a, b));
        }

        return best;
    }

    private static float DistPointToSegment(
        (float Y, float Up) p, (float Y, float Up) a, (float Y, float Up) b)
    {
        var abY = b.Y - a.Y;
        var abU = b.Up - a.Up;
        var lenSq = abY * abY + abU * abU;
        if (lenSq < 1e-12f)
            return MathF.Sqrt((p.Y - a.Y) * (p.Y - a.Y) + (p.Up - a.Up) * (p.Up - a.Up));
        var t = Math.Clamp(((p.Y - a.Y) * abY + (p.Up - a.Up) * abU) / lenSq, 0f, 1f);
        var qY = a.Y + abY * t;
        var qU = a.Up + abU * t;
        return MathF.Sqrt((p.Y - qY) * (p.Y - qY) + (p.Up - qU) * (p.Up - qU));
    }

    /// <summary>
    /// Cut a rectangular notch from the outer plate ring. Returns a single simple polygon.
    /// </summary>
    private static List<(float Y, float Up)> NotchOuterWithRect(
        List<(float Y, float Up)> outer,
        float y0, float y1, float u0, float u1,
        string partId,
        int holeIndex)
    {
        var pieces = SubtractAabbFromConvex(outer, y0, y1, u0, u1);
        if (pieces.Count == 0)
        {
            throw new InvalidOperationException(
                $"{partId}: notch opening[{holeIndex}] removed the entire plate.");
        }

        // Merge pieces along shared edges into one ring via largest-component preference when the
        // cut stays connected; if the rect severs the plate, keep the largest solid remnant and fail
        // only when area loss exceeds the opening (should not happen for a notch).
        var ordered = pieces.OrderByDescending(p => MathF.Abs(SignedArea(p))).ToList();
        var merged = ordered[0];
        if (ordered.Count > 1)
        {
            // For a true perimeter notch the four partition bands form one simply-connected region
            // after dissolving shared edges — reconstruct by stitching boundary of the piece set.
            var stitched = StitchPiecesToRing(pieces);
            if (stitched is { Count: >= 3 })
                merged = stitched;
        }

        if (merged.Count < 3)
        {
            throw new InvalidOperationException(
                $"{partId}: notch opening[{holeIndex}] produced a degenerate outer ring.");
        }

        return NormalizeRing(merged, wantCcw: true);
    }

    /// <summary>Dissolve shared edges among partition pieces and walk the remaining boundary.</summary>
    private static List<(float Y, float Up)>? StitchPiecesToRing(List<List<(float Y, float Up)>> pieces)
    {
        const float q = 1e-4f;
        long Key((float Y, float Up) p) =>
            ((long)MathF.Round(p.Y / q) << 32) ^ ((long)MathF.Round(p.Up / q) & 0xffffffffL);

        var verts = new Dictionary<long, (float Y, float Up)>();
        var edgeCount = new Dictionary<(long, long), int>();
        foreach (var piece in pieces)
        {
            for (var i = 0; i < piece.Count; i++)
            {
                var a = piece[i];
                var b = piece[(i + 1) % piece.Count];
                var ka = Key(a);
                var kb = Key(b);
                verts[ka] = a;
                verts[kb] = b;
                var e = ka < kb ? (ka, kb) : (kb, ka);
                edgeCount[e] = edgeCount.TryGetValue(e, out var n) ? n + 1 : 1;
            }
        }

        var boundary = edgeCount.Where(kv => kv.Value == 1).Select(kv => kv.Key).ToList();
        if (boundary.Count < 3)
            return null;

        var adj = new Dictionary<long, List<long>>();
        foreach (var (a, b) in boundary)
        {
            if (!adj.TryGetValue(a, out var la))
                adj[a] = la = [];
            if (!adj.TryGetValue(b, out var lb))
                adj[b] = lb = [];
            la.Add(b);
            lb.Add(a);
        }

        // Prefer start at extreme left for stability.
        var start = adj.Keys.OrderBy(k => verts[k].Y).ThenBy(k => verts[k].Up).First();
        if (adj[start].Count != 2)
        {
            start = adj.FirstOrDefault(kv => kv.Value.Count == 2).Key;
            if (!adj.ContainsKey(start))
                return null;
        }

        var ring = new List<(float Y, float Up)>();
        var prev = start;
        var cur = adj[start][0];
        ring.Add(verts[start]);
        var guard = 0;
        while (cur != start && guard++ < boundary.Count + 2)
        {
            ring.Add(verts[cur]);
            var next = adj[cur].FirstOrDefault(n => n != prev);
            if (next == 0 && !adj[cur].Contains(0) && next != start)
            {
                // next may legitimately be key 0; use explicit search.
                next = adj[cur].Find(n => n != prev);
            }

            if (adj[cur].All(n => n == prev) && adj[cur].Count == 1)
                break;
            var candidates = adj[cur].Where(n => n != prev).ToList();
            if (candidates.Count == 0)
                break;
            prev = cur;
            cur = candidates[0];
        }

        return ring.Count >= 3 ? ring : null;
    }

    private static bool TriangleOverlapsAabb(
        List<(float Y, float Up)> tri, float y0, float y1, float u0, float u1)
    {
        var minY = MathF.Min(tri[0].Y, MathF.Min(tri[1].Y, tri[2].Y));
        var maxY = MathF.Max(tri[0].Y, MathF.Max(tri[1].Y, tri[2].Y));
        var minU = MathF.Min(tri[0].Up, MathF.Min(tri[1].Up, tri[2].Up));
        var maxU = MathF.Max(tri[0].Up, MathF.Max(tri[1].Up, tri[2].Up));
        return minY <= y1 && maxY >= y0 && minU <= u1 && maxU >= u0;
    }

    /// <summary>
    /// Convex poly ∩ complement(open AABB), as four non-overlapping pieces.
    /// Left/right own the vertical opening edges; top/bottom own the horizontal edges between.
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

        Add(ClipConvex(poly, p => p.Y <= y0 + 1e-7f, (a, b) => LerpAtY(a, b, y0)));
        Add(ClipConvex(poly, p => p.Y >= y1 - 1e-7f, (a, b) => LerpAtY(a, b, y1)));
        Add(ClipBand(poly, y0, y1, maxUp: u0));
        Add(ClipBand(poly, y0, y1, minUp: u1));
        return parts;
    }

    private static List<(float Y, float Up)>? ClipBand(
        List<(float Y, float Up)> poly, float y0, float y1, float? minUp = null, float? maxUp = null)
    {
        // Inclusive Y: shared boundary with left/right is a zero-area edge (manifold after weld).
        var cur = ClipConvex(poly, p => p.Y >= y0 - 1e-7f, (a, b) => LerpAtY(a, b, y0));
        if (cur is null)
            return null;
        cur = ClipConvex(cur, p => p.Y <= y1 + 1e-7f, (a, b) => LerpAtY(a, b, y1));
        if (cur is null)
            return null;
        if (maxUp is float uMax)
            cur = ClipConvex(cur, p => p.Up <= uMax + 1e-7f, (a, b) => LerpAtUp(a, b, uMax));
        if (cur is null)
            return null;
        if (minUp is float uMin)
            cur = ClipConvex(cur, p => p.Up >= uMin - 1e-7f, (a, b) => LerpAtUp(a, b, uMin));
        return cur;
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

        var seen = new HashSet<(int, int, int)>();
        var tris = new List<(int A, int B, int C)>(faces.Count);
        foreach (var (a, b, c) in faces)
        {
            var ia = remap[a];
            var ib = remap[b];
            var ic = remap[c];
            if (ia == ib || ib == ic || ic == ia)
                continue;
            if (MathF.Abs(SignedArea([welded[ia], welded[ib], welded[ic]])) < 1e-10f)
                continue;
            // Force CCW.
            if (SignedArea([welded[ia], welded[ib], welded[ic]]) < 0f)
                (ib, ic) = (ic, ib);
            // Canonical key so duplicate faces from partition seams are dropped.
            var key = CanonicalTri(ia, ib, ic);
            if (!seen.Add(key))
                continue;
            tris.Add((ia, ib, ic));
        }

        if (tris.Count == 0)
            throw new InvalidOperationException($"{partId}: all faces degenerate after planar weld.");

        return (welded, tris);
    }

    private static (int, int, int) CanonicalTri(int a, int b, int c)
    {
        // Rotate so smallest index is first; keep winding.
        if (a <= b && a <= c)
            return (a, b, c);
        if (b <= a && b <= c)
            return (b, c, a);
        return (c, a, b);
    }

    private static void EnsureManifoldAnnulus(
        List<(float Y, float Up)> flat,
        List<(int A, int B, int C)> tris,
        string partId,
        int expectedOpenings)
    {
        var edgeCount = new Dictionary<(int, int), int>();
        foreach (var (a, b, c) in tris)
        {
            CountEdge(edgeCount, a, b);
            CountEdge(edgeCount, b, c);
            CountEdge(edgeCount, c, a);
        }

        var boundary = edgeCount.Where(kv => kv.Value == 1).Select(kv => kv.Key).ToList();
        var degree = new Dictionary<int, int>();
        foreach (var (a, b) in boundary)
        {
            degree[a] = degree.TryGetValue(a, out var da) ? da + 1 : 1;
            degree[b] = degree.TryGetValue(b, out var db) ? db + 1 : 1;
        }

        var bad = degree.Where(kv => kv.Value != 2).ToList();
        if (bad.Count == 0)
            return;

        // Drop zero-area / needle triangles that create spur boundary edges, then re-check.
        var cleaned = tris.Where(t =>
        {
            var area = MathF.Abs(SignedArea([flat[t.A], flat[t.B], flat[t.C]]));
            return area > 1e-8f;
        }).ToList();
        tris.Clear();
        tris.AddRange(cleaned);

        edgeCount.Clear();
        foreach (var (a, b, c) in tris)
        {
            CountEdge(edgeCount, a, b);
            CountEdge(edgeCount, b, c);
            CountEdge(edgeCount, c, a);
        }

        boundary = edgeCount.Where(kv => kv.Value == 1).Select(kv => kv.Key).ToList();
        degree.Clear();
        foreach (var (a, b) in boundary)
        {
            degree[a] = degree.TryGetValue(a, out var da) ? da + 1 : 1;
            degree[b] = degree.TryGetValue(b, out var db) ? db + 1 : 1;
        }

        bad = degree.Where(kv => kv.Value != 2).ToList();
        if (bad.Count == 0)
            return;

        foreach (var (vi, deg) in bad.Take(8))
        {
            var p = flat[vi];
            Console.Error.WriteLine($"  dbg {partId} boundary deg={deg} at ({p.Y:F3},{p.Up:F3}) idx={vi}");
        }

        // Dissolve: edges with count≥2 are internal; rebuild triangles only from a constrained
        // ear-clip of each boundary loop pair is too heavy. Instead, split high-degree vertices
        // by not sharing partition seam verts: already welded. Drop needle tris on spur edges.
        var spurEdges = boundary.Where(e =>
            degree.GetValueOrDefault(e.Item1) != 2 || degree.GetValueOrDefault(e.Item2) != 2).ToList();
        var spurSet = new HashSet<(int, int)>(spurEdges);
        var kept = tris.Where(t =>
        {
            static (int, int) U(int a, int b) => a < b ? (a, b) : (b, a);
            var usesSpur = spurSet.Contains(U(t.A, t.B)) || spurSet.Contains(U(t.B, t.C)) || spurSet.Contains(U(t.C, t.A));
            if (!usesSpur)
                return true;
            return MathF.Abs(SignedArea([flat[t.A], flat[t.B], flat[t.C]])) > 1e-4f;
        }).ToList();
        tris.Clear();
        tris.AddRange(kept);

        // Recompute; if still bad, throw with actionable detail.
        edgeCount.Clear();
        foreach (var (a, b, c) in tris)
        {
            CountEdge(edgeCount, a, b);
            CountEdge(edgeCount, b, c);
            CountEdge(edgeCount, c, a);
        }

        boundary = edgeCount.Where(kv => kv.Value == 1).Select(kv => kv.Key).ToList();
        degree.Clear();
        foreach (var (a, b) in boundary)
        {
            degree[a] = degree.TryGetValue(a, out var da) ? da + 1 : 1;
            degree[b] = degree.TryGetValue(b, out var db) ? db + 1 : 1;
        }

        bad = degree.Where(kv => kv.Value != 2).ToList();
        if (bad.Count > 0)
        {
            throw new InvalidOperationException(
                $"{partId}: planar plate annulus is not manifold after opening punch " +
                $"({bad.Count} boundary verts with degree ≠ 2, expectedOpenings={expectedOpenings}).");
        }
    }

    private static EditableMesh BuildWatertightMesh(
        List<(float Y, float Up)> flat,
        List<(int A, int B, int C)> tris,
        float z0,
        float z1)
    {
        var mesh = new EditableMesh();
        var flatFront = new int[flat.Count];
        var flatRear = new int[flat.Count];
        for (var i = 0; i < flat.Count; i++)
        {
            var (y, up) = flat[i];
            flatFront[i] = mesh.AddVertex(new Vector3(y, up, z0));
            flatRear[i] = mesh.AddVertex(new Vector3(y, up, z1));
        }

        foreach (var (a, b, c) in tris)
        {
            mesh.AddTriangle(flatFront[a], flatFront[c], flatFront[b]); // outward −Z
            mesh.AddTriangle(flatRear[a], flatRear[b], flatRear[c]);   // outward +Z
        }

        var edgeCount = new Dictionary<(int, int), int>();
        foreach (var (a, b, c) in tris)
        {
            CountEdge(edgeCount, a, b);
            CountEdge(edgeCount, b, c);
            CountEdge(edgeCount, c, a);
        }

        foreach (var (a, b, c) in tris)
        {
            EmitWallIfBoundary(mesh, flatFront, flatRear, edgeCount, a, b);
            EmitWallIfBoundary(mesh, flatFront, flatRear, edgeCount, b, c);
            EmitWallIfBoundary(mesh, flatFront, flatRear, edgeCount, c, a);
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
        Dictionary<(int, int), int> edgeCount,
        int a,
        int b)
    {
        var key = a < b ? (a, b) : (b, a);
        if (!edgeCount.TryGetValue(key, out var n) || n != 1)
            return;

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

        var isCcw = SignedArea(pts) > 0f;
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
                    $"(produced {tris.Count} triangles).");
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
                        $"{partId}: triangle[{t}] occupies opening[{hi}] at ({cy:F3},{cu:F3}).");
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
            if (Vector3.Cross(b - a, c - a).Length() < 1e-10f)
                degenerates++;

            AddUndirected(edgeCounts, ia, ib);
            AddUndirected(edgeCounts, ib, ic);
            AddUndirected(edgeCounts, ic, ia);
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
            "openings preserved in plate faces",
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

        if (kept.Count != mesh.Indices.Count)
            mesh.ReplaceContents(mesh.Vertices.ToList(), kept);
    }

    /// <summary>
    /// Partition seams can leave boundary vertices with degree &gt; 2, producing vertical wall
    /// edges with &gt;2 incident faces. Drop excess wall triangles until the mesh is manifold.
    /// </summary>
    private static void RepairNonManifoldWallEdges(EditableMesh mesh)
    {
        for (var pass = 0; pass < 64; pass++)
        {
            var edgeCounts = new Dictionary<(int, int), int>();
            var edgeTris = new Dictionary<(int, int), List<int>>();
            for (var t = 0; t < mesh.Indices.Count; t += 3)
            {
                void Edge(int a, int b)
                {
                    var key = a < b ? (a, b) : (b, a);
                    edgeCounts[key] = edgeCounts.TryGetValue(key, out var n) ? n + 1 : 1;
                    if (!edgeTris.TryGetValue(key, out var list))
                        edgeTris[key] = list = [];
                    list.Add(t);
                }

                Edge(mesh.Indices[t], mesh.Indices[t + 1]);
                Edge(mesh.Indices[t + 1], mesh.Indices[t + 2]);
                Edge(mesh.Indices[t + 2], mesh.Indices[t]);
            }

            var bad = edgeCounts.Where(kv => kv.Value > 2).Select(kv => kv.Key).ToList();
            if (bad.Count == 0)
                return;

            var drop = new HashSet<int>();
            foreach (var edge in bad)
            {
                var pa = mesh.Vertices[edge.Item1];
                var pb = mesh.Vertices[edge.Item2];
                // Prefer dropping wall triangles (edge is nearly vertical through the plate).
                var vertical = MathF.Abs(pa.X - pb.X) < 1e-4f
                               && MathF.Abs(pa.Y - pb.Y) < 1e-4f
                               && MathF.Abs(pa.Z - pb.Z) > 1e-4f;
                if (!vertical)
                    continue;

                var tris = edgeTris[edge]
                    .Distinct()
                    .Select(t => (T: t, Score: WallScore(mesh, t)))
                    .OrderByDescending(x => x.Score)
                    .ToList();
                // Keep two incident faces; drop the rest (highest wall score first).
                for (var i = 2; i < tris.Count; i++)
                    drop.Add(tris[i].T);
            }

            if (drop.Count == 0)
                return;

            var kept = new List<int>(mesh.Indices.Count);
            for (var t = 0; t < mesh.Indices.Count; t += 3)
            {
                if (drop.Contains(t))
                    continue;
                kept.Add(mesh.Indices[t]);
                kept.Add(mesh.Indices[t + 1]);
                kept.Add(mesh.Indices[t + 2]);
            }

            mesh.ReplaceContents(mesh.Vertices.ToList(), kept);
        }
    }

    private static float WallScore(EditableMesh mesh, int triOffset)
    {
        var a = mesh.Vertices[mesh.Indices[triOffset]];
        var b = mesh.Vertices[mesh.Indices[triOffset + 1]];
        var c = mesh.Vertices[mesh.Indices[triOffset + 2]];
        var n = Vector3.Cross(b - a, c - a);
        if (n.LengthSquared() < 1e-16f)
            return 0f;
        n = Vector3.Normalize(n);
        // Walls are orthogonal to the plate normal (Z); high score ⇒ likely a wall face.
        return 1f - MathF.Abs(n.Z);
    }

    private static void AddUndirected(Dictionary<(int, int), int> counts, int a, int b)
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
}
