using System.Numerics;

namespace CalypsoCad.Drawing;

/// <summary>Single generic orthographic projection into a 2D view plane.</summary>
internal static class ViewProjector
{
    public readonly record struct ViewPoint2(float U, float V, float Depth);

    public readonly record struct ProjectedSegment(
        ViewPoint2 A,
        ViewPoint2 B,
        Guid SourceEntityId,
        string SourceName,
        bool IsSectionCut);

    public readonly record struct ProjectedTriangle(
        ViewPoint2 A,
        ViewPoint2 B,
        ViewPoint2 C,
        Guid SourceEntityId,
        string SourceName);

    public readonly record struct ProjectionResult(
        IReadOnlyList<ProjectedSegment> Segments,
        IReadOnlyList<ProjectedTriangle> FilledTriangles,
        DrawingBounds3 WorldBounds,
        float MinU,
        float MaxU,
        float MinV,
        float MaxV);

    public enum EdgeMode
    {
        /// <summary>Camera-facing silhouette edges only (no triangulation seams).</summary>
        Silhouette,
        /// <summary>Explicit polylines only (paths / box wires) — for location context overlays.</summary>
        PolylinesOnly,
        /// <summary>Silhouette + polylines.</summary>
        SilhouetteAndPolylines,
    }

    public static ProjectionResult ProjectOrthographic(
        IReadOnlyList<DrawableGeometry> geometry,
        StandardViewOrientation orientation,
        EdgeMode edges = EdgeMode.SilhouetteAndPolylines,
        bool includeFilledTriangles = true)
    {
        var segments = new List<ProjectedSegment>();
        var tris = new List<ProjectedTriangle>();
        var world = DrawingBounds3.Empty;
        var minU = float.PositiveInfinity;
        var maxU = float.NegativeInfinity;
        var minV = float.PositiveInfinity;
        var maxV = float.NegativeInfinity;
        var viewDir = ViewDirection(orientation);

        void Track(ViewPoint2 p)
        {
            minU = Math.Min(minU, p.U);
            maxU = Math.Max(maxU, p.U);
            minV = Math.Min(minV, p.V);
            maxV = Math.Max(maxV, p.V);
        }

        foreach (var g in geometry)
        {
            world = world.Union(g.Bounds);

            // Boxes already carry explicit edge polylines — do not also emit triangulated seams.
            var skipMeshEdges = g.Kind == DrawablePrimitiveKind.Box && g.Polylines is { Count: > 0 };

            if (!skipMeshEdges
                && edges is EdgeMode.Silhouette or EdgeMode.SilhouetteAndPolylines
                && g.Vertices is { Length: > 0 } verts
                && g.Indices is { Length: >= 3 } idx)
            {
                // Per undirected edge → list of triangle facing signs (+1 front, -1 back).
                var edgeFacing = new Dictionary<(int, int), List<int>>();
                for (var i = 0; i + 2 < idx.Length; i += 3)
                {
                    var i0 = idx[i];
                    var i1 = idx[i + 1];
                    var i2 = idx[i + 2];
                    if (i0 < 0 || i1 < 0 || i2 < 0
                        || i0 >= verts.Length || i1 >= verts.Length || i2 >= verts.Length)
                        continue;

                    var w0 = verts[i0];
                    var w1 = verts[i1];
                    var w2 = verts[i2];
                    var n = Vector3.Cross(w1 - w0, w2 - w0);
                    if (n.LengthSquared() < 1e-14f)
                        continue;
                    n = Vector3.Normalize(n);
                    var facing = Vector3.Dot(n, viewDir) >= 0f ? 1 : -1;

                    var a = Map(w0, orientation);
                    var b = Map(w1, orientation);
                    var c = Map(w2, orientation);
                    Track(a); Track(b); Track(c);
                    if (includeFilledTriangles && facing > 0)
                        tris.Add(new ProjectedTriangle(a, b, c, g.SourceEntityId, g.SourceName));

                    AccumulateEdge(edgeFacing, i0, i1, facing);
                    AccumulateEdge(edgeFacing, i1, i2, facing);
                    AccumulateEdge(edgeFacing, i2, i0, facing);
                }

                foreach (var ((ia, ib), facings) in edgeFacing)
                {
                    // Silhouette: boundary (one triangle) or front/back junction.
                    var front = facings.Count(f => f > 0);
                    var back = facings.Count(f => f < 0);
                    if (!(facings.Count == 1 || (front > 0 && back > 0)))
                        continue;
                    var a = Map(verts[ia], orientation);
                    var b = Map(verts[ib], orientation);
                    AddEdge(segments, a, b, g, false);
                }
            }
            else if (g.Vertices is { Length: > 0 } vTrack)
            {
                // Still track bounds even when skipping mesh edges.
                foreach (var p in vTrack)
                    Track(Map(p, orientation));
            }

            if (edges is EdgeMode.PolylinesOnly or EdgeMode.SilhouetteAndPolylines
                && g.Polylines is { } polys)
            {
                foreach (var poly in polys)
                {
                    for (var i = 0; i + 1 < poly.Length; i++)
                    {
                        var a = Map(poly[i], orientation);
                        var b = Map(poly[i + 1], orientation);
                        Track(a); Track(b);
                        segments.Add(new ProjectedSegment(a, b, g.SourceEntityId, g.SourceName, false));
                    }
                }
            }
        }

        if (float.IsInfinity(minU))
            minU = maxU = minV = maxV = 0;

        return new ProjectionResult(segments, tris, world, minU, maxU, minV, maxV);
    }

