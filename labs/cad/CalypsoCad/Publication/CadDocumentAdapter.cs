using System.Text.Json;
using CalypsoCad.Model;
using Novolis.Cad.Primitives;

namespace CalypsoCad.Publication;

/// <summary>
/// Bridges <see cref="ShipPartRegistry"/> ↔ <see cref="CadDocument"/> without breaking
/// existing Name / Guid / Kind contracts used by fab, blueprints, and joints.
/// </summary>
internal static class CadDocumentAdapter
{
    public const string PropPartId = "partId";
    public const string PropAssemblyId = "assemblyId";
    public const string PropCadSystem = "cadSystem";
    public const string PropCadPartKind = "cadPartKind";
    public const string PropPartFunctions = "partFunctions";
    public const string PropStructuralRole = "structuralRole";
    public const string PropAssembly = "assembly";
    public const string PropParentAssemblyId = "parentAssemblyId";
    public const string PropObjectClass = "objectClass"; // physical | reference | annotation | diagnostic

    /// <summary>Active registry for the current generate / load pass.</summary>
    public static ShipPartRegistry Active { get; } = new();

    public static void Reset() => Active.Clear();

    /// <summary>
    /// Stamp semantic properties onto an entity and register the corresponding <see cref="CadPart"/>.
    /// Preserves Name, Id, Kind. Mirrors legacy structuralRole/assembly for migration.
    /// </summary>
    public static CadPart StampAndRegister(
        CadEntity entity,
        string displayName,
        string legacyRole,
        string legacyAssembly,
        float? stationZStem = null,
        float? deckUp = null,
        string? fabPartId = null,
        GeometrySource? geometry = null,
        PartFunction? functionsOverride = null)
    {
        ArgumentNullException.ThrowIfNull(entity);
        // Prefer name classification for annular web / deck chord / etc. over coarse legacy role.
        CadSystem system;
        CadPartKind kind;
        PartFunction functions;
        if (StructuralRoleMap.TryClassifyFromName(displayName) is { } named)
        {
            system = named.System;
            kind = named.Kind;
            functions = named.Functions;
        }
        else
        {
            (system, kind, functions) = StructuralRoleMap.FromLegacyRole(legacyRole);
        }

        if (functionsOverride is { } fo)
            functions |= fo;

        var assemblyId = StructuralRoleMap.NormalizeAssemblyId(legacyAssembly, system);
        var partId = StructuralRoleMap.PartIdFromEntity(displayName, kind, assemblyId.Value);

        // Prefer stable part id for plates; otherwise keep display name as part id for joint compat.
        if (kind != CadPartKind.BulkheadPlate)
            partId = PartId.Of(displayName);

        var part = new CadPart(
            partId,
            assemblyId,
            system,
            kind,
            functions,
            displayName,
            geometry,
            stationZStem,
            deckUp,
            fabPartId,
            legacyRole,
            entity.Id,
            Classified: true);

        Active.RegisterPart(part);
        WriteProperties(entity, part, legacyRole, legacyAssembly);
        return part;
    }

    public static void WriteProperties(
        CadEntity entity,
        CadPart part,
        string? legacyRole = null,
        string? legacyAssembly = null)
    {
        entity.Properties ??= new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        var props = entity.Properties;
        props[PropPartId] = JsonSerializer.SerializeToElement(part.Id.Value);
        props[PropAssemblyId] = JsonSerializer.SerializeToElement(part.Assembly.Value);
        props[PropCadSystem] = JsonSerializer.SerializeToElement(part.System.ToString());
        props[PropCadPartKind] = JsonSerializer.SerializeToElement(part.Kind.ToString());
        props[PropPartFunctions] = JsonSerializer.SerializeToElement((int)part.Functions);
        props[PropObjectClass] = JsonSerializer.SerializeToElement("physical");

        var role = legacyRole ?? part.LegacyRole ?? StructuralRoleMap.LegacyRoleOf(part.Kind, part.Functions);
        var asm = legacyAssembly ?? part.Assembly.Value;
        props[PropStructuralRole] = JsonSerializer.SerializeToElement(role);
        props[PropAssembly] = JsonSerializer.SerializeToElement(asm);

        if (part.StationZStem is { } z)
            props["stationZ"] = JsonSerializer.SerializeToElement(z);
        if (part.DeckUp is { } d)
            props["deckUp"] = JsonSerializer.SerializeToElement(d);
        if (!string.IsNullOrWhiteSpace(part.FabPartId))
            props["fabPartId"] = JsonSerializer.SerializeToElement(part.FabPartId);

        if (Active.TryGetAssembly(part.Assembly, out var assembly)
            && assembly.ParentAssemblyId is { } parent)
            props[PropParentAssemblyId] = JsonSerializer.SerializeToElement(parent.Value);
    }

