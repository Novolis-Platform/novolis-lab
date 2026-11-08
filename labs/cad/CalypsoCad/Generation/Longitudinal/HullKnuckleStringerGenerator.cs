using System.Globalization;
using System.Numerics;
using System.Text.Json;
using CalypsoCad.Geometry;
using CalypsoCad.Model;
using CalypsoCad.Structure;
using Novolis.Cad.Primitives;
using Novolis.Math.Geometry;

namespace CalypsoCad.Generation.Longitudinal;

/// <summary>
/// Emits one <see cref="CadAssemblyKind.HullKnuckleStringer"/> per octagon knuckle,
/// with manufactured mesh segments following the continuous 3D knuckle path between
/// structural joint stations (frames + heavy bulkheads).
/// </summary>
internal static class HullKnuckleStringerGenerator
{
    public static GenerationResult Generate(
        IReadOnlyList<HullKnucklePath> paths,
        float loa,
        float web,
        ShipGeometryContext context,
        List<CadEntity> entities)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(entities);
        if (paths.Count != HullKnucklePath.KnuckleCount)
            throw new ArgumentException($"Expected {HullKnucklePath.KnuckleCount} knuckle paths.", nameof(paths));

        var parts = new List<CadPart>();
        var diagnostics = new List<string>();
        var stiffHy = MathF.Max(0.04f, web * 0.22f);
        var stiffHu = MathF.Max(0.045f, web * 0.24f);
        var functions = PartFunction.LoadBearing | PartFunction.HullAttached;

        foreach (var path in paths)
        {
            var asmId = AssemblyId.Of(path.AssemblyId);
            context.Registry.RegisterAssembly(new CadAssembly(
                asmId,
                CadSystem.Hull,
                path.AssemblyId,
                path.Samples[0].ZStem,
                functions,
                MemberPartIds: [],
                Kind: CadAssemblyKind.HullKnuckleStringer));

            var placed = 0;
            for (var s = 0; s < path.Samples.Count - 1; s++)
            {
                var a = path.Samples[s];
                var b = path.Samples[s + 1];
                if (b.ZStem - a.ZStem < 0.35f)
                    continue;

                if (!TryEmitSegment(
                        entities, loa, path, s, a, b, stiffHy, stiffHu, asmId, functions, context, parts, diagnostics))
                    continue;
                placed++;
            }

            if (placed == 0)
                diagnostics.Add($"FAIL {path.AssemblyId}: no segments placed along path");
            else
                diagnostics.Add($"OK {path.AssemblyId}: {placed} segments z={path.ZStart:0.#}→{path.ZEnd:0.#}");
        }

        return new GenerationResult(parts, [], [], diagnostics);
    }

    private static bool TryEmitSegment(
        List<CadEntity> entities,
        float loa,
        HullKnucklePath path,
        int segIndex,
        HullKnuckleSample a,
        HullKnuckleSample b,
        float stiffHy,
        float stiffHu,
        AssemblyId asmId,
        PartFunction functions,
        ShipGeometryContext context,
        List<CadPart> parts,
        List<string> diagnostics)
    {
        var (cyA, cuA) = SectionCenter(context.Hull, a.ZStem);
        var (cyB, cuB) = SectionCenter(context.Hull, b.ZStem);

        for (var shrink = 0; shrink < 8; shrink++)
        {
            var hy = stiffHy * MathF.Pow(0.88f, shrink);
            var hu = stiffHu * MathF.Pow(0.88f, shrink);
            if (hy < 0.028f || hu < 0.028f)
                break;

            var pull = MathF.Max(hy, hu) * 1.42f;
            var (y0, u0) = PullInward(a.Y, a.Up, cyA, cuA, pull);
            var (y1, u1) = PullInward(b.Y, b.Up, cyB, cuB, pull);

            // Containment at ends + mid — fail closed rather than emit floating steel.
            var zMid = (a.ZStem + b.ZStem) * 0.5f;
            var yMid = (y0 + y1) * 0.5f;
            var uMid = (u0 + u1) * 0.5f;
            if (!CalypsoLockGenerator.InsideHullPublic(y0, u0, a.ZStem, 0.08f)
                || !CalypsoLockGenerator.InsideHullPublic(y1, u1, b.ZStem, 0.08f)
                || !CalypsoLockGenerator.InsideHullPublic(yMid, uMid, zMid, 0.08f))
                continue;

            var wa = CalypsoLockGenerator.LockToWorldPublic(y0, u0, a.ZStem, loa);
            var wb = CalypsoLockGenerator.LockToWorldPublic(y1, u1, b.ZStem, loa);
            EditableMesh mesh;
            try
            {
                mesh = SweptRectSection.Build(wa, wb, hy, hu);
            }
            catch (ArgumentException ex)
            {
                diagnostics.Add($"WARN {path.AssemblyId} s{segIndex}: {ex.Message}");
                return false;
            }

            var name = $"stringer-knuckle-v{path.VertexIndex}-s{segIndex}";
            var meta = new StructuralSolidMeta(
                StructuralRoles.ShellStiffener,
                path.AssemblyId,
                StationZ: zMid,
                DeckUp: uMid,
                Joins: JointLabels(a.ZStem, b.ZStem));

            if (!CalypsoLockGenerator.TryAddStructureMesh(
                    entities, loa, name, zMid, mesh, fabPartId: $"P-{path.AssemblyId}-S{segIndex}", meta: meta))
                continue;

            StampSegmentProps(entities[^1], path.VertexIndex, a.ZStem, b.ZStem, path.AssemblyId);

            var part = new CadPart(
                PartId.Of(name),
                asmId,
                CadSystem.Hull,
                CadPartKind.HullStringer,
                functions,
                name,
                Geometry: null,
                StationZStem: zMid,
                FabPartId: $"P-{path.AssemblyId}-S{segIndex}",
                LegacyRole: StructuralRoles.ShellStiffener,
                Classified: true);
            context.Registry.RegisterPart(part);
            parts.Add(part);
            return true;
        }

        diagnostics.Add(
            $"WARN {path.AssemblyId} s{segIndex}: segment z={a.ZStem:0.##}→{b.ZStem:0.##} failed containment");
        return false;
    }

    private static (float Cy, float Cu) SectionCenter(HullGeometry hull, float zStem)
    {
        var ring = hull.InnerOctagonRing(zStem, 0f);
        return ring.GetSectionCenter();
    }

    private static (float Y, float Up) PullInward(
        float y, float up, float cy, float cu, float pull)
    {
        var iy = cy - y;
        var iu = cu - up;
        var len = MathF.Sqrt(iy * iy + iu * iu);
        if (len < 1e-4f)
            return (y, up);
        return (y + iy / len * pull, up + iu / len * pull);
    }

    private static string[] JointLabels(float z0, float z1) =>
    [
        $"z{z0.ToString("0.#", CultureInfo.InvariantCulture)}",
        $"z{z1.ToString("0.#", CultureInfo.InvariantCulture)}",
    ];

    private static void StampSegmentProps(
        CadEntity entity, int vertex, float z0, float z1, string assembly)
    {
        var props = entity.Properties ?? new Dictionary<string, JsonElement>();
        props["platingJoinVertex"] = JsonSerializer.SerializeToElement(vertex);
        props["platingJoin"] = JsonSerializer.SerializeToElement(true);
        props["knucklePath"] = JsonSerializer.SerializeToElement(true);
        props["z0"] = JsonSerializer.SerializeToElement(z0);
        props["z1"] = JsonSerializer.SerializeToElement(z1);
        props["assembly"] = JsonSerializer.SerializeToElement(assembly);
        entity.Properties = props;
    }
}
