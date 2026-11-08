namespace CalypsoCad.Layout;

/// <summary>
/// Per-frame attachment sites derived from <see cref="CompartmentLayout"/> intersections.
/// </summary>
internal sealed class PartitionAttachmentPlan
{
    public float ZStem { get; init; }
    public IReadOnlyList<AttachmentSite> Sites { get; init; } = [];
    public IReadOnlyList<PassageEnvelope> PassagesAtStation { get; init; } = [];
    public IReadOnlyList<string> Diagnostics { get; init; } = [];

    public readonly record struct AttachmentSite(
        AttachmentKind Kind,
        float Y,
        float Up0,
        float Up1,
        float HalfCrossY,
        float HalfUp,
        string Purpose,
        string OwnerCompartmentId);

    public enum AttachmentKind
    {
        PartitionPost,
        PortalJamb,
        PortalHeader,
        AttachmentRail,
    }

    /// <summary>Build attachment sites for one ordinary ring-frame stem.</summary>
    public static PartitionAttachmentPlan Build(CompartmentLayout layout, float zStem, float web)
    {
        var diagnostics = new List<string>();
        var sites = new List<AttachmentSite>();
        var passages = layout.PassagesAtStem(zStem).ToList();

        // Skip cargo / full-height machinery atrium — no cabin posts through clear volumes.
        var atCargo = layout.Compartments.Any(c =>
            c.IsCargoClearance && layout.ContainsStem(c, zStem));
        var atEngAtrium = layout.Compartments.Any(c =>
            c.IsMachineryAtrium && layout.ContainsStem(c, zStem));

        if (atCargo)
        {
            diagnostics.Add($"z={zStem:0.##}: HOLD clearance — no partition posts");
            return new PartitionAttachmentPlan
            {
                ZStem = zStem,
                Sites = [],
                PassagesAtStation = passages,
                Diagnostics = diagnostics,
            };
        }

        var postHalf = MathF.Max(0.04f, web * 0.28f);
        var jambHalf = MathF.Max(0.05f, web * 0.32f);

        // Longitudinal partition posts (merged Y) outside passage envelopes.
        var wallYs = new List<(float Y, float Up0, float Up1, string Owner)>();
        foreach (var path in layout.LongitudinalAtStem(zStem))
        {
            if (path.IsPassageBoundary)
                continue; // portal jambs handle corridor edges
            if (layout.YInsideAnyPassage(path.Y, zStem, margin: 0.12f))
            {
                diagnostics.Add($"skip post y={path.Y:0.##} inside passage");
                continue;
            }

            wallYs.Add((path.Y, path.Up0, path.Up1, path.OwnerCompartmentId));
        }

        wallYs.Sort((a, b) => a.Y.CompareTo(b.Y));
        var merged = MergeYs(wallYs, 0.18f);

        if (!atEngAtrium)
        {
            foreach (var (y, up0, up1, owner) in merged)
            {
                // Stay above plate / below next deck headroom.
                var floor = up0 + layout.TPlate * 0.5f;
                var head = up1 - 0.08f;
                if (head - floor < 1.2f)
                    continue;
                sites.Add(new AttachmentSite(
                    AttachmentKind.PartitionPost,
                    y,
                    floor,
                    head,
                    postHalf,
                    (head - floor) * 0.5f,
                    $"Partition attachment post @ y={y:0.##}",
                    owner));
            }
        }
        else
        {
            diagnostics.Add($"z={zStem:0.##}: ENG atrium — portal jambs only");
        }

        // Portal jambs + headers at each passage envelope edge.
        foreach (var pass in passages)
        {
            var floor = pass.FloorUp + layout.TPlate * 0.5f;
            var head = floor + pass.ClearHeight;
            var jambTop = head + 0.12f;
            foreach (var yEdge in new[] { pass.Y0, pass.Y1 })
            {
                sites.Add(new AttachmentSite(
                    AttachmentKind.PortalJamb,
                    yEdge,
                    floor,
                    jambTop,
                    jambHalf,
                    (jambTop - floor) * 0.5f,
                    $"Portal jamb {pass.Id} @ y={yEdge:0.##}",
                    pass.Id));
            }

            var headerCy = (pass.Y0 + pass.Y1) * 0.5f;
            var headerHy = (pass.Y1 - pass.Y0) * 0.5f + jambHalf;
            var headerUp = head + 0.06f;
            sites.Add(new AttachmentSite(
                AttachmentKind.PortalHeader,
                headerCy,
                headerUp - 0.05f,
                headerUp + 0.05f,
                headerHy,
                0.05f,
                $"Portal header {pass.Id}",
                pass.Id));
        }

        return new PartitionAttachmentPlan
        {
            ZStem = zStem,
            Sites = sites,
            PassagesAtStation = passages,
            Diagnostics = diagnostics,
        };
    }

    private static List<(float Y, float Up0, float Up1, string Owner)> MergeYs(
        List<(float Y, float Up0, float Up1, string Owner)> walls,
        float tol)
    {
        var merged = new List<(float Y, float Up0, float Up1, string Owner)>();
        foreach (var w in walls)
        {
            if (merged.Count == 0 || MathF.Abs(w.Y - merged[^1].Y) > tol)
            {
                merged.Add(w);
                continue;
            }

            var prev = merged[^1];
            merged[^1] = (
                (prev.Y + w.Y) * 0.5f,
                MathF.Min(prev.Up0, w.Up0),
                MathF.Max(prev.Up1, w.Up1),
                prev.Owner);
        }

        return merged;
    }
}
