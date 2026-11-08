using CalypsoCad.Model;
using CalypsoCad.Structure;
using Novolis.Cad.Primitives;

namespace CalypsoCad.Generation.Transverse;

/// <summary>
/// Defines transverse assemblies first, then generates geometry via existing plate/frame emitters.
/// </summary>
internal static class TransverseStructureGenerator
{
    public static GenerationResult GenerateBulkhead(BulkheadSpec spec, ShipGeometryContext context, List<CadEntity> entities)
    {
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(entities);

        var functions = PartFunction.LoadBearing | PartFunction.HullAttached;
        if (spec.PressureTight)
            functions |= PartFunction.PressureTight;

        context.Registry.RegisterAssembly(new CadAssembly(
            spec.Assembly,
            CadSystem.Transverse,
            spec.Assembly.Value,
            spec.ZStem,
            functions,
            MemberPartIds: []));

        var before = entities.Count;
        var diagnostics = new List<string>();
        try
        {
            FrameWebPlateBuilder.EmitBulkhead(
                entities,
                context.Loa,
                $"bh-{spec.Assembly.Value.Replace("BH-", "", StringComparison.OrdinalIgnoreCase)}",
                spec.ZStem,
                spec.Thickness,
                spec.HalfDepthZ,
                spec.AthwartGaps,
                spec.BowAligned,
                spec.DeckUps,
                skipLightening: true,
                useCoarseSpans: true,
                hatchPortals: spec.HatchPortals);
        }
        catch (Exception ex)
        {
            diagnostics.Add($"FAIL {spec.Assembly.Value}: {ex.Message}");
            return new GenerationResult([], [], [], diagnostics);
        }

        var parts = new List<CadPart>();
        var reps = new List<GeneratedRepresentation>();
        for (var i = before; i < entities.Count; i++)
        {
            var e = entities[i];
            if (e.Name is null)
                continue;
            if (context.Registry.TryGetPartByDisplayName(e.Name, out var part))
            {
                parts.Add(part);
                reps.Add(new GeneratedRepresentation(part.Id, e));
            }
        }

        if (parts.Count == 0)
            diagnostics.Add($"FAIL {spec.Assembly.Value}: no plate/parts emitted");

        return new GenerationResult(parts, [], reps, diagnostics);
    }

    public static GenerationResult GenerateRingFrame(
        RingFrameSpec spec,
        ShipGeometryContext context,
        List<CadEntity> entities,
        Action<string, float, bool> placeFrame)
    {
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(entities);
        ArgumentNullException.ThrowIfNull(placeFrame);

        context.Registry.RegisterAssembly(new CadAssembly(
            spec.Assembly,
            CadSystem.Transverse,
            spec.Assembly.Value,
            spec.ZStem,
            PartFunction.LoadBearing | PartFunction.HullAttached,
            MemberPartIds: []));

        var before = entities.Count;
        placeFrame(spec.FrameEntityPrefix, spec.ZStem, spec.BowAligned);

        var parts = new List<CadPart>();
        var reps = new List<GeneratedRepresentation>();
        for (var i = before; i < entities.Count; i++)
        {
            var e = entities[i];
            if (e.Name is null)
                continue;
            if (context.Registry.TryGetPartByDisplayName(e.Name, out var part))
            {
                parts.Add(part);
                reps.Add(new GeneratedRepresentation(part.Id, e));
            }
        }

        return new GenerationResult(parts, [], reps, []);
    }
}
