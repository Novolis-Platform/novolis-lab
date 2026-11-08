using System.Numerics;
using Novolis.Cad.Primitives;

namespace CalypsoCad.Drawing;

/// <summary>Converts CadEntity physical representations into <see cref="DrawableGeometry"/>.</summary>
internal static class CadGeometryNormalizer
{
    public static DrawableGeometry Normalize(CadEntity entity, string semanticNodeId)
    {
        var name = entity.Name ?? entity.Kind ?? entity.Id.ToString("N");
        var kind = (entity.Kind ?? "").ToLowerInvariant();

        if (kind == "mesh"
            && entity.MeshVertices is { Count: >= 3 } verts
            && entity.MeshIndices is { Count: >= 3 } indices)
        {
            var v = new Vector3[verts.Count];
            for (var i = 0; i < verts.Count; i++)
                v[i] = To3(verts[i]);
            var bounds = BoundsOf(v);
            var centerline = ExtractMeshCenterline(v);
            return new DrawableGeometry
            {
                SourceEntityId = entity.Id,
                SourceName = name,
                SemanticNodeId = semanticNodeId,
                Kind = DrawablePrimitiveKind.Mesh,
                Vertices = v,
                Indices = indices.ToArray(),
                Polylines = centerline is null ? null : [centerline],
                Bounds = bounds,
            };
        }

        if (kind is "box" or "wedge" && CadShipGeometry.TryGetBox(entity, out var center, out var he))
        {
            var corners = BoxCorners(center, he);
            var indicesBox = BoxTriangleIndices();
            return new DrawableGeometry
            {
                SourceEntityId = entity.Id,
                SourceName = name,
                SemanticNodeId = semanticNodeId,
                Kind = DrawablePrimitiveKind.Box,
                Vertices = corners,
                Indices = indicesBox,
                Polylines = BoxEdgePolylines(corners),
                Bounds = BoundsOf(corners),
            };
        }

        if (kind is "wall" && entity.A is { Length: >= 3 } && entity.B is { Length: >= 3 })
        {
            var a = CadVec.To(entity.A);
            var b = CadVec.To(entity.B);
            var h = Math.Max(0.05f, entity.Height);
            var corners = new[]
            {
                a, b, b + new Vector3(0, h, 0), a + new Vector3(0, h, 0),
            };
            // Extrude as thin slab along AB × up for a 2-triangle double-sided panel.
            var wallVerts = new[]
            {
                corners[0], corners[1], corners[2], corners[3],
            };
            var idx = new[] { 0, 1, 2, 0, 2, 3 };
            return new DrawableGeometry
            {
                SourceEntityId = entity.Id,
                SourceName = name,
                SemanticNodeId = semanticNodeId,
                Kind = DrawablePrimitiveKind.Mesh,
                Vertices = wallVerts,
                Indices = idx,
                Polylines = [corners, [corners[0], corners[3], corners[2], corners[1], corners[0]]],
                Bounds = BoundsOf(wallVerts),
            };
        }

        if (kind is "space" or "opening")
        {
            var ring = ExtractRing(entity);
            if (ring.Length >= 3)
            {
                var h = Math.Max(0f, entity.Height);
                var bounds = BoundsOf(ring);
                if (h > 0)
                    bounds = bounds.Encapsulate(ring[0] + new Vector3(0, h, 0));
                return new DrawableGeometry
                {
                    SourceEntityId = entity.Id,
                    SourceName = name,
                    SemanticNodeId = semanticNodeId,
                    Kind = DrawablePrimitiveKind.PolygonRing,
                    Polylines = [CloseRing(ring)],
                    Bounds = bounds,
                };
            }
        }

        if (kind is "line" && entity.A is { Length: >= 3 } && entity.B is { Length: >= 3 })
        {
            var a = CadVec.To(entity.A);
            var b = CadVec.To(entity.B);
            return new DrawableGeometry
            {
                SourceEntityId = entity.Id,
                SourceName = name,
                SemanticNodeId = semanticNodeId,
                Kind = DrawablePrimitiveKind.Polyline,
                Polylines = [[a, b]],
                Bounds = DrawingBounds3.Empty.Encapsulate(a).Encapsulate(b),
            };
        }

        // Sweep path from properties z0/z1 + points centerline when present.
        if (entity.Points is { Count: >= 2 } pts
            && pts.All(p => p is { Length: >= 3 }))
        {
            var poly = pts.Select(To3).ToArray();
            return new DrawableGeometry
            {
                SourceEntityId = entity.Id,
                SourceName = name,
                SemanticNodeId = semanticNodeId,
                Kind = DrawablePrimitiveKind.Polyline,
                Polylines = [poly],
                Bounds = BoundsOf(poly),
            };
        }

        return new DrawableGeometry
        {
            SourceEntityId = entity.Id,
            SourceName = name,
            SemanticNodeId = semanticNodeId,
            Kind = DrawablePrimitiveKind.Unsupported,
            UnsupportedReason = $"Unsupported CadEntity.Kind='{entity.Kind}' without mesh/box/ring geometry.",
            Bounds = DrawingBounds3.Empty,
        };
    }

