using CalypsoCad.Geometry;
using CalypsoCad.Model;
using CalypsoCad.Structure;
using Novolis.Cad.Primitives;

namespace CalypsoCad.Generation.Transverse;

/// <summary>
/// Defines a hull-conforming ring frame, derives <see cref="AnnularProfile"/>, then emits
/// one connected annular web (existing plate builders) plus deck chords via placeFrame.
/// </summary>
internal static class RingFrameGenerator
{
    public static GenerationResult Generate(
        RingFrameDefinition definition,
        ShipGeometryContext context,
        List<CadEntity> entities,
        Action<string, float, bool> placeFrame)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(entities);
        ArgumentNullException.ThrowIfNull(placeFrame);

        var diagnostics = new List<string>();
        var functions = PartFunction.LoadBearing | PartFunction.HullAttached;

        context.Registry.RegisterAssembly(new CadAssembly(
            definition.Id,
            CadSystem.Transverse,
            definition.Id.Value,
            definition.Station,
            functions,
            MemberPartIds: [],
            Kind: definition.AssemblyKind));

        AnnularProfile? profile = null;
        if (!definition.HoldPerimeterOnly)
        {
            profile = AnnularProfileBuilder.FromHullConforming(
                context.Hull,
                definition.Station,
                definition.WebDepth,
                definition.SkinInset);
            if (profile is null || !profile.IsValid)
            {
                diagnostics.Add(
                    $"WARN {definition.Id.Value}: annular profile collapsed at z={definition.Station:0.##}");
            }
        }

        // Register the annular web as a semantic part before (or without) geometry when profile exists.
        CadPart? webPart = null;
        if (profile is { IsValid: true })
        {
            var geom = new AnnularWebGeometry(
                definition.Station,
                definition.WebDepth,
                definition.PlateThickness,
                definition.SkinInset,
                profile);
            webPart = new CadPart(
                PartId.Of($"{definition.EntityPrefix}-annulus"),
                definition.Id,
                CadSystem.Transverse,
                CadPartKind.AnnularWebPlate,
                functions,
                $"{definition.EntityPrefix}-annulus",
                geom,
                definition.Station,
                FabPartId: $"P-{definition.Id.Value}-WEB",
                LegacyRole: StructuralRoles.TransverseFrame,
                Classified: true);
            context.Registry.RegisterPart(webPart);
        }

        var before = entities.Count;
        placeFrame(definition.EntityPrefix, definition.Station, definition.BowAligned);

        var parts = new List<CadPart>();
        var reps = new List<GeneratedRepresentation>();
        if (webPart is not null)
            parts.Add(webPart);

        for (var i = before; i < entities.Count; i++)
        {
            var e = entities[i];
            if (e.Name is null)
                continue;
            if (context.Registry.TryGetPartByDisplayName(e.Name, out var part))
            {
                if (parts.All(p => !string.Equals(p.Id.Value, part.Id.Value, StringComparison.OrdinalIgnoreCase)))
                    parts.Add(part);
                reps.Add(new GeneratedRepresentation(part.Id, e));
            }
        }

        if (parts.Count == 0)
            diagnostics.Add($"FAIL {definition.Id.Value}: no ring-frame parts emitted");

        return new GenerationResult(parts, [], reps, diagnostics);
    }

    public static RingFrameType InferType(float webDepth, bool holdPerimeterOnly)
    {
        if (holdPerimeterOnly)
            return RingFrameType.Ordinary;
        // Deeper webs (HOLD floor / heavy stations) — threshold matches ~0.55× typical web.
        return webDepth >= 0.22f ? RingFrameType.DeepWeb : RingFrameType.Ordinary;
    }
}
