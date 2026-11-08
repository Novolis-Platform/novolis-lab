using Novolis.Math.Geometry;

namespace CalypsoCad.Structure;

/// <summary>Midpoints, axis alignment, and uniform frame schedules for ship CAD.</summary>
internal static class CalypsoDrafting
{
    /// <summary>Default transverse frame pitch when lock omits <c>frames.bayM</c> (primary web-frame bay).</summary>
    public const float DefaultFrameBayM = 4f;

    public static float Midpoint(float a, float b) => (a, b).GetCenter();

    public static (float X, float Y) Midpoint2D(float x0, float y0, float x1, float y1) =>
        ((x0, x1).GetCenter(), (y0, y1).GetCenter());

    /// <summary>Re-home an athwart [y0,y1] span onto the ship spine (y=0).</summary>
    public static (float Y0, float Y1) CenterAthwartOnSpine(float y0, float y1) =>
        (y0, y1).CenterAligned();

    public static float SnapToGrid(float value, float anchor, float step)
    {
        if (step < 1e-4f)
            return value;
        var n = MathF.Round((value - anchor) / step, MidpointRounding.AwayFromZero);
        return anchor + n * step;
    }

    public static List<(string Id, float ZStem)> DeduplicateStations(
        IEnumerable<(string Id, float ZStem)> stations,
        float minSep = 0.35f)
    {
        var ordered = stations.OrderBy(s => s.ZStem).ToList();
        var merged = new List<(string Id, float ZStem)>();
        foreach (var s in ordered)
        {
            if (merged.Count == 0 || s.ZStem - merged[^1].ZStem > minSep)
            {
                merged.Add(s);
                continue;
            }

            merged[^1] = PreferStationId(merged[^1], s);
        }

        return merged;
    }

    /// <summary>
    /// Uniform pitch grid without bulkhead awareness. Prefer
    /// <see cref="FrameSchedule.Create"/> for structure emit (BH-first, fill spans).
    /// </summary>
    public static List<(string Id, float ZStem)> BuildUniformFrameSchedule(
        float loa,
        float frameBay = DefaultFrameBayM,
        float zStart = 0f,
        float sternClear = 0.05f)
    {
        if (frameBay < 0.5f)
            frameBay = DefaultFrameBayM;
        var zEnd = MathF.Max(zStart + frameBay, loa - sternClear);
        var list = new List<(string Id, float ZStem)>();
        for (var z = zStart; z <= zEnd + 1e-3f; z += frameBay)
        {
            var zClamped = MathF.Min(z, loa - sternClear);
            list.Add(($"F{zClamped:0}", zClamped));
            if (zClamped >= loa - sternClear - 1e-3f)
                break;
        }

        // Guarantee a stern frame if the last bay left a gap larger than half pitch.
        if (list.Count == 0 || loa - sternClear - list[^1].ZStem > frameBay * 0.55f)
            list.Add(($"F{loa - sternClear:0}", loa - sternClear));

        return DeduplicateStations(list, minSep: frameBay * 0.45f);
    }

    /// <summary>Obsolete mixed schedule — redirects to uniform bay (loft/compartment midpoints ignored).</summary>
    public static List<(string Id, float ZStem)> BuildBowFrameSchedule(
        IEnumerable<(string Id, float Z0, float Z1)> bowCompartments,
        IEnumerable<(string Id, float ZStem)> loftStations,
        float loa,
        float frameBay = DefaultFrameBayM)
    {
        _ = bowCompartments;
        _ = loftStations;
        return BuildUniformFrameSchedule(loa, frameBay);
    }

    /// <summary>Obsolete mixed schedule — redirects to uniform bay.</summary>
    public static List<(string Id, float ZStem)> BuildFrameSchedule(
        IEnumerable<(string Id, float ZStem)> loftStations,
        float loa,
        float frameBay = DefaultFrameBayM)
    {
        _ = loftStations;
        return BuildUniformFrameSchedule(loa, frameBay);
    }

    private static (string Id, float ZStem) PreferStationId(
        (string Id, float ZStem) a,
        (string Id, float ZStem) b)
    {
        if (IsMidbodyGridFrameId(a.Id) && !IsMidbodyGridFrameId(b.Id))
            return a;
        if (IsMidbodyGridFrameId(b.Id) && !IsMidbodyGridFrameId(a.Id))
            return b;
        return a;
    }

    private static bool IsMidbodyGridFrameId(string id) =>
        id.Length >= 2
        && id[0] is 'F' or 'f'
        && float.TryParse(
            id[1..],
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out _);

    /// <summary>Center athwart bounds on spine (y=0) for fore compartments; preserve stem extent.</summary>
    public static (float Y0, float Y1, float Z0, float Z1) CenterOnSpine(float y0, float y1, float z0, float z1, float zStemMax = 18f)
    {
        if (MathF.Max(z0, z1) > zStemMax)
            return (y0, y1, z0, z1);
        var (cy0, cy1) = CenterAthwartOnSpine(y0, y1);
        var zA = MathF.Min(z0, z1);
        var zB = MathF.Max(z0, z1);
        return (cy0, cy1, zA, zB);
    }

    /// <summary>Stem z of the shared athwart axis for a set of bow objects (median of centers).</summary>
    public static float MedianStemCenter(IReadOnlyList<float> zCenters)
    {
        if (zCenters.Count == 0)
            return 0f;
        var sorted = zCenters.OrderBy(z => z).ToList();
        var mid = sorted.Count / 2;
        return sorted.Count % 2 == 1
            ? sorted[mid]
            : Midpoint(sorted[mid - 1], sorted[mid]);
    }
}
