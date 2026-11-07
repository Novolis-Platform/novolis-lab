namespace CalypsoCad.Structure;

/// <summary>
/// Shared deck footprint authority: hull half-beam at elevation, void splits at exact
/// compartment bounds — not bay-midpoint heuristics or min-beam rectangles.
/// </summary>
internal static class HullDeckGeometry
{
    public const float DeckEdgeInset = CalypsoShipbuildingFrames.DefaultSkinInset + 0.12f;

    /// <summary>Inner half-beam at deck plate top — local station only (hull-conforming).</summary>
    public static float HalfBeamAtDeck(float zStem, float plateTop, float inset = DeckEdgeInset)
    {
        // Bow loft can lift the IML floor above a lock deck elevation — no deck there.
        var floor = CalypsoLockGenerator.MinUpInsidePublic(zStem, 0f, 0f);
        if (plateTop < floor + 0.02f)
            return 0f;
        if (!CalypsoLockGenerator.InsideHullPublic(0f, plateTop, zStem, 0.02f))
            return 0f;
        return CalypsoLockGenerator.HalfBeamInsidePublic(zStem, plateTop, inset);
    }

    /// <summary>
    /// Merge frame stations with exact open-cell bounds so panels never straddle ENG/HOLD cuts.
    /// </summary>
    public static List<float> BuildDeckCutStations(
        IReadOnlyList<float> frameStations,
        IReadOnlyList<(float Z0, float Z1, float FloorTop)>? openAtriumSpans,
        float zStart,
        float zEnd)
    {
        var set = new SortedSet<float>();
        foreach (var z in frameStations)
        {
            if (z >= zStart - 0.01f && z <= zEnd + 0.01f)
                set.Add(z);
        }

        set.Add(zStart);
        set.Add(zEnd);
        if (openAtriumSpans is not null)
        {
            foreach (var (z0, z1, _) in openAtriumSpans)
            {
                if (z0 >= zStart && z0 <= zEnd)
                    set.Add(z0);
                if (z1 >= zStart && z1 <= zEnd)
                    set.Add(z1);
            }
        }

        return set.ToList();
    }

    public enum DeckSpanMode
    {
        FullSandwich,
        CorridorOnly,
        Skip,
    }

    /// <summary>
    /// Classify a longitudinal span using interval overlap with open cells (not midpoint).
    /// </summary>
    public static DeckSpanMode ClassifySpan(
        float z0,
        float z1,
        float plateTop,
        IReadOnlyList<(float Z0, float Z1, float FloorTop)>? openAtriumSpans)
    {
        if (openAtriumSpans is null || openAtriumSpans.Count == 0)
            return DeckSpanMode.FullSandwich;

        foreach (var (a0, a1, floorTop) in openAtriumSpans)
        {
            if (!Overlaps(z0, z1, a0, a1))
                continue;
            if (plateTop <= floorTop + 0.15f)
                continue; // cargo / atrium floor plate stays

            // Long aft span = HOLD cargo — no intermediate decks.
            if (a1 - a0 >= 12f)
                return DeckSpanMode.Skip;

            // Short midbody = ENG atrium — corridor strips only.
            return DeckSpanMode.CorridorOnly;
        }

        return DeckSpanMode.FullSandwich;
    }

    public static bool Overlaps(float z0, float z1, float a0, float a1) =>
        z0 < a1 - 0.02f && z1 > a0 + 0.02f;

    /// <summary>
    /// One manufacturable panel footprint per cut span (frame/ENG/HOLD bounds).
    /// AABB half-beam is the tightest station in the span (stays inside IML).
    /// Only splits when athwart change is large — abutting panels, never overlapping.
    /// </summary>
    public static List<(float Z0, float Z1, float Zc, float HalfY)> HullConformingSegments(
        float z0,
        float z1,
        float plateTop,
        float web,
        float inset = DeckEdgeInset)
    {
        _ = web;
        var span = z1 - z0;
        if (span < 0.35f)
            return [];

        var hy0 = HalfBeamAtDeck(z0, plateTop, inset);
        var hy1 = HalfBeamAtDeck(z1, plateTop, inset);
        var hyc = HalfBeamAtDeck((z0 + z1) * 0.5f, plateTop, inset);
        if (hy0 < 0.35f && hy1 < 0.35f && hyc < 0.35f)
            return [];

        // Split only for strong taper (≥1.2 m half-beam change over ≥3.2 m) — abut, no overlap.
        var taper = MathF.Abs(hy0 - hy1);
        var count = taper >= 1.2f && span >= 3.2f ? 2 : 1;
        var list = new List<(float, float, float, float)>(count);
        for (var i = 0; i < count; i++)
        {
            var za = z0 + span * (i / (float)count);
            var zb = z0 + span * ((i + 1) / (float)count);
            var zc = (za + zb) * 0.5f;
            var halfY = MathF.Min(
                HalfBeamAtDeck(za, plateTop, inset),
                MathF.Min(HalfBeamAtDeck(zc, plateTop, inset), HalfBeamAtDeck(zb, plateTop, inset)));
            if (halfY < 0.35f)
                continue;
            list.Add((za, zb, zc, halfY * 0.98f));
        }

        return list;
    }
}
