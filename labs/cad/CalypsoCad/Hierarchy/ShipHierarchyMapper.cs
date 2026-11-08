using CalypsoCad.Model;
using Novolis.Cad.Hierarchy;
using HAssemblyId = Novolis.Cad.Hierarchy.AssemblyId;
using HPartId = Novolis.Cad.Hierarchy.PartId;

namespace CalypsoCad.Hierarchy;

/// <summary>
/// Projects Calypso <see cref="ShipPartRegistry"/> into a generic <see cref="CadHierarchyRegistry"/>
/// with domain tags in metadata (not in the generic API surface).
/// </summary>
internal static class ShipHierarchyMapper
{
    public static CadHierarchyRegistry ToHierarchy(ShipPartRegistry ship, string? rootId = null)
    {
        ArgumentNullException.ThrowIfNull(ship);
        var reg = new CadHierarchyRegistry
        {
            RootId = rootId ?? ShipHierarchyIds.ShipRoot,
        };

        foreach (var asm in ship.Assemblies)
        {
            reg.RegisterAssembly(new CadHierarchyAssembly(
                HAssemblyId.Of(asm.Id.Value),
                asm.DisplayName,
                asm.ParentAssemblyId is { } p ? HAssemblyId.Of(p.Value) : null,
                asm.MemberPartIds?.Select(m => HPartId.Of(m.Value)).ToList(),
                BuildAssemblyMeta(asm)));
        }

        foreach (var part in ship.Parts)
        {
            reg.RegisterPart(new CadHierarchyPart(
                HPartId.Of(part.Id.Value),
                HAssemblyId.Of(part.Assembly.Value),
                part.DisplayName,
                part.EntityId,
                BuildPartMeta(part)));

            foreach (var eid in ship.RepresentationsOf(part.Id))
                reg.AttachEntity(part.Id.Value, eid);
        }

        foreach (var reference in ship.References)
        {
            reg.RegisterReference(new CadHierarchyReference(
                reference.Id,
                reference.DisplayName,
                reference.ParentAssemblyId is { } p ? HAssemblyId.Of(p.Value) : null,
                reference.EntityId,
                BuildReferenceMeta(reference)));
        }

        return reg;
    }

    private static Dictionary<string, string> BuildAssemblyMeta(CadAssembly asm)
    {
        var meta = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [CalypsoHierarchyMetadata.CadSystem] = asm.System.ToString(),
            [CalypsoHierarchyMetadata.CadAssemblyKind] = asm.Kind.ToString(),
            [CalypsoHierarchyMetadata.PartFunctions] = asm.Functions.ToString(),
            [CalypsoHierarchyMetadata.ShipHierarchyId] = asm.Id.Value,
        };
        if (asm.StationZStem is { } z)
            meta[CalypsoHierarchyMetadata.StationZStem] = z.ToString("R");
        return meta;
    }

    private static Dictionary<string, string> BuildPartMeta(CadPart part)
    {
        var meta = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [CalypsoHierarchyMetadata.CadSystem] = part.System.ToString(),
            [CalypsoHierarchyMetadata.CadPartKind] = part.Kind.ToString(),
            [CalypsoHierarchyMetadata.PartFunctions] = part.Functions.ToString(),
            [CalypsoHierarchyMetadata.ShipHierarchyId] = part.Id.Value,
        };
        if (!string.IsNullOrWhiteSpace(part.LegacyRole))
            meta[CalypsoHierarchyMetadata.LegacyRole] = part.LegacyRole!;
        if (part.StationZStem is { } z)
            meta[CalypsoHierarchyMetadata.StationZStem] = z.ToString("R");
        if (part.DeckUp is { } up)
            meta[CalypsoHierarchyMetadata.DeckUp] = up.ToString("R");
        if (!string.IsNullOrWhiteSpace(part.FabPartId))
            meta[CalypsoHierarchyMetadata.FabPartId] = part.FabPartId!;
        return meta;
    }

    private static Dictionary<string, string> BuildReferenceMeta(CadReferenceObject reference)
    {
        var meta = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [CalypsoHierarchyMetadata.ReferenceKind] = reference.Kind.ToString(),
            [CalypsoHierarchyMetadata.ShipHierarchyId] = reference.Id,
        };
        if (reference.StationZStem is { } z)
            meta[CalypsoHierarchyMetadata.StationZStem] = z.ToString("R");
        if (reference.DeckUp is { } up)
            meta[CalypsoHierarchyMetadata.DeckUp] = up.ToString("R");
        return meta;
    }
}
