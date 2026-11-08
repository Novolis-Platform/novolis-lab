using CalypsoCad.Model;

namespace CalypsoCad.Visibility;

/// <summary>Named hierarchy visibility configurations — applied only through <see cref="HierarchyVisibilityService"/>.</summary>
internal static class VisibilityPresets
{
    public static IReadOnlyList<(string Id, string Label, Func<VisibilitySnapshot> Build)> All { get; } =
    [
        ("complete", "Complete arrangement", Complete),
        ("primary", "Primary structure", PrimaryStructure),
        ("primary-decks", "Primary structure and decks", PrimaryPlusDecks),
        ("pressure", "Pressure structure", PressureStructure),
        ("cargo", "Cargo hold", CargoHold),
        ("frames", "Ring frames only", RingFramesOnly),
        ("bulkheads", "Heavy bulkheads only", BulkheadsOnly),
        ("knuckles", "Knuckle stringers only", KnucklesOnly),
        ("longitudinal", "Primary longitudinal", PrimaryLongitudinal),
    ];

    public static VisibilitySnapshot Complete() =>
        Snapshot("complete", []);

    public static VisibilitySnapshot PrimaryStructure() =>
        Snapshot("primary",
        [
            (ShipHierarchyIds.ShipRoot, LocalVisibility.Visible),
            // Reference geometry stays available (OML edge guide); hull shell mesh hidden.
            (ShipHierarchyIds.PressureHullShell, LocalVisibility.Hidden),
            (ShipHierarchyIds.Closures, LocalVisibility.Hidden),
            (ShipHierarchyIds.Interior, LocalVisibility.Hidden),
            (ShipHierarchyIds.CargoSystems, LocalVisibility.Hidden),
            (ShipHierarchyIds.LoadedCargo, LocalVisibility.Hidden),
            (ShipHierarchyIds.Machinery, LocalVisibility.Hidden),
            (ShipHierarchyIds.Diagnostics, LocalVisibility.Hidden),
            (ShipHierarchyIds.DeckStructure, LocalVisibility.Hidden),
        ]);

    public static VisibilitySnapshot PrimaryPlusDecks()
    {
        var overrides = new Dictionary<string, LocalVisibility>(PrimaryStructure().Overrides, StringComparer.OrdinalIgnoreCase)
        {
            [ShipHierarchyIds.DeckStructure] = LocalVisibility.Visible,
        };
        return new VisibilitySnapshot { Label = "primary-decks", Overrides = overrides };
    }

    /// <summary>Structure + interiors (partitions / spaces), no solid OML.</summary>
    public static VisibilitySnapshot InteriorStructure() =>
        Snapshot("interior-structure",
        [
            (ShipHierarchyIds.ShipRoot, LocalVisibility.Visible),
            (ShipHierarchyIds.PressureHullShell, LocalVisibility.Hidden),
            (ShipHierarchyIds.LoadedCargo, LocalVisibility.Hidden),
            (ShipHierarchyIds.Machinery, LocalVisibility.Hidden),
            (ShipHierarchyIds.Diagnostics, LocalVisibility.Hidden),
            (ShipHierarchyIds.Interior, LocalVisibility.Visible),
            (ShipHierarchyIds.Closures, LocalVisibility.Visible),
        ]);

    /// <summary>Full arrangement minus solid OML shell (edge guide may still show).</summary>
    public static VisibilitySnapshot ArrangementNoSolidOml() =>
        Snapshot("arrangement-no-oml",
        [
            (ShipHierarchyIds.ShipRoot, LocalVisibility.Visible),
            (ShipHierarchyIds.PressureHullShell, LocalVisibility.Hidden),
        ]);

    public static VisibilitySnapshot PressureStructure() =>
        Snapshot("pressure",
        [
            (ShipHierarchyIds.ShipRoot, LocalVisibility.Visible),
            (ShipHierarchyIds.PhysicalStructure, LocalVisibility.Hidden),
            (ShipHierarchyIds.Hull, LocalVisibility.Visible),
            (ShipHierarchyIds.PressureHullShell, LocalVisibility.Visible),
            (ShipHierarchyIds.KnuckleStringers, LocalVisibility.Hidden),
            (ShipHierarchyIds.ShellStiffeners, LocalVisibility.Hidden),
            (ShipHierarchyIds.Transverse, LocalVisibility.Visible),
            (ShipHierarchyIds.RingFrames, LocalVisibility.Hidden),
            (ShipHierarchyIds.HeavyBulkheads, LocalVisibility.Visible),
            (ShipHierarchyIds.Longitudinal, LocalVisibility.Hidden),
            (ShipHierarchyIds.DeckStructure, LocalVisibility.Hidden),
            (ShipHierarchyIds.ReferenceGeometry, LocalVisibility.Hidden),
            (ShipHierarchyIds.Interior, LocalVisibility.Hidden),
            (ShipHierarchyIds.Closures, LocalVisibility.Visible),
            (ShipHierarchyIds.CargoSystems, LocalVisibility.Hidden),
            (ShipHierarchyIds.LoadedCargo, LocalVisibility.Hidden),
            (ShipHierarchyIds.Machinery, LocalVisibility.Hidden),
            (ShipHierarchyIds.Diagnostics, LocalVisibility.Hidden),
        ]);

