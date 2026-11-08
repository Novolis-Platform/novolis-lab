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

    public static ProjectionResult ProjectOrthographic(
        IReadOnlyList<DrawableGeometry> geometry,
        StandardViewOrientation orientation)
    {
        var segments = new List<ProjectedSegment>();
        var tris = new List<ProjectedTriangle>();
        var world = DrawingBounds3.Empty;
        var minU = float.PositiveInfinity;
        var maxU = float.NegativeInfinity;
        var minV = float.PositiveInfinity;
        var maxV = float.NegativeInfinity;

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

            if (g.Vertices is { Length: > 0 } verts && g.Indices is { Length: >= 3 } idx)
            {
                for (var i = 0; i + 2 < idx.Length; i += 3)
                {
                    var i0 = idx[i];
                    var i1 = idx[i + 1];
                    var i2 = idx[i + 2];
                    if (i0 < 0 || i1 < 0 || i2 < 0
                        || i0 >= verts.Length || i1 >= verts.Length || i2 >= verts.Length)
                        continue;
                    var a = Map(verts[i0], orientation);
                    var b = Map(verts[i1], orientation);
                    var c = Map(verts[i2], orientation);
                    Track(a); Track(b); Track(c);
                    tris.Add(new ProjectedTriangle(a, b, c, g.SourceEntityId, g.SourceName));
                    // Boundary edges only when they are silhouette candidates — emit all outer edges;
                    // composer styles them. Avoid inventing every wire as a seam by de-duping later.
                    AddEdge(segments, a, b, g, false);
                    AddEdge(segments, b, c, g, false);
                    AddEdge(segments, c, a, g, false);
                }
            }

            if (g.Polylines is { } polys)
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
        {
            minU = maxU = minV = maxV = 0;
        }

        return new ProjectionResult(segments, tris, world, minU, maxU, minV, maxV);
    }

    public static ViewPoint2 Map(Vector3 world, StandardViewOrientation orientation) =>
        orientation switch
        {
            // Stern left: U = −Z so larger ZStem (aft) → smaller U… wait ISO stern left means
            // aft appears on left. World +Z is bow, so aft is −Z direction from bow.
            // Drawing U increases to the right (bow). U = world.Z (bow right) would put stem left.
            // Existing ShipX: ox + (loa - zFromStem)*scale → larger z → left. So U_model = −Z.
            StandardViewOrientation.PlanSternLeft =>
                new ViewPoint2(-world.Z, -world.X, world.Y),
            StandardViewOrientation.ProfileSternLeft =>
                new ViewPoint2(-world.Z, world.Y, world.X),
            StandardViewOrientation.TransverseLookingAft =>
                new ViewPoint2(-world.X, world.Y, -world.Z),
            StandardViewOrientation.TransverseLookingForward =>
                new ViewPoint2(world.X, world.Y, world.Z),
            _ => new ViewPoint2(-world.Z, world.Y, world.X),
        };

    private static void AddEdge(
        List<ProjectedSegment> segments,
        ViewPoint2 a,
        ViewPoint2 b,
        DrawableGeometry g,
        bool section)
    {
        // Skip near-zero edges.
        var du = a.U - b.U;
        var dv = a.V - b.V;
        if (du * du + dv * dv < 1e-12f)
            return;
        segments.Add(new ProjectedSegment(a, b, g.SourceEntityId, g.SourceName, section));
    }
}
