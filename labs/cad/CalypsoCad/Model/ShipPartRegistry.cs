namespace CalypsoCad.Model;

/// <summary>
/// Central in-memory registry for one generate / load pass.
/// Assemblies and parts are registered before (or as) geometry is emitted.
/// Parent assembly IDs are authoritative; child lists are derived.
/// </summary>
internal sealed class ShipPartRegistry
{
    private readonly Dictionary<string, CadAssembly> _assemblies = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, CadPart> _parts = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, CadReferenceObject> _references = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<Guid>> _representations = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<StructuralConnection> _connections = [];
    private readonly List<string> _unclassified = [];
    private readonly List<string> _duplicateConflicts = [];

    public ShipDefinition? Ship { get; private set; }

    public IReadOnlyCollection<CadAssembly> Assemblies => _assemblies.Values;
    public IReadOnlyCollection<CadPart> Parts => _parts.Values;
    public IReadOnlyCollection<CadReferenceObject> References => _references.Values;
    public IReadOnlyList<StructuralConnection> Connections => _connections;
    public IReadOnlyList<string> Unclassified => _unclassified;
    public IReadOnlyList<string> DuplicateRegistrationConflicts => _duplicateConflicts;

    public void Clear()
    {
        _assemblies.Clear();
        _parts.Clear();
        _references.Clear();
        _representations.Clear();
        _connections.Clear();
        _unclassified.Clear();
        _duplicateConflicts.Clear();
        Ship = null;
    }

    public void SetShip(ShipDefinition ship)
    {
        ArgumentNullException.ThrowIfNull(ship);
        Ship = ship;
    }

    public CadAssembly RegisterAssembly(CadAssembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        if (_assemblies.TryGetValue(assembly.Id.Value, out var existing))
        {
            // Merge: preserve parent if new omit; union member parts.
            var members = existing.MemberPartIds?.ToList() ?? [];
            if (assembly.MemberPartIds is { Count: > 0 } incoming)
            {
                foreach (var m in incoming)
                {
                    if (!members.Any(x => string.Equals(x.Value, m.Value, StringComparison.OrdinalIgnoreCase)))
                        members.Add(m);
                }
            }

            var merged = existing with
            {
                DisplayName = string.IsNullOrWhiteSpace(assembly.DisplayName) ? existing.DisplayName : assembly.DisplayName,
                Kind = assembly.Kind != CadAssemblyKind.Other ? assembly.Kind : existing.Kind,
                Functions = existing.Functions | assembly.Functions,
                StationZStem = assembly.StationZStem ?? existing.StationZStem,
                ExtentZ0 = assembly.ExtentZ0 ?? existing.ExtentZ0,
                ExtentZ1 = assembly.ExtentZ1 ?? existing.ExtentZ1,
                ParentAssemblyId = assembly.ParentAssemblyId ?? existing.ParentAssemblyId,
                MemberPartIds = members,
                System = assembly.System != CadSystem.Organization ? assembly.System : existing.System,
            };
            _assemblies[assembly.Id.Value] = merged;
            return merged;
        }

        _assemblies[assembly.Id.Value] = assembly;
        return assembly;
    }

    /// <summary>
    /// Register a physical part. Same ID with conflicting EntityIds is recorded as a conflict
    /// and the existing EntityId is preserved (no silent overwrite of the representation link).
    /// </summary>
    public CadPart RegisterPart(CadPart part)
    {
        ArgumentNullException.ThrowIfNull(part);

        if (_parts.TryGetValue(part.Id.Value, out var existing))
        {
            // Same physical part, additional representation — keep primary EntityId.
            if (existing.EntityId is { } oldId
                && part.EntityId is { } newId
                && oldId != newId)
                RememberRepresentation(part.Id.Value, newId);

            var merged = existing with
            {
                EntityId = existing.EntityId ?? part.EntityId,
                FabPartId = part.FabPartId ?? existing.FabPartId,
                Geometry = part.Geometry ?? existing.Geometry,
                StationZStem = part.StationZStem ?? existing.StationZStem,
                DeckUp = part.DeckUp ?? existing.DeckUp,
                Functions = existing.Functions | part.Functions,
                Kind = part.Kind != CadPartKind.Unknown ? part.Kind : existing.Kind,
                System = part.System,
                Assembly = part.Assembly,
                DisplayName = string.IsNullOrWhiteSpace(part.DisplayName) ? existing.DisplayName : part.DisplayName,
                LegacyRole = part.LegacyRole ?? existing.LegacyRole,
                Classified = existing.Classified || part.Classified,
            };
            _parts[part.Id.Value] = merged;
            if (merged.EntityId is { } primary)
                RememberRepresentation(part.Id.Value, primary);
            EnsurePartOnAssembly(merged);
            return merged;
        }

        _parts[part.Id.Value] = part;
        if (part.EntityId is { } eid)
            RememberRepresentation(part.Id.Value, eid);
        EnsurePartOnAssembly(part);
        return part;
    }

    /// <summary>Attach EntityId to an already-registered part without inventing a second record.</summary>
    public bool TryAttachEntity(PartId partId, Guid entityId)
    {
        if (!_parts.TryGetValue(partId.Value, out var existing))
            return false;
        if (existing.EntityId is { } old && old != entityId)
        {
            RememberRepresentation(partId.Value, entityId);
            return true;
        }

        _parts[partId.Value] = existing with { EntityId = entityId };
        RememberRepresentation(partId.Value, entityId);
        return true;
    }

