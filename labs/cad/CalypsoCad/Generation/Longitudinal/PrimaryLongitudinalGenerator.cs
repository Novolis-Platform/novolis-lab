using CalypsoCad.Structure;
using Novolis.Cad.Primitives;

namespace CalypsoCad.Generation.Longitudinal;

/// <summary>
/// Major hull longitudinal load paths only: lower keel spine + ENG thrust spine.
/// Not deck furniture — accommodation floors belong to <see cref="DeckSupportGenerator"/>.
/// </summary>
internal static class PrimaryLongitudinalGenerator
{
    public static void Generate(
        List<CadEntity> entities,
        float loa,
        StructuralTopology topo,
        IReadOnlyList<(string Id, float ZStem, bool BowAligned)> ordinaryFrames,
        IReadOnlyList<(string Id, float ZStem)> bulkheads)
    {
        var floor = topo.DeckUps.Count > 0 ? topo.DeckUps[0] : 0.5f;
        var webH = topo.Web * 0.55f;
        var up = floor - topo.TPlate - webH;
        var joints = LongitudinalRunEmitter.MergeJoints(
            topo.ZStart,
            topo.ZEnd,
            ordinaryFrames.Select(f => f.ZStem),
            bulkheads.Select(b => b.ZStem));

        // True keel: lower hull centerline, full structural length.
        LongitudinalRunEmitter.Emit(
            entities, loa, topo.Web, joints,
            new LongitudinalRunEmitter.RunSpec(
                RunId: "primary-keel",
                AssemblyId: "primary-keel",
                Role: StructuralRoles.LongitudinalGirder,
                Up: up,
                HalfCrossY: MathF.Min(0.14f, topo.Web),
                HalfUp: webH,
                Z0: topo.ZStart,
                Z1: topo.ZEnd,
                YAtStem: _ => 0f,
                Purpose: "Lower hull centerline keel girder"));

        // Propulsion thrust: centerline through ENG between CROSS-AFT and HOLD fore.
        var thrustZ0 = MathF.Max(topo.ZStart, 23f);
        var thrustZ1 = MathF.Min(topo.HoldFore, topo.ZEnd);
        if (thrustZ1 > thrustZ0 + 2f)
        {
            var thrustWeb = topo.Web * 0.65f;
            var thrustUp = floor - topo.TPlate - thrustWeb;
            LongitudinalRunEmitter.Emit(
                entities, loa, topo.Web, joints,
                new LongitudinalRunEmitter.RunSpec(
                    RunId: "thrust-spine",
                    AssemblyId: "thrust-spine",
                    Role: StructuralRoles.ThrustStructure,
                    Up: thrustUp,
                    HalfCrossY: MathF.Min(0.16f, topo.Web * 1.1f),
                    HalfUp: thrustWeb,
                    Z0: thrustZ0,
                    Z1: thrustZ1,
                    YAtStem: _ => 0f,
                    Purpose: "ENG thrust transfer spine (lower CL)"));
        }

        _ = loa;
    }
}
