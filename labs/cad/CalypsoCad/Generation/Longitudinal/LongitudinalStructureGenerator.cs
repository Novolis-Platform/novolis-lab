using CalypsoCad.Geometry;
using CalypsoCad.Structure;
using Novolis.Cad.Primitives;
using CadDocument = Novolis.Cad.Primitives.CadDocument;

namespace CalypsoCad.Generation.Longitudinal;

/// <summary>
/// Hull knuckle stringers + primary longitudinal load paths.
/// Deck-edge and cargo-floor supports are owned by deck/cargo generators.
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
        var bh = bulkheadStations ?? [];

        // Primary keel + ENG thrust (CadSystem.Longitudinal).
        PrimaryLongitudinalGenerator.Generate(entities, loa, topo, ordinaryFrames, bh);

        // Cargo-floor edge supports under Deck -1 / HOLD (owned as deck-dkm1).
        CargoFloorSupportGenerator.Generate(entities, loa, topo, ordinaryFrames, bh);

        // Eight continuous knuckle paths → swept segments between structural joints.
        var jointZs = HullKnucklePath.MergeJointStations(
            loa,
            ordinaryFrames.Select(f => f.ZStem),
            bh.Select(b => b.ZStem));
        var paths = HullKnucklePath.Build(
            context.Hull, CalypsoShipbuildingFrames.PlatingJoinInset, jointZs);
        var knuckle = HullKnuckleStringerGenerator.Generate(paths, loa, topo.Web, context, entities);
        foreach (var d in knuckle.Diagnostics)
            Console.Error.WriteLine($"  {d}");

        // Path-swept shell plates on the same joint stations as knuckle paths.
        CalypsoShipbuildingFrames.AddOuterHullShellPlating(entities, loa, topo.Web, jointZs);

        var knuckleReport = HullKnucklePathValidation.Evaluate(
            new CadDocument { Entities = entities }, context.Hull, CalypsoShipbuildingFrames.PlatingJoinInset);
        LastKnucklePathReport = knuckleReport;
        foreach (var fail in knuckleReport.Segments.Where(s => !s.Passed).Take(8))
            Console.Error.WriteLine(
                $"  FAIL knuckle path {fail.PartId}: deviation={fail.DeviationM:0.###}m @z={fail.MidZ:0.##}");

        // No AddDeckLongitudinalGirders / AddFrameGirderJoinStubs — purpose-owned generators only.
    }
}
