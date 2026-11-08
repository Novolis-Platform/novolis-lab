using CalypsoCad.Model;
using Novolis.Cad.Primitives;

namespace CalypsoCad.Visibility;

/// <summary>
/// Sole authority for physical/reference object visibility. Local Inherit/Visible/Hidden;
/// effective visibility walks ancestors until an explicit state (root defaults Visible).
/// A descendant with explicit Visible overrides a Hidden ancestor.
/// </summary>
internal sealed class HierarchyVisibilityService
{
    private readonly Dictionary<string, LocalVisibility> _overrides = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, bool> _effectiveCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<Guid, string> _entityToNode = new();
    private readonly Stack<VisibilitySnapshot> _isolationStack = new();
    private readonly Stack<VisibilitySnapshot> _undo = new();
    private readonly Stack<VisibilitySnapshot> _redo = new();
    /// <summary>
    /// Optional entity-level reveal filter owned by this service (build-step / inspection isolate).
    /// Null = no filter. Consulted only inside <see cref="IsEntityEffectivelyVisible"/>.
    /// </summary>
    private HashSet<Guid>? _entityReveal;
    private ShipPartRegistry? _registry;
    private string _rootId = ShipHierarchyIds.ShipRoot;

    public event Action? Changed;

    public bool IsolationActive => _isolationStack.Count > 0 || _entityReveal is not null;
    public string? ActivePresetLabel { get; private set; }
    public bool HasEntityRevealFilter => _entityReveal is not null;

    public void BindRegistry(
        ShipPartRegistry registry,
        CadDocument? document = null,
        string rootId = ShipHierarchyIds.ShipRoot)
    {
        _registry = registry;
        _rootId = rootId;
        RebuildEntityMap(document);
        InvalidateCache();
        Changed?.Invoke();
    }

    public LocalVisibility GetLocal(string nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
            return LocalVisibility.Inherit;
        return _overrides.TryGetValue(nodeId, out var v) ? v : LocalVisibility.Inherit;
    }

    public bool IsEffectivelyVisible(string nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
            return true;
        if (_effectiveCache.TryGetValue(nodeId, out var cached))
            return cached;

        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = ResolveWalk(nodeId, visited);
        _effectiveCache[nodeId] = result;
        return result;
    }

    public bool IsEntityEffectivelyVisible(Guid entityId)
    {
        if (_entityReveal is not null && !_entityReveal.Contains(entityId))
            return false;

        if (_entityToNode.TryGetValue(entityId, out var nodeId))
            return IsEffectivelyVisible(nodeId);

        // Unclassified — diagnostic path: treat as Diagnostics group (visible unless Diagnostics hidden).
        return IsEffectivelyVisible(ShipHierarchyIds.Diagnostics);
    }

    /// <summary>
    /// Temporary entity-set reveal for progressive build / inspection dumps.
    /// Lives inside this service — renderer must not consult a parallel HashSet.
    /// </summary>
    public void SetEntityRevealFilter(IEnumerable<Guid>? entityIds)
    {
        _entityReveal = entityIds is null ? null : new HashSet<Guid>(entityIds);
        Changed?.Invoke();
    }

    public void ClearEntityRevealFilter()
    {
        if (_entityReveal is null)
            return;
        _entityReveal = null;
        Changed?.Invoke();
    }

    /// <summary>Deck-focused view: overrides Deck −1/0/+1 nodes without rewriting ownership.</summary>
    public void ApplyDeckFocus(int? deck, bool recordUndo = true)
    {
        if (recordUndo)
            PushUndo();

        foreach (var (id, d) in new[]
                 {
                     (ShipHierarchyIds.DeckM1, -1),
                     (ShipHierarchyIds.Deck0, 0),
                     (ShipHierarchyIds.DeckP1, 1),
                 })
        {
            if (deck is null)
                _overrides.Remove(id);
            else
                _overrides[id] = d == deck.Value ? LocalVisibility.Visible : LocalVisibility.Hidden;
        }

        ActivePresetLabel = deck is null ? ActivePresetLabel : $"deck:{deck.Value}";
        InvalidateCache();
        Changed?.Invoke();
    }

    public string? SemanticNodeForEntity(Guid entityId) =>
        _entityToNode.TryGetValue(entityId, out var id) ? id : null;

    public IReadOnlyCollection<Guid> UnclassifiedEntityIds(IEnumerable<Guid> allEntityIds)
    {
        var missing = new List<Guid>();
        foreach (var id in allEntityIds)
        {
            if (!_entityToNode.ContainsKey(id))
                missing.Add(id);
        }

        return missing;
    }

    public void SetLocal(string nodeId, LocalVisibility state, bool recordUndo = true)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
            return;
        if (recordUndo)
            PushUndo();

        if (state == LocalVisibility.Inherit)
            _overrides.Remove(nodeId);
        else
            _overrides[nodeId] = state;

