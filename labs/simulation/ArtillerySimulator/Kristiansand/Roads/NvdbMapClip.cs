using System.Numerics;

namespace ArtillerySimulator;

/// <summary>Liang–Barsky clip of road segments against the heightfield cutout.</summary>
internal static class NvdbMapClip
{
    /// <summary>
    /// Clips segment AB against [0,east]×[0,north].
    /// Returns false when the segment lies entirely outside.
    /// </summary>
    public static bool ClipSegmentToMap(ref Vector2 a, ref Vector2 b, float east, float north)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        var t0 = 0f;
        var t1 = 1f;

        bool Clip(float p, float q)
        {
            if (MathF.Abs(p) < 1e-8f)
                return q >= 0f;
            var r = q / p;
            if (p < 0f)
            {
                if (r > t1)
                    return false;
                if (r > t0)
                    t0 = r;
            }
            else
            {
                if (r < t0)
                    return false;
                if (r < t1)
                    t1 = r;
            }

            return true;
        }

        if (!Clip(-dx, a.X) || !Clip(dx, east - a.X) || !Clip(-dy, a.Y) || !Clip(dy, north - a.Y))
            return false;
        if (t1 < t0)
            return false;

        var na = new Vector2(a.X + t0 * dx, a.Y + t0 * dy);
        var nb = new Vector2(a.X + t1 * dx, a.Y + t1 * dy);
        a = na;
        b = nb;
        return Vector2.DistanceSquared(a, b) >= 0.25f;
    }
}