    /// <summary>Finalize hierarchy + write the semantic snapshot into the document properties.</summary>
    public static void WriteRegistryToDocument(CadDocument doc)
    {
        SemanticRegistrySnapshot.WriteToDocument(doc, Active);
    }

    /// <summary>
    /// Rebuild registry from a loaded document. Prefers the serialized semantic snapshot,
    /// then stamped entity properties, then name classification. Builds the ownership tree.
    /// </summary>
    public static ShipPartRegistry LoadFromDocument(CadDocument doc)
    {
        ArgumentNullException.ThrowIfNull(doc);
        Active.Clear();

        var fromSnapshot = SemanticRegistrySnapshot.TryReadFromDocument(doc, Active);

        foreach (var e in doc.Entities ?? [])
        {
            if (e.Name is null)
                continue;

            if (string.Equals(e.Kind, "space", StringComparison.OrdinalIgnoreCase))
            {
                // Spaces are reference compartment envelopes — not steel parts.
                continue;
            }

            if (TryReadStamped(e, out var stamped))
            {
                Active.RegisterPart(stamped with { EntityId = e.Id, DisplayName = e.Name });
                continue;
            }

            if (!IsClassifiableEntity(e))
                continue;

            var classified = StructuralRoleMap.TryClassifyFromName(e.Name);
            if (classified is null)
            {
                if (LooksPrimary(e.Name) || LooksInteriorOrCargo(e.Name))
                    Active.ReportUnclassified(e.Name);
                continue;
            }

            var (system, kind, functions, legacyRole, legacyAsm) = classified.Value;
            var assemblyId = StructuralRoleMap.NormalizeAssemblyId(legacyAsm, system);
            var partId = kind == CadPartKind.BulkheadPlate
                ? StructuralRoleMap.PartIdFromEntity(e.Name, kind, assemblyId.Value)
                : PartId.Of(e.Name);

            Active.RegisterPart(new CadPart(
                partId,
                assemblyId,
                system,
                kind,
                functions,
                e.Name,
                StationZStem: ReadFloat(e, "stationZ"),
                DeckUp: ReadFloat(e, "deckUp"),
                FabPartId: ReadString(e, "fabPartId"),
                LegacyRole: legacyRole,
                EntityId: e.Id,
                Classified: true));

            if (Active.TryGetPart(partId, out var part))
                WriteProperties(e, part, legacyRole, legacyAsm);
        }

        ShipHierarchyBuilder.EnsureCanonicalHierarchy(Active, doc);

        // When no snapshot was present, persist one so the next open round-trips cleanly.
        if (!fromSnapshot)
            SemanticRegistrySnapshot.WriteToDocument(doc, Active);

        return Active;
    }

