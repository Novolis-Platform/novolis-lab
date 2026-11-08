namespace CalypsoCad.Structure;

/// <summary>
/// Exact inward offset of a simple convex polygon by a uniform distance (mitered vertices).
/// Used to derive structural rings from the authoritative hull section — not half-beam shrink.
/// </summary>
internal static class PolygonInset2D
{
    /// <summary>
    /// Inset a CCW polygon by <paramref name="distance"/> along edge inward normals.
    /// Returns an empty array if the inset collapses the polygon.
    /// </summary>
    public static (float Y, float Up)[] Inset(IReadOnlyList<(float Y, float Up)> ring, float distance)
    {
        ArgumentNullException.ThrowIfNull(ring);
        if (ring.Count < 3)
            return [];
        if (distance <= 1e-8f)
            return ring.ToArray();

        var n = ring.Count;
        // Ensure CCW for consistent inward normals (left of directed edge).
        var pts = ring.ToArray();
        if (SignedArea(pts) < 0f)
            Array.Reverse(pts);

        var offsetA = new (float Y, float Up)[n];
        var offsetB = new (float Y, float Up)[n];
        for (var i = 0; i < n; i++)
        {
            var a = pts[i];
            var b = pts[(i + 1) % n];
            var ey = b.Y - a.Y;
            var eu = b.Up - a.Up;
            var len = MathF.Sqrt(ey * ey + eu * eu);
            if (len < 1e-8f)
                return [];
            // Inward normal for CCW: rotate edge 90° CCW → (−eu, ey) points left = interior.
            var ny = -eu / len;
            var nu = ey / len;
            offsetA[i] = (a.Y + ny * distance, a.Up + nu * distance);
            offsetB[i] = (b.Y + ny * distance, b.Up + nu * distance);
        }

        var result = new (float Y, float Up)[n];
        for (var i = 0; i < n; i++)
        {
            var prev = (i + n - 1) % n;
            // Intersection of offset edge[prev] (offsetA[prev]→offsetB[prev])
            // with offset edge[i] (offsetA[i]→offsetB[i]).
            if (!TryIntersect(
                    offsetA[prev], offsetB[prev],
                    offsetA[i], offsetB[i],
                    out var hit))
            {
                // Parallel edges — fall back to offset of the shared vertex along average normal.
                var a = pts[i];
                var p = pts[prev];
                var q = pts[(i + 1) % n];
                var n0y = -(a.Up - p.Up);
                var n0u = a.Y - p.Y;
                var n1y = -(q.Up - a.Up);
                var n1u = q.Y - a.Y;
                var l0 = MathF.Sqrt(n0y * n0y + n0u * n0u);
                var l1 = MathF.Sqrt(n1y * n1y + n1u * n1u);
                if (l0 < 1e-8f || l1 < 1e-8f)
                    return [];
                n0y /= l0; n0u /= l0;
                n1y /= l1; n1u /= l1;
                var my = n0y + n1y;
                var mu = n0u + n1u;
                var ml = MathF.Sqrt(my * my + mu * mu);
                if (ml < 1e-8f)
                    return [];
                hit = (a.Y + my / ml * distance, a.Up + mu / ml * distance);
            }

            result[i] = hit;
        }

        if (MathF.Abs(SignedArea(result)) < 1e-4f)
            return [];
        // Keep original winding relative to input (caller may expect CCW).
        if (SignedArea(ring) < 0f)
            Array.Reverse(result);
        return result;
    }

    private static bool TryIntersect(
        (float Y, float Up) a, (float Y, float Up) b,
        (float Y, float Up) c, (float Y, float Up) d,
        out (float Y, float Up) hit)
    {
        hit = default;
        var dx1 = b.Y - a.Y;
        var dy1 = b.Up - a.Up;
        var dx2 = d.Y - c.Y;
        var dy2 = d.Up - c.Up;
        var denom = dx1 * dy2 - dy1 * dx2;
        if (MathF.Abs(denom) < 1e-12f)
            return false;
        var t = ((c.Y - a.Y) * dy2 - (c.Up - a.Up) * dx2) / denom;
        hit = (a.Y + t * dx1, a.Up + t * dy1);
        return float.IsFinite(hit.Y) && float.IsFinite(hit.Up);
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
}
