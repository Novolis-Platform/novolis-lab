using CalypsoCad.Structure;
using Novolis.Cad.Primitives;

namespace CalypsoCad.Generation.Longitudinal;

/// <summary>
/// HOLD cargo-floor edge supports only. Keel through the hold is owned by
/// <see cref="PrimaryLongitudinalGenerator"/> — do not duplicate it here.
/// Full-height cargo clearance above the floor remains clear.
/// </summary>
internal static class CargoFloorSupportGenerator
{
    public static void Generate(
        List<CadEntity> entities,
        float loa,
        StructuralTopology topo,
        IReadOnlyList<(string Id, float ZStem, bool BowAligned)> ordinaryFrames,
        IReadOnlyList<(string Id, float ZStem)> bulkheads)
    {
        var z0 = MathF.Max(topo.HoldFore, topo.ZStart);
        var z1 = MathF.Min(topo.HoldAft - topo.HoldDoorBand, topo.ZEnd);
        if (z1 <= z0 + 1f)
            return;

        var floor = topo.DeckUps.Count > 0 ? topo.DeckUps[0] : 0.5f;
        var webH = topo.Web * 0.55f;
        var up = floor - topo.TPlate - webH;
        var inset = CalypsoShipbuildingFrames.DefaultSkinInset + 0.28f;
        var joints = LongitudinalRunEmitter.MergeJoints(
            z0, z1,
            ordinaryFrames.Select(f => f.ZStem),
            bulkheads.Select(b => b.ZStem));

        float EdgeY(float z, float sign)
        {
            var halfB = CalypsoLockGenerator.HalfBeamInsidePublic(z, up, inset);
            var y = halfB - topo.Web * 0.85f;
            return y < 0.35f ? float.NaN : sign * y;
        }

        LongitudinalRunEmitter.Emit(
            entities, loa, topo.Web, joints,
            new LongitudinalRunEmitter.RunSpec(
                RunId: "cargo-floor-port",
                AssemblyId: "deck-dkm1",
                Role: StructuralRoles.LongitudinalGirder,
                Up: up,
                HalfCrossY: topo.Web * 0.4f,
                HalfUp: webH,
                Z0: z0,
                Z1: z1,
                YAtStem: z => EdgeY(z, -1f),
                Purpose: "HOLD cargo-floor port edge support"));

        LongitudinalRunEmitter.Emit(
            entities, loa, topo.Web, joints,
            new LongitudinalRunEmitter.RunSpec(
                RunId: "cargo-floor-stbd",
                AssemblyId: "deck-dkm1",
                Role: StructuralRoles.LongitudinalGirder,
                Up: up,
                HalfCrossY: topo.Web * 0.4f,
                HalfUp: webH,
                Z0: z0,
                Z1: z1,
                YAtStem: z => EdgeY(z, +1f),
                Purpose: "HOLD cargo-floor starboard edge support"));
    }
}