    public IReadOnlyList<Guid> RepresentationsOf(PartId partId) =>
        _representations.TryGetValue(partId.Value, out var list) ? list : [];

    public CadReferenceObject RegisterReference(CadReferenceObject reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        _references[reference.Id] = reference;
        return reference;
    }

    public void RegisterConnection(StructuralConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        _connections.Add(connection);
    }

    public void ReportUnclassified(string entityNameOrId)
    {
        if (string.IsNullOrWhiteSpace(entityNameOrId))
            return;
        if (!_unclassified.Contains(entityNameOrId, StringComparer.OrdinalIgnoreCase))
            _unclassified.Add(entityNameOrId);
    }

    public bool TryGetPart(PartId id, out CadPart part) =>
        _parts.TryGetValue(id.Value, out part!);

    public bool TryGetPartByDisplayName(string name, out CadPart part)
    {
        part = null!;
        if (string.IsNullOrWhiteSpace(name))
            return false;
        foreach (var p in _parts.Values)
        {
            if (string.Equals(p.DisplayName, name, StringComparison.OrdinalIgnoreCase)
                || string.Equals(p.Id.Value, name, StringComparison.OrdinalIgnoreCase))
            {
                part = p;
                return true;
            }
        }

        return false;
    }

    public bool TryGetAssembly(AssemblyId id, out CadAssembly assembly) =>
        _assemblies.TryGetValue(id.Value, out assembly!);

    public bool TryGetReference(string id, out CadReferenceObject reference) =>
        _references.TryGetValue(id, out reference!);

    public IEnumerable<CadAssembly> ChildAssemblies(AssemblyId parent)
    {
        foreach (var a in _assemblies.Values)
        {
            if (a.ParentAssemblyId is { } p
                && string.Equals(p.Value, parent.Value, StringComparison.OrdinalIgnoreCase))
                yield return a;
        }
    }

    public IEnumerable<CadPart> PartsOf(AssemblyId assembly)
    {
        foreach (var p in _parts.Values)
        {
            if (string.Equals(p.Assembly.Value, assembly.Value, StringComparison.OrdinalIgnoreCase))
                yield return p;
        }
    }

    /// <summary>All entity ids owned by an assembly and its nested descendants.</summary>
    public HashSet<Guid> CollectEntityIds(AssemblyId assembly)
    {
        var ids = new HashSet<Guid>();
        CollectEntityIdsRecursive(assembly, ids);
        return ids;
    }

    public HashSet<Guid> CollectEntityIds(PartId partId)
    {
        var ids = new HashSet<Guid>();
        foreach (var eid in RepresentationsOf(partId))
            ids.Add(eid);
        if (ids.Count == 0 && TryGetPart(partId, out var part) && part.EntityId is { } primary)
            ids.Add(primary);
        return ids;
    }

    private void CollectEntityIdsRecursive(AssemblyId assembly, HashSet<Guid> ids)
    {
        foreach (var p in PartsOf(assembly))
        {
            foreach (var eid in RepresentationsOf(p.Id))
                ids.Add(eid);
            if (p.EntityId is { } primary)
                ids.Add(primary);
        }

        foreach (var child in ChildAssemblies(assembly))
            CollectEntityIdsRecursive(child.Id, ids);
    }

    public IEnumerable<CadPart> Query(
        CadSystem? system = null,
        CadPartKind? kind = null,
        PartFunction? functions = null)
    {
        foreach (var p in _parts.Values)
        {
            if (system is { } s && p.System != s)
                continue;
            if (kind is { } k && p.Kind != k)
                continue;
            if (functions is { } f && (p.Functions & f) != f)
                continue;
            yield return p;
        }
    }

    public int CountBySystem(CadSystem system) =>
        _parts.Values.Count(p => p.System == system);

    public int BulkheadPlateCount =>
        _parts.Values.Count(p => p.Kind == CadPartKind.BulkheadPlate);

    public int FrameAssemblyCount =>
        _assemblies.Values.Count(a =>
            a.System == CadSystem.Transverse
            && a.Id.Value.StartsWith("FRAME-", StringComparison.OrdinalIgnoreCase));

    public int KnuckleAssemblyCount =>
        _assemblies.Values.Count(a => a.Kind == CadAssemblyKind.HullKnuckleStringer);

    private void EnsurePartOnAssembly(CadPart part)
    {
        if (_assemblies.TryGetValue(part.Assembly.Value, out var asm))
        {
            var members = asm.MemberPartIds?.ToList() ?? [];
            if (!members.Any(m => string.Equals(m.Value, part.Id.Value, StringComparison.OrdinalIgnoreCase)))
            {
                members.Add(part.Id);
                _assemblies[asm.Id.Value] = asm with { MemberPartIds = members };
            }
        }
        else
        {
            RegisterAssembly(new CadAssembly(
                part.Assembly,
                part.System,
                part.Assembly.Value,
                part.StationZStem,
                part.Functions,
                [part.Id]));
        }
    }

    private void RememberRepresentation(string partId, Guid entityId)
    {
        if (!_representations.TryGetValue(partId, out var list))
        {
            list = [];
            _representations[partId] = list;
        }

        if (!list.Contains(entityId))
            list.Add(entityId);
    }
}