        ActivePresetLabel = null;
        InvalidateCache();
        Changed?.Invoke();
    }

    public void ClearAllOverrides(bool recordUndo = true)
    {
        if (recordUndo)
            PushUndo();
        _overrides.Clear();
        ActivePresetLabel = null;
        InvalidateCache();
        Changed?.Invoke();
    }

    public void ApplySnapshot(VisibilitySnapshot snapshot, bool recordUndo = true, string? asPreset = null)
    {
        if (recordUndo)
            PushUndo();
        _overrides.Clear();
        _entityReveal = null;
        foreach (var (k, v) in snapshot.Overrides)
        {
            if (v != LocalVisibility.Inherit)
                _overrides[k] = v;
        }

        ActivePresetLabel = asPreset ?? snapshot.Label;
        InvalidateCache();
        Changed?.Invoke();
    }

    public VisibilitySnapshot CaptureSnapshot(string? label = null) =>
        new()
        {
            Label = label ?? ActivePresetLabel,
            Overrides = new Dictionary<string, LocalVisibility>(_overrides, StringComparer.OrdinalIgnoreCase),
        };

    /// <summary>Temporary isolation: selected subtree Visible; everything else Hidden via root children.</summary>
    public void BeginIsolation(string selectedNodeId)
    {
        if (_registry is null || string.IsNullOrWhiteSpace(selectedNodeId))
            return;
        // Nested isolate: each Begin pushes; EndIsolation restores the previous scope.
        _isolationStack.Push(CaptureSnapshot("pre-isolation"));
        PushUndo();

        _overrides.Clear();
        _entityReveal = null;
        // Hide physical + reference roots; keep selected path Visible.
        HideAllTopLevelExcept(selectedNodeId);
        EnsureAncestorsVisible(selectedNodeId);
        SetLocal(selectedNodeId, LocalVisibility.Visible, recordUndo: false);
        // Hide siblings of the selection chain
        HideUnrelatedBranches(selectedNodeId);

        ActivePresetLabel = $"isolate:{selectedNodeId}";
        InvalidateCache();
        Changed?.Invoke();
    }

    public void EndIsolation()
    {
        if (_entityReveal is not null)
            ClearEntityRevealFilter();
        if (_isolationStack.Count == 0)
            return;
        var prior = _isolationStack.Pop();
        ApplySnapshot(prior, recordUndo: true, asPreset: prior.Label);
    }

    public void Undo()
    {
        if (_undo.Count == 0)
            return;
        _redo.Push(CaptureSnapshot());
        var prev = _undo.Pop();
        ApplySnapshot(prev, recordUndo: false, asPreset: prev.Label);
    }

    public void Redo()
    {
        if (_redo.Count == 0)
            return;
        _undo.Push(CaptureSnapshot());
        var next = _redo.Pop();
        ApplySnapshot(next, recordUndo: false, asPreset: next.Label);
    }

    /// <summary>Aggregate of children: all visible / all hidden / mixed (display only).</summary>
    public ChildVisibilitySummary SummarizeChildren(string nodeId)
    {
        if (_registry is null || string.IsNullOrWhiteSpace(nodeId))
            return ChildVisibilitySummary.None;
        var aid = AssemblyId.Of(nodeId);
        if (!_registry.TryGetAssembly(aid, out _))
            return ChildVisibilitySummary.None;

        var anyVis = false;
        var anyHid = false;
        foreach (var child in _registry.ChildAssemblies(aid))
        {
            if (IsEffectivelyVisible(child.Id.Value))
                anyVis = true;
            else
                anyHid = true;
        }

        foreach (var part in _registry.PartsOf(aid))
        {
            if (IsEffectivelyVisible(part.Id.Value))
                anyVis = true;
            else
                anyHid = true;
        }

        if (anyVis && anyHid)
            return ChildVisibilitySummary.Mixed;
        if (anyHid)
            return ChildVisibilitySummary.AllHidden;
        if (anyVis)
            return ChildVisibilitySummary.AllVisible;
        return ChildVisibilitySummary.None;
    }

    public string? InheritedFrom(string nodeId)
    {
        var local = GetLocal(nodeId);
        if (local != LocalVisibility.Inherit)
            return null;
        var parent = ParentOf(nodeId);
        while (parent is not null)
        {
            if (GetLocal(parent) != LocalVisibility.Inherit)
                return parent;
            parent = ParentOf(parent);
        }

        return _rootId;
    }

    public void RebuildEntityMap(CadDocument? document = null)
    {
        _entityToNode.Clear();
        if (_registry is null)
            return;

        foreach (var part in _registry.Parts)
        {
            foreach (var eid in _registry.CollectEntityIds(part.Id))
                _entityToNode[eid] = part.Id.Value;
        }

        foreach (var reference in _registry.References)
        {
            if (reference.EntityId is { } eid)
                _entityToNode[eid] = reference.Id;
        }

        foreach (var asm in _registry.Assemblies)
        {
            foreach (var eid in _registry.CollectEntityIds(asm.Id))
            {
                if (!_entityToNode.ContainsKey(eid))
                    _entityToNode[eid] = asm.Id.Value;
            }
        }

        // Document leftovers → semantic owners (no silent bypass).
        if (document?.Entities is null)
            return;
        foreach (var e in document.Entities)
        {
            if (_entityToNode.ContainsKey(e.Id))
                continue;
            var n = e.Name ?? "";
            if (string.Equals(e.Kind, "wall", StringComparison.OrdinalIgnoreCase)
                || n.StartsWith("w-", StringComparison.OrdinalIgnoreCase))
                _entityToNode[e.Id] = ShipHierarchyIds.Partitions;
            else if (string.Equals(e.Kind, "space", StringComparison.OrdinalIgnoreCase))
                _entityToNode[e.Id] = ShipHierarchyIds.CompartmentEnvelopes;
            else if (string.Equals(e.Kind, "opening", StringComparison.OrdinalIgnoreCase)
                     || n.StartsWith("outfit-hatch-", StringComparison.OrdinalIgnoreCase))
                _entityToNode[e.Id] = ShipHierarchyIds.Closures;
            else if (n.StartsWith("outfit-", StringComparison.OrdinalIgnoreCase))
                _entityToNode[e.Id] = ShipHierarchyIds.Furniture;
            else if (n.StartsWith("C40", StringComparison.OrdinalIgnoreCase))
                _entityToNode[e.Id] = ShipHierarchyIds.LoadedCargo;
            else if (n.Contains("oml", StringComparison.OrdinalIgnoreCase)
                     || n.Contains("iml", StringComparison.OrdinalIgnoreCase)
                     || n.Contains("hull", StringComparison.OrdinalIgnoreCase))
                _entityToNode[e.Id] = ShipHierarchyIds.PressureHullShell;
            else
                _entityToNode[e.Id] = ShipHierarchyIds.Diagnostics;
        }
    }

    private bool ResolveWalk(string nodeId, HashSet<string> visited)
    {
        if (!visited.Add(nodeId))
            return true; // cycle → visible

        var local = GetLocal(nodeId);
        if (local == LocalVisibility.Visible)
            return true;
        if (local == LocalVisibility.Hidden)
            return false;

        if (string.Equals(nodeId, _rootId, StringComparison.OrdinalIgnoreCase))
            return true; // root Inherit ≡ Visible

        var parent = ParentOf(nodeId);
        if (parent is null)
            return true;
        return ResolveWalk(parent, visited);
    }

    private string? ParentOf(string nodeId)
    {
        if (_registry is null || string.IsNullOrWhiteSpace(nodeId))
            return null;
        if (_registry.TryGetAssembly(AssemblyId.Of(nodeId), out var asm))
            return asm.ParentAssemblyId?.Value;
        if (_registry.TryGetPart(PartId.Of(nodeId), out var part))
            return part.Assembly.Value;
        if (_registry.TryGetReference(nodeId, out var reference))
            return reference.ParentAssemblyId?.Value;
        return null;
    }

    private void HideAllTopLevelExcept(string keepNodeId)
    {
        if (_registry is null)
            return;
        foreach (var child in _registry.ChildAssemblies(AssemblyId.Of(_rootId)))
        {
            if (IsAncestorOf(child.Id.Value, keepNodeId) || string.Equals(child.Id.Value, keepNodeId, StringComparison.OrdinalIgnoreCase))
                continue;
            _overrides[child.Id.Value] = LocalVisibility.Hidden;
        }
    }

    private void EnsureAncestorsVisible(string nodeId)
    {
        var p = ParentOf(nodeId);
        while (p is not null)
        {
            _overrides[p] = LocalVisibility.Visible;
            p = ParentOf(p);
        }

        _overrides[_rootId] = LocalVisibility.Visible;
    }

    private void HideUnrelatedBranches(string selectedNodeId)
    {
        if (_registry is null)
            return;
        // Walk from selection to root; hide siblings at each level.
        var current = selectedNodeId;
        while (true)
        {
            var parent = ParentOf(current);
            if (parent is null)
                break;
            var paid = AssemblyId.Of(parent);
            if (!_registry.TryGetAssembly(paid, out _))
                break;
            foreach (var sib in _registry.ChildAssemblies(paid))
            {
                if (string.Equals(sib.Id.Value, current, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (IsAncestorOf(sib.Id.Value, selectedNodeId))
                    continue;
                _overrides[sib.Id.Value] = LocalVisibility.Hidden;
            }

            foreach (var part in _registry.PartsOf(paid))
            {
                if (string.Equals(part.Id.Value, current, StringComparison.OrdinalIgnoreCase))
                    continue;
                _overrides[part.Id.Value] = LocalVisibility.Hidden;
            }

            current = parent;
        }
    }

    private bool IsAncestorOf(string ancestorId, string descendantId)
    {
        var p = ParentOf(descendantId);
        while (p is not null)
        {
            if (string.Equals(p, ancestorId, StringComparison.OrdinalIgnoreCase))
                return true;
            p = ParentOf(p);
        }

        return false;
    }

    private void PushUndo()
    {
        _undo.Push(CaptureSnapshot());
        _redo.Clear();
    }

    private void InvalidateCache() => _effectiveCache.Clear();
}
