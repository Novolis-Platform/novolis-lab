namespace CalypsoCad.Structure;

/// <summary>
/// Station-driven transverse framing: heavy bulkheads are fixed anchors first;
/// ordinary ring frames fill the spans between them so no bay exceeds target pitch.
/// </summary>
internal static class FrameSchedule
{
    /// <summary>
    /// Build one coherent list of structural stations (ring frames + heavy bulkheads).
    /// </summary>
    /// <param name="hullLength">LOA (m), stem at 0.</param>
    /// <param name="targetPitch">Maximum ordinary-frame bay (m); default 4 m.</param>
    /// <param name="bulkheads">Heavy bulkhead stations (id, z from stem).</param>
    public static IReadOnlyList<ScheduledStation> Create(
        float hullLength,
        float targetPitch,
        IReadOnlyList<(string Id, float ZStem)> bulkheads,
        float zStart = 0f,
        float sternClear = 0.05f)
    {
        if (targetPitch < 0.5f)
            targetPitch = CalypsoDrafting.DefaultFrameBayM;

        var zEnd = MathF.Max(zStart + targetPitch * 0.5f, hullLength - sternClear);
        var bh = (bulkheads ?? [])
            .Where(b => b.ZStem > zStart + 0.05f && b.ZStem < zEnd - 0.05f)
            .OrderBy(b => b.ZStem)
            .GroupBy(b => MathF.Round(b.ZStem, 2))
            .Select(g => g.First())
            .ToList();

        // Fixed anchors: stem ring frame, each heavy BH, stern ring frame.
        var anchors = new List<(float Z, StructuralStationRole Role, string? BhId)>
        {
            (zStart, StructuralStationRole.OrdinaryRingFrame, null),
        };
        foreach (var (id, z) in bh)
            anchors.Add((z, StructuralStationRole.HeavyBulkhead, id));
        if (zEnd - anchors[^1].Z > 0.05f)
            anchors.Add((zEnd, StructuralStationRole.OrdinaryRingFrame, null));

        var stations = new List<ScheduledStation>();
        for (var i = 0; i < anchors.Count; i++)
        {
            var (z, role, bhId) = anchors[i];
            stations.Add(MakeStation(z, role, bhId));

            if (i + 1 >= anchors.Count)
                continue;

            var zNext = anchors[i + 1].Z;
            foreach (var mid in FillSpan(z, zNext, targetPitch))
                stations.Add(MakeStation(mid, StructuralStationRole.OrdinaryRingFrame, null));
        }

        return stations
            .GroupBy(s => MathF.Round(s.ZStem, 3))
            .Select(g => Prefer(g))
            .OrderBy(s => s.ZStem)
            .ToList();
    }

    public static IReadOnlyList<ScheduledStation> OrdinaryRingFrames(IReadOnlyList<ScheduledStation> schedule) =>
        schedule.Where(s => s.Role == StructuralStationRole.OrdinaryRingFrame).ToList();

    public static IReadOnlyList<ScheduledStation> HeavyBulkheads(IReadOnlyList<ScheduledStation> schedule) =>
        schedule.Where(s => s.Role == StructuralStationRole.HeavyBulkhead).ToList();

    /// <summary>
    /// Intermediate ring-frame Z values in (z0, z1) so every bay ≤ targetPitch.
    /// Evenly distributes when the span is not an integer multiple of pitch.
    /// </summary>
    public static IReadOnlyList<float> FillSpan(float z0, float z1, float targetPitch)
    {
        var gap = z1 - z0;
        if (gap <= targetPitch + 0.05f)
            return [];

        var bayCount = (int)MathF.Ceiling(gap / targetPitch - 1e-4f);
        if (bayCount < 2)
            return [];

        var pitch = gap / bayCount;
        var mids = new List<float>(bayCount - 1);
        for (var i = 1; i < bayCount; i++)
            mids.Add(z0 + pitch * i);
        return mids;
    }

    private static ScheduledStation MakeStation(float z, StructuralStationRole role, string? bhId)
    {
        if (role == StructuralStationRole.HeavyBulkhead && !string.IsNullOrWhiteSpace(bhId))
            return new ScheduledStation($"BH-{bhId}", z, role, bhId);

        // Id encodes rounded stem metres for stable frame-F* entity names.
        var rounded = MathF.Round(z);
        if (MathF.Abs(z - rounded) < 0.05f)
            return new ScheduledStation($"F{rounded:0}", z, role);

        return new ScheduledStation(
            $"F{z.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)}",
            z,
            role);
    }

    private static ScheduledStation Prefer(IEnumerable<ScheduledStation> group)
    {
        // Heavy bulkhead wins when co-located with a ring frame (replacement).
        return group
            .OrderByDescending(s => s.Role == StructuralStationRole.HeavyBulkhead)
            .ThenBy(s => s.Id, StringComparer.OrdinalIgnoreCase)
            .First();
    }
}
