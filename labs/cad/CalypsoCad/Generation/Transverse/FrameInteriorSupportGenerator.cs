using CalypsoCad.Layout;
using CalypsoCad.Model;
using CalypsoCad.Structure;
using Novolis.Cad.Primitives;

namespace CalypsoCad.Generation.Transverse;

/// <summary>
/// Secondary interior supports at ordinary ring frames: partition posts, portal jambs/headers.
/// Consumes <see cref="CompartmentLayout"/> — does not invent walls or recalculate the hull ring.
/// </summary>
internal static class FrameInteriorSupportGenerator
{
    public static GenerationResult Generate(
        List<CadEntity> entities,
        float loa,
        string frameName,
        AssemblyId frameAssembly,
        float zStem,
        float web,
        float halfDepthZ,
        CompartmentLayout layout,
        ShipGeometryContext context)
    {
        ArgumentNullException.ThrowIfNull(entities);
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(context);

        var plan = PartitionAttachmentPlan.Build(layout, zStem, web);
        var diagnostics = new List<string>(plan.Diagnostics);
        var parts = new List<CadPart>();
        var reps = new List<GeneratedRepresentation>();
        var before = entities.Count;
        var postI = 0;
        var jambI = 0;
        var headI = 0;

        foreach (var site in plan.Sites)
        {
            // Never place a vertical member inside a passage clear envelope.
            if (site.Kind is PartitionAttachmentPlan.AttachmentKind.PartitionPost
                && layout.YInsideAnyPassage(site.Y, zStem, margin: 0.08f))
            {
                diagnostics.Add($"REJECT post inside passage y={site.Y:0.##}");
                continue;
            }

            if (!CalypsoLockGenerator.InsideHullPublic(site.Y, (site.Up0 + site.Up1) * 0.5f, zStem, -0.02f))
            {
                diagnostics.Add($"REJECT outside IML y={site.Y:0.##}");
                continue;
            }

            var (kind, role, nameTag, idx) = site.Kind switch
            {
                PartitionAttachmentPlan.AttachmentKind.PartitionPost =>
                    (CadPartKind.PartitionAttachmentPost, StructuralRoles.PartitionAttachment,
                        "post", postI++),
                PartitionAttachmentPlan.AttachmentKind.PortalJamb =>
                    (CadPartKind.PortalJamb, StructuralRoles.PortalFrame, "jamb", jambI++),
                PartitionAttachmentPlan.AttachmentKind.PortalHeader =>
                    (CadPartKind.PortalHeader, StructuralRoles.PortalFrame, "hdr", headI++),
                _ => (CadPartKind.PartitionAttachmentRail, StructuralRoles.PartitionAttachment,
                    "rail", postI++),
            };

            var cup = (site.Up0 + site.Up1) * 0.5f;
            var hu = MathF.Max(0.04f, (site.Up1 - site.Up0) * 0.5f);
            var hy = MathF.Max(0.03f, site.HalfCrossY);
            var name = $"{frameName}-int-{nameTag}-{idx}-y{site.Y:0.#}";

            if (!CalypsoLockGenerator.TryAddStructureBar(
                    entities,
                    loa,
                    name,
                    site.Y,
                    cup,
                    zStem,
                    hy,
                    hu,
                    MathF.Max(halfDepthZ, web * 0.45f),
                    meta: new StructuralSolidMeta(
                        role,
                        frameAssembly.Value,
                        StationZ: zStem,
                        DeckUp: site.Up0,
                        Joins: [frameAssembly.Value, site.OwnerCompartmentId])))
            {
                diagnostics.Add($"FAIL place {name}");
                continue;
            }

            var part = new CadPart(
                PartId.Of($"{frameAssembly.Value}-{nameTag}-{idx}"),
                frameAssembly,
                CadSystem.Transverse,
                kind,
                PartFunction.LoadBearing,
                name,
                StationZStem: zStem,
                DeckUp: site.Up0,
                LegacyRole: role,
                Classified: true);
            context.Registry.RegisterPart(part);
            parts.Add(part);
        }

        for (var i = before; i < entities.Count; i++)
        {
            var e = entities[i];
            if (e.Name is null || !e.Name.Contains("-int-", StringComparison.OrdinalIgnoreCase))
                continue;
            if (context.Registry.TryGetPartByDisplayName(e.Name, out var p))
                reps.Add(new GeneratedRepresentation(p.Id, e));
        }

        return new GenerationResult(parts, [], reps, diagnostics);
    }
}