    /// <summary>
    /// Centerline from actual mesh ends (min/max along principal extent) — not hull loft samples.
    /// </summary>
    private static Vector3[]? ExtractMeshCenterline(Vector3[] verts)
    {
        if (verts.Length < 2)
            return null;
        var min = verts[0];
        var max = verts[0];
        foreach (var p in verts)
        {
            min = Vector3.Min(min, p);
            max = Vector3.Max(max, p);
        }

        var size = max - min;
        // Prefer Z (longitudinal) for stringers; else longest axis.
        var axis = 2;
        if (size.X >= size.Y && size.X >= size.Z) axis = 0;
        else if (size.Y >= size.X && size.Y >= size.Z) axis = 1;

        float Coord(Vector3 p) => axis switch { 0 => p.X, 1 => p.Y, _ => p.Z };
        var lo = Coord(min);
        var hi = Coord(max);
        var span = hi - lo;
        if (span < 1e-4f)
            return null;
        var band = Math.Max(0.02f, span * 0.08f);

        Vector3 AvgNear(float target)
        {
            var sum = Vector3.Zero;
            var n = 0;
            foreach (var p in verts)
            {
                if (Math.Abs(Coord(p) - target) <= band)
                {
                    sum += p;
                    n++;
                }
            }

            return n == 0 ? Vector3.Lerp(min, max, (target - lo) / span) : sum / n;
        }

        return [AvgNear(lo), AvgNear(hi)];
    }

    private static Vector3[] ExtractRing(CadEntity entity)
    {
        if (entity.Footprint is { Count: >= 3 } fp)
            return fp.Select(To3).ToArray();
        if (entity.Points is { Count: >= 3 } pts)
            return pts.Select(To3).ToArray();
        return [];
    }

    private static Vector3[] CloseRing(Vector3[] ring)
    {
        if (ring.Length == 0)
            return ring;
        if ((ring[0] - ring[^1]).LengthSquared() < 1e-8f)
            return ring;
        var closed = new Vector3[ring.Length + 1];
        Array.Copy(ring, closed, ring.Length);
        closed[^1] = ring[0];
        return closed;
    }

    private static Vector3 To3(float[] p) =>
        p.Length >= 3 ? new Vector3(p[0], p[1], p[2]) : Vector3.Zero;

    private static DrawingBounds3 BoundsOf(IReadOnlyList<Vector3> pts)
    {
        var b = DrawingBounds3.Empty;
        foreach (var p in pts)
            b = b.Encapsulate(p);
        return b;
    }

    private static Vector3[] BoxCorners(Vector3 c, Vector3 he)
    {
        return
        [
            c + new Vector3(-he.X, -he.Y, -he.Z),
            c + new Vector3(+he.X, -he.Y, -he.Z),
            c + new Vector3(+he.X, +he.Y, -he.Z),
            c + new Vector3(-he.X, +he.Y, -he.Z),
            c + new Vector3(-he.X, -he.Y, +he.Z),
            c + new Vector3(+he.X, -he.Y, +he.Z),
            c + new Vector3(+he.X, +he.Y, +he.Z),
            c + new Vector3(-he.X, +he.Y, +he.Z),
        ];
    }

    private static int[] BoxTriangleIndices() =>
    [
        0, 1, 2, 0, 2, 3, // -Z
        4, 6, 5, 4, 7, 6, // +Z
        0, 4, 5, 0, 5, 1, // -Y
        3, 2, 6, 3, 6, 7, // +Y
        0, 3, 7, 0, 7, 4, // -X
        1, 5, 6, 1, 6, 2, // +X
    ];

    private static IReadOnlyList<Vector3[]> BoxEdgePolylines(Vector3[] c) =>
    [
        [c[0], c[1], c[2], c[3], c[0]],
        [c[4], c[5], c[6], c[7], c[4]],
        [c[0], c[4]], [c[1], c[5]], [c[2], c[6]], [c[3], c[7]],
    ];
}
