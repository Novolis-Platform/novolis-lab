using CalypsoCad.Structure;
using Novolis.Cad.Primitives;

namespace CalypsoCad.Generation.Longitudinal;

/// <summary>Phase-3 scaffold — wraps existing girder/stringer emitters.</summary>
internal static class LongitudinalStructureGenerator
{
    public static void Generate(
        List<CadEntity> entities,
        float loa,
        StructuralTopology topo,
        IReadOnlyList<(string Id, float ZStem, bool BowAligned)> ordinaryFrames,
        ShipGeometryContext context)
    {
        var halfDepthZ = topo.Web * 0.5f;
        CalypsoShipbuildingFrames.AddDeckLongitudinalGirders(
            entities, loa, topo.ZStart, topo.ZEnd, topo.Web, topo.FrameBay, topo.DeckUps, topo.TPlate,
            topo.OpenAtriumSpans);
        CalypsoShipbuildingFrames.AddOuterHullStringers(
            entities, loa, topo.ZStart, topo.ZEnd, topo.Web, topo.FrameBay, ordinaryFrames);
        Func<float, bool> inHold = context.Constraints.InCargoClearance;
        CalypsoShipbuildingFrames.AddFrameGirderJoinStubs(
            entities, loa, ordinaryFrames, topo.Web, halfDepthZ, topo.DeckUps, topo.TPlate,
            inHold, topo.OpenAtriumSpans);
    }
}
