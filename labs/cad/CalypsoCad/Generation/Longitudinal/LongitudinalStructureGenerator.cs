using CalypsoCad.Geometry;
using CalypsoCad.Structure;
using Novolis.Cad.Primitives;

namespace CalypsoCad.Generation.Longitudinal;

/// <summary>
/// Whole-ship longitudinal structure: deck girders, continuous hull-knuckle stringers,
/// frame×girder join stubs. Knuckle stringers are path-driven (not mid-bay boxes).
/// </summary>
internal static class LongitudinalStructureGenerator
{
    public static void Generate(
        List<CadEntity> entities,
        float loa,
        StructuralTopology topo,
        IReadOnlyList<(string Id, float ZStem, bool BowAligned)> ordinaryFrames,
        ShipGeometryContext context,
        IReadOnlyList<(string Id, float ZStem)>? bulkheadStations = null)
    {
        var halfDepthZ = topo.Web * 0.5f;
        CalypsoShipbuildingFrames.AddDeckLongitudinalGirders(
            entities, loa, topo.ZStart, topo.ZEnd, topo.Web, topo.FrameBay, topo.DeckUps, topo.TPlate,
            topo.OpenAtriumSpans);

        // Eight continuous knuckle paths → swept segments between structural joints.
        var jointZs = HullKnucklePath.MergeJointStations(
            loa,
            ordinaryFrames.Select(f => f.ZStem),
            (bulkheadStations ?? []).Select(b => b.ZStem));
        var paths = HullKnucklePath.Build(
            context.Hull, CalypsoShipbuildingFrames.PlatingJoinInset, jointZs);
        var knuckle = HullKnuckleStringerGenerator.Generate(paths, loa, topo.Web, context, entities);
        foreach (var d in knuckle.Diagnostics)
            Console.Error.WriteLine($"  {d}");

        // Shell plating strips still bay-scoped (P2 — path-swept plates later).
        CalypsoShipbuildingFrames.AddOuterHullShellPlating(
            entities, loa, topo.Web, ordinaryFrames);

        Func<float, bool> inHold = context.Constraints.InCargoClearance;
        CalypsoShipbuildingFrames.AddFrameGirderJoinStubs(
            entities, loa, ordinaryFrames, topo.Web, halfDepthZ, topo.DeckUps, topo.TPlate,
            inHold, topo.OpenAtriumSpans);
    }
}
