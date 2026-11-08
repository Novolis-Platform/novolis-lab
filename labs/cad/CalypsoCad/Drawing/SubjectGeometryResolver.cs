using CalypsoCad.Model;
using Novolis.Cad.Primitives;

namespace CalypsoCad.Drawing;

/// <summary>
/// Resolves a semantic hierarchy node into drawable geometry from the CAD document.
/// Does not reconstruct geometry from names — only follows registry ownership → entities.
/// </summary>
internal static class SubjectGeometryResolver
{
    public sealed record ResolveResult(
        string SubjectId,
        IReadOnlyList<DrawableGeometry> Geometry,
        IReadOnlyList<string> Diagnostics,
        bool HasDrawableGeometry);

    public static ResolveResult Resolve(
        string subjectId,
        ShipPartRegistry registry,
        CadDocument document)
    {
        var diagnostics = new List<string>();
        var entityToNode = new Dictionary<Guid, string>();
        CollectOwnedEntities(subjectId, registry, entityToNode, diagnostics);

        if (entityToNode.Count == 0
            && registry.TryGetReference(subjectId, out var reference)
            && reference.EntityId is { } refEid)
            entityToNode[refEid] = subjectId;

        var byId = document.Entities.ToDictionary(e => e.Id);
        var geometry = new List<DrawableGeometry>();
        foreach (var (eid, nodeId) in entityToNode)
        {
            if (!byId.TryGetValue(eid, out var entity))
            {
                diagnostics.Add($"Missing entity {eid:N} for node {nodeId}.");
                continue;
            }

            var g = CadGeometryNormalizer.Normalize(entity, nodeId);
            if (g.Kind == DrawablePrimitiveKind.Unsupported)
            {
                diagnostics.Add($"{g.SourceName}: {g.UnsupportedReason}");
                continue;
            }

            geometry.Add(g);
        }

        if (geometry.Count == 0)
            diagnostics.Add($"Subject '{subjectId}' has no drawable geometry.");

        return new ResolveResult(subjectId, geometry, diagnostics, geometry.Count > 0);
    }

    public static IReadOnlyList<CadPart> CollectPhysicalParts(string subjectId, ShipPartRegistry registry)
    {
        var parts = new List<CadPart>();
        if (registry.TryGetPart(PartId.Of(subjectId), out var part))
        {
            parts.Add(part);
            return parts;
        }

        if (!registry.TryGetAssembly(AssemblyId.Of(subjectId), out _))
            return parts;

        CollectPartsRecursive(AssemblyId.Of(subjectId), registry, parts);
        return parts;
    }

    private static void CollectPartsRecursive(AssemblyId assembly, ShipPartRegistry registry, List<CadPart> parts)
    {
        parts.AddRange(registry.PartsOf(assembly));
        foreach (var child in registry.ChildAssemblies(assembly))
            CollectPartsRecursive(child.Id, registry, parts);
    }

    private static void CollectOwnedEntities(
        string subjectId,
        ShipPartRegistry registry,
        Dictionary<Guid, string> entityToNode,
        List<string> diagnostics)
    {
        if (registry.TryGetPart(PartId.Of(subjectId), out var part))
        {
            foreach (var id in registry.CollectEntityIds(part.Id))
                entityToNode[id] = part.Id.Value;
            return;
        }

        if (registry.TryGetAssembly(AssemblyId.Of(subjectId), out var asm))
        {
            foreach (var id in registry.CollectEntityIds(asm.Id))
            {
                // Prefer the owning part id when available.
                var owner = subjectId;
                foreach (var p in registry.Parts)
                {
                    if (registry.CollectEntityIds(p.Id).Contains(id))
                    {
                        owner = p.Id.Value;
                        break;
                    }
                }

                entityToNode[id] = owner;
            }

            return;
        }

        if (registry.TryGetReference(subjectId, out _))
            return;

        diagnostics.Add($"Unknown subject '{subjectId}' — not a part, assembly, or reference in ShipPartRegistry.");
    }
}
