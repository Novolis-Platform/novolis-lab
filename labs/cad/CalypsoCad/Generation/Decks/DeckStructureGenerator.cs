using CalypsoCad.Structure;
using Novolis.Cad.Primitives;

namespace CalypsoCad.Generation.Decks;

/// <summary>Phase-3 scaffold — wraps cellular sandwich deck emitters.</summary>
internal static class DeckStructureGenerator
{
    public static void Generate(
        List<CadEntity> entities,
        float loa,
        StructuralTopology topo,
        IReadOnlyList<(string Id, float ZStem, bool BowAligned)> ordinaryFrames,
        IReadOnlyList<CalypsoBulkheads.Station> bulkheads,
        ShipGeometryContext context)
    {
        _ = context;
        var bhStations = bulkheads.Select(b => (b.Id, b.ZStem)).ToList();
        // Deck-edge longitudinal supports before sandwich panels (load path → panel).
        DeckSupportGenerator.Generate(entities, loa, topo, ordinaryFrames, bhStations);
        CalypsoShipbuildingFrames.AddCellularSandwichDecks(
            entities, loa, topo.ZStart, topo.ZEnd, topo.FrameBay, topo.SandwichDecks, topo.TPlate,
            topo.OpenAtriumSpans, topo.Web, ordinaryFrames, bhStations);
    }
}