    public static VisibilitySnapshot CargoHold() =>
        Snapshot("cargo",
        [
            (ShipHierarchyIds.ShipRoot, LocalVisibility.Visible),
            (ShipHierarchyIds.Interior, LocalVisibility.Hidden),
            (ShipHierarchyIds.Closures, LocalVisibility.Hidden),
            (ShipHierarchyIds.Machinery, LocalVisibility.Hidden),
            (ShipHierarchyIds.ReferenceGeometry, LocalVisibility.Hidden),
            (ShipHierarchyIds.Diagnostics, LocalVisibility.Hidden),
            (ShipHierarchyIds.LoadedCargo, LocalVisibility.Visible),
            (ShipHierarchyIds.CargoSystems, LocalVisibility.Visible),
        ]);

    public static VisibilitySnapshot RingFramesOnly() =>
        IsolateBranch("frames", ShipHierarchyIds.RingFrames);

    public static VisibilitySnapshot BulkheadsOnly() =>
        IsolateBranch("bulkheads", ShipHierarchyIds.HeavyBulkheads);

    public static VisibilitySnapshot KnucklesOnly() =>
        IsolateBranch("knuckles", ShipHierarchyIds.KnuckleStringers);

    public static VisibilitySnapshot PrimaryLongitudinal() =>
        IsolateBranch("longitudinal", ShipHierarchyIds.Longitudinal);

    private static VisibilitySnapshot IsolateBranch(string label, string keepId)
    {
        var overrides = new Dictionary<string, LocalVisibility>(StringComparer.OrdinalIgnoreCase)
        {
            [ShipHierarchyIds.ShipRoot] = LocalVisibility.Visible,
            [ShipHierarchyIds.PhysicalStructure] = LocalVisibility.Visible,
            [ShipHierarchyIds.ReferenceGeometry] = LocalVisibility.Hidden,
            [ShipHierarchyIds.Closures] = LocalVisibility.Hidden,
            [ShipHierarchyIds.Interior] = LocalVisibility.Hidden,
            [ShipHierarchyIds.CargoSystems] = LocalVisibility.Hidden,
            [ShipHierarchyIds.LoadedCargo] = LocalVisibility.Hidden,
            [ShipHierarchyIds.Machinery] = LocalVisibility.Hidden,
            [ShipHierarchyIds.Diagnostics] = LocalVisibility.Hidden,
            [ShipHierarchyIds.Hull] = LocalVisibility.Hidden,
            [ShipHierarchyIds.Transverse] = LocalVisibility.Hidden,
            [ShipHierarchyIds.Longitudinal] = LocalVisibility.Hidden,
            [ShipHierarchyIds.DeckStructure] = LocalVisibility.Hidden,
            [keepId] = LocalVisibility.Visible,
        };

        // Reveal ancestors of keepId
        if (keepId is ShipHierarchyIds.RingFrames or ShipHierarchyIds.HeavyBulkheads)
            overrides[ShipHierarchyIds.Transverse] = LocalVisibility.Visible;
        if (keepId is ShipHierarchyIds.KnuckleStringers or ShipHierarchyIds.PressureHullShell
            or ShipHierarchyIds.ShellStiffeners)
            overrides[ShipHierarchyIds.Hull] = LocalVisibility.Visible;
        if (keepId is ShipHierarchyIds.Longitudinal or ShipHierarchyIds.PrimaryHullGirders
            or ShipHierarchyIds.ThrustStructure)
            overrides[ShipHierarchyIds.Longitudinal] = LocalVisibility.Visible;

        return new VisibilitySnapshot { Label = label, Overrides = overrides };
    }

    private static VisibilitySnapshot Snapshot(
        string label,
        (string Id, LocalVisibility State)[] overrides)
    {
        var dict = new Dictionary<string, LocalVisibility>(StringComparer.OrdinalIgnoreCase);
        foreach (var (id, state) in overrides)
            dict[id] = state;
        return new VisibilitySnapshot { Label = label, Overrides = dict };
    }
}