    public static Vector3 ViewDirection(StandardViewOrientation orientation) =>
        orientation switch
        {
            StandardViewOrientation.PlanSternLeft => -Vector3.UnitY,
            StandardViewOrientation.ProfileSternLeft => -Vector3.UnitX, // looking toward port from starboard
            StandardViewOrientation.TransverseLookingAft => -Vector3.UnitZ,
            StandardViewOrientation.TransverseLookingForward => Vector3.UnitZ,
            _ => -Vector3.UnitX,
        };

    /// <summary>
    /// Orthographic view coords. For plan/profile, +U is toward the bow so FitAndDraw
    /// (page X ∝ U) matches ISO 128-15 stern-left / bow-right used by <c>ShipX(zFromStem)</c>.
    /// CAD +Z points bow-ward from midships — do not treat world.Z as zFromStem.
    /// </summary>
    public static ViewPoint2 Map(Vector3 world, StandardViewOrientation orientation) =>
        orientation switch
        {
            // U = world.Z (bow +) → right; V = −X (starboard down when bow is right).
            StandardViewOrientation.PlanSternLeft =>
                new ViewPoint2(world.Z, -world.X, world.Y),
            StandardViewOrientation.ProfileSternLeft =>
                new ViewPoint2(world.Z, world.Y, world.X),
            // Looking aft: observer toward bow looking −Z; +X stbd → left on page (existing sheets).
            StandardViewOrientation.TransverseLookingAft =>
                new ViewPoint2(-world.X, world.Y, -world.Z),
            StandardViewOrientation.TransverseLookingForward =>
                new ViewPoint2(world.X, world.Y, world.Z),
            _ => new ViewPoint2(world.Z, world.Y, world.X),
        };

    private static void AccumulateEdge(
        Dictionary<(int, int), List<int>> map, int a, int b, int facing)
    {
        var key = a < b ? (a, b) : (b, a);
        if (!map.TryGetValue(key, out var list))
        {
            list = [];
            map[key] = list;
        }

        list.Add(facing);
    }

    private static void AddEdge(
        List<ProjectedSegment> segments,
        ViewPoint2 a,
        ViewPoint2 b,
        DrawableGeometry g,
        bool section)
    {
        var du = a.U - b.U;
        var dv = a.V - b.V;
        if (du * du + dv * dv < 1e-12f)
            return;
        segments.Add(new ProjectedSegment(a, b, g.SourceEntityId, g.SourceName, section));
    }
}
