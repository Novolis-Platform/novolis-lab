namespace CalypsoCad.Generation;

/// <summary>Midpoints, axis alignment, and uniform frame schedules for ship CAD.</summary>
internal static class CalypsoDrafting
{
    public static float Midpoint(float a, float b) => (a + b) * 0.5f;

    public static (float X, float Y) Midpoint2D(float x0, float y0, float x1, float y1) =>
        ((x0 + x1) * 0.5f, (y0 + y1) * 0.5f);

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

    /// <summary>Bow compartment spine midpoints + loft fore + uniform grid + loft aft.</summary>
    public static List<(string Id, float ZStem)> BuildBowFrameSchedule(
        IEnumerable<(string Id, float Z0, float Z1)> bowCompartments,
        IEnumerable<(string Id, float ZStem)> loftStations,
        float loa,
        float frameBay = 4f)
    {
        var list = new List<(string Id, float ZStem)>();
        foreach (var (id, z0, z1) in bowCompartments)
        {
            if (string.IsNullOrEmpty(id))
                continue;
            if (z1 > 18.5f)
                continue;
            var zMid = (z0 + z1) * 0.5f;
            list.Add(($"frame-{id}", zMid));
        }

        foreach (var (id, z) in loftStations.OrderBy(s => s.ZStem))
        {
            if (z <= 17.5f)
                list.Add((id, z));
        }

        const float gridAnchor = 17f;
        for (var z = gridAnchor; z <= loa - frameBay * 0.5f - 0.2f; z += frameBay)
            list.Add(($"F{z:0}", z));

        foreach (var (id, z) in loftStations.OrderBy(s => s.ZStem))
        {
            if (z > 17.5f)
                list.Add((id, z));
        }

        return DeduplicateStations(list);
    }

    /// <summary>Loft bow stations + uniform 4 m grid (anchor 17 m) + loft aft — no double bays.</summary>
    public static List<(string Id, float ZStem)> BuildFrameSchedule(
        IEnumerable<(string Id, float ZStem)> loftStations,
        float loa,
        float frameBay = 4f)
    {
        var list = new List<(string Id, float ZStem)>();
        foreach (var (id, z) in loftStations.OrderBy(s => s.ZStem))
        {
            if (z <= 17.5f)
                list.Add((id, z));
        }

        const float gridAnchor = 17f;
        for (var z = gridAnchor; z <= loa - frameBay * 0.5f - 0.2f; z += frameBay)
            list.Add(($"F{z:0}", z));

        foreach (var (id, z) in loftStations.OrderBy(s => s.ZStem))
        {
            if (z > 17.5f)
                list.Add((id, z));
        }

        return DeduplicateStations(list);
    }

    /// <summary>When two stations share the same stem z, keep the 4 m grid id (F17, F21, …) for acceptance and naming.</summary>
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
        var hy = (y1 - y0) * 0.5f;
        var zA = MathF.Min(z0, z1);
        var zB = MathF.Max(z0, z1);
        return (-hy, hy, zA, zB);
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
