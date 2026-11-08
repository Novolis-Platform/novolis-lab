using CalypsoCad.Hierarchy;
using CalypsoCad.Model;
using Novolis.Cad.Hierarchy;
using Novolis.Cad.Primitives;
using Novolis.Cad.Visibility;
using HAssemblyId = Novolis.Cad.Hierarchy.AssemblyId;

namespace CalypsoCad.Visibility;

/// <summary>
/// Calypso adapter over <see cref="Novolis.Cad.Visibility.HierarchyVisibilityService"/>.
/// Adds ship deck focus and name-prefix entity classification for legacy .cadjson.
/// </summary>
internal sealed class HierarchyVisibilityService
{
    private readonly Novolis.Cad.Visibility.HierarchyVisibilityService _inner = new();
    private ShipPartRegistry? _registry;
    private CadDocument? _document;
    private string _rootId = ShipHierarchyIds.ShipRoot;

    public event Action? Changed
    {
        add => _inner.Changed += value;
        remove => _inner.Changed -= value;
    }

    public bool IsolationActive => _inner.IsolationActive;
    public string? ActivePresetLabel => _inner.ActivePresetLabel;
    public bool HasEntityRevealFilter => _inner.HasEntityRevealFilter;

    /// <summary>Underlying generic service (Avalonia.Cad / multi-doc session).</summary>
    public Novolis.Cad.Visibility.HierarchyVisibilityService Inner => _inner;

    public void BindRegistry(
        ShipPartRegistry registry,
        CadDocument? document = null,
        string rootId = ShipHierarchyIds.ShipRoot)
    {
        _registry = registry;
        _document = document;
        _rootId = rootId;
        var hierarchy = ShipHierarchyMapper.ToHierarchy(registry, rootId);
        AttachDocumentFallbacks(hierarchy, document);
        _inner.BindHierarchy(hierarchy, document, rootId, ShipHierarchyIds.Diagnostics);
    }

    public LocalVisibility GetLocal(string nodeId) => _inner.GetLocal(nodeId);

    public bool IsEffectivelyVisible(string nodeId) => _inner.IsEffectivelyVisible(nodeId);

    public bool IsEntityEffectivelyVisible(Guid entityId) => _inner.IsEntityEffectivelyVisible(entityId);

    public void SetEntityRevealFilter(IEnumerable<Guid>? entityIds) =>
        _inner.SetEntityRevealFilter(entityIds);

    public void ClearEntityRevealFilter() => _inner.ClearEntityRevealFilter();

    public void ApplyDeckFocus(int? deck, bool recordUndo = true)
    {
        var next = CaptureSnapshot(deck is null ? ActivePresetLabel : $"deck:{deck.Value}");
        foreach (var (id, d) in new[]
                 {
                     (ShipHierarchyIds.DeckM1, -1),
                     (ShipHierarchyIds.Deck0, 0),
                     (ShipHierarchyIds.DeckP1, 1),
                 })
        {
            if (deck is null)
                next.Overrides.Remove(id);
            else
                next.Overrides[id] = d == deck.Value ? LocalVisibility.Visible : LocalVisibility.Hidden;
        }

        ApplySnapshot(next, recordUndo, asPreset: next.Label);
    }

    public string? SemanticNodeForEntity(Guid entityId) => _inner.SemanticNodeForEntity(entityId);

    public IReadOnlyCollection<Guid> UnclassifiedEntityIds(IEnumerable<Guid> allEntityIds)
    {
        var missing = new List<Guid>();
        foreach (var id in allEntityIds)
        {
            if (_inner.SemanticNodeForEntity(id) is null)
                missing.Add(id);
        }

        return missing;
    }

    public void SetLocal(string nodeId, LocalVisibility state, bool recordUndo = true) =>
        _inner.SetLocal(nodeId, state, recordUndo);

    public void ClearAllOverrides(bool recordUndo = true) =>
        _inner.ClearAllOverrides(recordUndo);

    public void ApplySnapshot(VisibilitySnapshot snapshot, bool recordUndo = true, string? asPreset = null) =>
        _inner.ApplySnapshot(snapshot, recordUndo, asPreset);

    public VisibilitySnapshot CaptureSnapshot(string? label = null) =>
        _inner.CaptureSnapshot(label);

    public void BeginIsolation(string selectedNodeId) =>
        _inner.BeginIsolation(selectedNodeId);

    public void EndIsolation() => _inner.EndIsolation();

    public void Undo() => _inner.Undo();

    public void Redo() => _inner.Redo();

    public ChildVisibilitySummary SummarizeChildren(string nodeId) =>
        _inner.SummarizeChildren(nodeId);

    public string? InheritedFrom(string nodeId) => _inner.InheritedFrom(nodeId);

    public void RebuildEntityMap(CadDocument? document = null)
    {
        if (_registry is null)
            return;
        BindRegistry(_registry, document ?? _document, _rootId);
    }

    private static void AttachDocumentFallbacks(CadHierarchyRegistry hierarchy, CadDocument? document)
    {
        if (document?.Entities is null)
            return;

        var known = new HashSet<Guid>();
        foreach (var asm in hierarchy.Assemblies)
        {
            foreach (var eid in hierarchy.CollectEntityIds(asm.Id))
                known.Add(eid);
        }

        foreach (var part in hierarchy.Parts)
        {
            foreach (var eid in hierarchy.CollectEntityIds(part.Id))
                known.Add(eid);
        }

        foreach (var reference in hierarchy.References)
        {
            if (reference.EntityId is { } eid)
                known.Add(eid);
        }

        foreach (var e in document.Entities)
        {
            if (known.Contains(e.Id))
                continue;

            var n = e.Name ?? "";
            string owner;
            if (string.Equals(e.Kind, "wall", StringComparison.OrdinalIgnoreCase)
                || n.StartsWith("w-", StringComparison.OrdinalIgnoreCase))
                owner = ShipHierarchyIds.Partitions;
            else if (string.Equals(e.Kind, "space", StringComparison.OrdinalIgnoreCase))
                owner = ShipHierarchyIds.CompartmentEnvelopes;
            else if (string.Equals(e.Kind, "opening", StringComparison.OrdinalIgnoreCase)
                     || n.StartsWith("outfit-hatch-", StringComparison.OrdinalIgnoreCase))
                owner = ShipHierarchyIds.Closures;
            else if (n.StartsWith("outfit-", StringComparison.OrdinalIgnoreCase))
                owner = ShipHierarchyIds.Furniture;
            else if (n.StartsWith("C40", StringComparison.OrdinalIgnoreCase))
                owner = ShipHierarchyIds.LoadedCargo;
            else if (n.Contains("oml", StringComparison.OrdinalIgnoreCase)
                     || n.Contains("iml", StringComparison.OrdinalIgnoreCase)
                     || n.Contains("hull", StringComparison.OrdinalIgnoreCase))
                owner = ShipHierarchyIds.PressureHullShell;
            else
                owner = ShipHierarchyIds.Diagnostics;

            if (hierarchy.TryGetAssembly(HAssemblyId.Of(owner), out _))
                hierarchy.AttachEntity(owner, e.Id);
            else
                hierarchy.AttachEntity(ShipHierarchyIds.Diagnostics, e.Id);
        }
    }
}
