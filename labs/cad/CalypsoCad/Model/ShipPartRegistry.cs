namespace CalypsoCad.Model;

/// <summary>
/// Central in-memory registry for one generate / load pass.
/// Assemblies and parts are registered before (or as) geometry is emitted.
/// </summary>
internal sealed class ShipPartRegistry
{
    private readonly Dictionary<string, CadAssembly> _assemblies = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, CadPart> _parts = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<StructuralConnection> _connections = [];
    private readonly List<string> _unclassified = [];

    public ShipDefinition? Ship { get; private set; }

    public IReadOnlyCollection<CadAssembly> Assemblies => _assemblies.Values;
    public IReadOnlyCollection<CadPart> Parts => _parts.Values;
    public IReadOnlyList<StructuralConnection> Connections => _connections;
    public IReadOnlyList<string> Unclassified => _unclassified;

    public void Clear()
    {
        _assemblies.Clear();
        _parts.Clear();
        _connections.Clear();
        _unclassified.Clear();
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
        _assemblies[assembly.Id.Value] = assembly;
        return assembly;
    }

    public CadPart RegisterPart(CadPart part)
    {
        ArgumentNullException.ThrowIfNull(part);
        _parts[part.Id.Value] = part;

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

        return part;
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
}
