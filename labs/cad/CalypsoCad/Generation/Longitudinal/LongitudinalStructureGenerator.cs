using CalypsoCad.Geometry;
using CalypsoCad.Structure;
using Novolis.Cad.Primitives;
using CadDocument = Novolis.Cad.Primitives.CadDocument;

namespace CalypsoCad.Generation.Longitudinal;

/// <summary>
/// Whole-ship longitudinal structure: deck girders, continuous hull-knuckle stringers,
/// frame×girder join stubs. Knuckle stringers are path-driven (not mid-bay boxes).
/// </summary>
internal static class LongitudinalStructureGenerator
{
    /// <summary>Last knuckle path deviation report from the most recent generate.</summary>
    public static HullKnucklePathValidation.Report? LastKnucklePathReport { get; private set; }

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

        // Path-swept shell plates on the same joint stations as knuckle paths.
        CalypsoShipbuildingFrames.AddOuterHullShellPlating(entities, loa, topo.Web, jointZs);

        // Independent loft re-sample gate (written under docs/renders on headless / acceptance).
        var knuckleReport = HullKnucklePathValidation.Evaluate(
            new CadDocument { Entities = entities }, context.Hull, CalypsoShipbuildingFrames.PlatingJoinInset);
        LastKnucklePathReport = knuckleReport;
        foreach (var fail in knuckleReport.Segments.Where(s => !s.Passed).Take(8))
            Console.Error.WriteLine(
                $"  FAIL knuckle path {fail.PartId}: deviation={fail.DeviationM:0.###}m @z={fail.MidZ:0.##}");

        Func<float, bool> inHold = context.Constraints.InCargoClearance;
        CalypsoShipbuildingFrames.AddFrameGirderJoinStubs(
            entities, loa, ordinaryFrames, topo.Web, halfDepthZ, topo.DeckUps, topo.TPlate,
            inHold, topo.OpenAtriumSpans);
    }
}
