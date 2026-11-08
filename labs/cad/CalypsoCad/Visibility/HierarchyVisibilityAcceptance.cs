namespace CalypsoCad.Visibility;

/// <summary>Unit-style inheritance checks for <see cref="HierarchyVisibilityService"/> (no geometry).</summary>
internal static class HierarchyVisibilityAcceptance
{
    public static IReadOnlyList<(string Name, bool Ok, string Detail)> Run()
    {
        var results = new List<(string, bool, string)>();
        void Check(string name, bool ok, string detail = "") => results.Add((name, ok, detail));

        var svc = new HierarchyVisibilityService();
        // Minimal fake tree via overrides only — BindRegistry optional for parent walk.
        // Without registry, ParentOf returns null so Inherit at child = root Visible.
        // Use SetLocal on synthetic IDs with a tiny in-memory registry.

        var registry = new Model.ShipPartRegistry();
        Model.ShipHierarchyBuilder.EnsureCanonicalHierarchy(registry);
        svc.BindRegistry(registry);

        // Root Visible, Transverse Hidden, child Inherit → Hidden
        svc.ClearAllOverrides(recordUndo: false);
        svc.SetLocal(Model.ShipHierarchyIds.ShipRoot, LocalVisibility.Visible, false);
        svc.SetLocal(Model.ShipHierarchyIds.Transverse, LocalVisibility.Hidden, false);
        svc.SetLocal(Model.ShipHierarchyIds.RingFrames, LocalVisibility.Inherit, false);
        Check(
            "parent Hidden + child Inherit → Hidden",
            !svc.IsEffectivelyVisible(Model.ShipHierarchyIds.RingFrames));

        // Child Visible overrides Hidden parent
        svc.SetLocal(Model.ShipHierarchyIds.RingFrames, LocalVisibility.Visible, false);
        Check(
            "parent Hidden + child Visible → Visible",
            svc.IsEffectivelyVisible(Model.ShipHierarchyIds.RingFrames));

        // Parent Inherit, child Hidden
        svc.ClearAllOverrides(false);
        svc.SetLocal(Model.ShipHierarchyIds.ShipRoot, LocalVisibility.Visible, false);
        svc.SetLocal(Model.ShipHierarchyIds.Transverse, LocalVisibility.Inherit, false);
        svc.SetLocal(Model.ShipHierarchyIds.RingFrames, LocalVisibility.Hidden, false);
        Check(
            "parent Inherit + child Hidden → Hidden",
            !svc.IsEffectivelyVisible(Model.ShipHierarchyIds.RingFrames));

        // Root Hidden, child Visible
        svc.ClearAllOverrides(false);
        svc.SetLocal(Model.ShipHierarchyIds.ShipRoot, LocalVisibility.Hidden, false);
        svc.SetLocal(Model.ShipHierarchyIds.PhysicalStructure, LocalVisibility.Inherit, false);
        svc.SetLocal(Model.ShipHierarchyIds.Transverse, LocalVisibility.Visible, false);
        Check(
            "root Hidden + descendant Visible → Visible",
            svc.IsEffectivelyVisible(Model.ShipHierarchyIds.Transverse));

        // Parent change does not overwrite explicit child
        svc.ClearAllOverrides(false);
        svc.SetLocal(Model.ShipHierarchyIds.RingFrames, LocalVisibility.Hidden, false);
        svc.SetLocal(Model.ShipHierarchyIds.Transverse, LocalVisibility.Visible, false);
        Check(
            "parent Visible does not clear child Hidden",
            svc.GetLocal(Model.ShipHierarchyIds.RingFrames) == LocalVisibility.Hidden
            && !svc.IsEffectivelyVisible(Model.ShipHierarchyIds.RingFrames));

        // Isolation restore
        svc.ClearAllOverrides(false);
        svc.SetLocal(Model.ShipHierarchyIds.DeckStructure, LocalVisibility.Hidden, false);
        var before = svc.CaptureSnapshot();
        svc.BeginIsolation(Model.ShipHierarchyIds.KnuckleStringers);
        Check("isolation active", svc.IsolationActive);
        Check(
            "isolation shows knuckles",
            svc.IsEffectivelyVisible(Model.ShipHierarchyIds.KnuckleStringers));
        svc.EndIsolation();
        Check("isolation ended", !svc.IsolationActive);
        Check(
            "isolation restores prior DeckStructure Hidden",
            svc.GetLocal(Model.ShipHierarchyIds.DeckStructure) == LocalVisibility.Hidden,
            $"local={svc.GetLocal(Model.ShipHierarchyIds.DeckStructure)} prior={before.Overrides.Count}");

        return results;
    }
}