    public static bool TryReadStamped(CadEntity e, out CadPart part)
    {
        part = null!;
        if (e.Properties is null)
            return false;
        if (!e.Properties.TryGetValue(PropPartId, out var pidEl)
            || pidEl.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(pidEl.GetString()))
            return false;
        if (!e.Properties.TryGetValue(PropAssemblyId, out var aidEl)
            || aidEl.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(aidEl.GetString()))
            return false;
        if (!e.Properties.TryGetValue(PropCadSystem, out var sysEl)
            || !Enum.TryParse<CadSystem>(sysEl.GetString(), ignoreCase: true, out var system))
            return false;
        if (!e.Properties.TryGetValue(PropCadPartKind, out var kindEl)
            || !Enum.TryParse<CadPartKind>(kindEl.GetString(), ignoreCase: true, out var kind))
            return false;

        var functions = PartFunction.None;
        if (e.Properties.TryGetValue(PropPartFunctions, out var fnEl) && fnEl.ValueKind == JsonValueKind.Number)
            functions = (PartFunction)fnEl.GetInt32();

        part = new CadPart(
            PartId.Of(pidEl.GetString()!),
            AssemblyId.Of(aidEl.GetString()!),
            system,
            kind,
            functions,
            e.Name ?? pidEl.GetString()!,
            StationZStem: ReadFloat(e, "stationZ"),
            DeckUp: ReadFloat(e, "deckUp"),
            FabPartId: ReadString(e, "fabPartId"),
            LegacyRole: ReadString(e, PropStructuralRole),
            EntityId: e.Id,
            Classified: true);
        return true;
    }

    public static bool IsStructureEntity(CadEntity e) => IsClassifiableEntity(e) && LooksPrimary(e.Name);

    public static bool IsClassifiableEntity(CadEntity e)
    {
        if (string.Equals(e.Kind, "box", StringComparison.OrdinalIgnoreCase)
            || string.Equals(e.Kind, "mesh", StringComparison.OrdinalIgnoreCase))
        {
            if (e.Properties is not null
                && e.Properties.TryGetValue(PropStructuralRole, out var role)
                && role.ValueKind == JsonValueKind.String
                && !string.IsNullOrEmpty(role.GetString()))
                return true;
            return LooksPrimary(e.Name) || LooksInteriorOrCargo(e.Name);
        }

        if (string.Equals(e.Kind, "wall", StringComparison.OrdinalIgnoreCase)
            || string.Equals(e.Kind, "opening", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    public static bool LooksPrimary(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;
        return name.StartsWith("frame-", StringComparison.OrdinalIgnoreCase)
               || name.StartsWith("girder-", StringComparison.OrdinalIgnoreCase)
               || name.StartsWith("stringer-", StringComparison.OrdinalIgnoreCase)
               || name.StartsWith("shell-", StringComparison.OrdinalIgnoreCase)
               || name.StartsWith("deck-sandwich-", StringComparison.OrdinalIgnoreCase)
               || name.StartsWith("bh-", StringComparison.OrdinalIgnoreCase)
               || name.StartsWith("coaming-", StringComparison.OrdinalIgnoreCase)
               || name.StartsWith("ramp-", StringComparison.OrdinalIgnoreCase)
               || name.Contains("-join-", StringComparison.OrdinalIgnoreCase);
    }

    public static bool LooksInteriorOrCargo(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;
        return name.StartsWith("outfit-", StringComparison.OrdinalIgnoreCase)
               || name.StartsWith("C40", StringComparison.OrdinalIgnoreCase)
               || name.StartsWith("w-", StringComparison.OrdinalIgnoreCase)
               || name.StartsWith("hatch-", StringComparison.OrdinalIgnoreCase)
               || name.StartsWith("door-", StringComparison.OrdinalIgnoreCase);
    }

    private static float? ReadFloat(CadEntity e, string key)
    {
        if (e.Properties is null || !e.Properties.TryGetValue(key, out var el))
            return null;
        return el.ValueKind == JsonValueKind.Number ? el.GetSingle() : null;
    }

    private static string? ReadString(CadEntity e, string key)
    {
        if (e.Properties is null || !e.Properties.TryGetValue(key, out var el))
            return null;
        return el.ValueKind == JsonValueKind.String ? el.GetString() : null;
    }
}
