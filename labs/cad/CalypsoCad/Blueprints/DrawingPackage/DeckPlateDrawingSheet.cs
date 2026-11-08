using CalypsoCad.Drawing;
using Novolis.Cad.Primitives;

namespace CalypsoCad.Blueprints.DrawingPackage;

/// <summary>Declarative deck drawing — panels from ownership under the deck assembly.</summary>
internal static class DeckPlateDrawingSheet
{
    public static string Build(DrawingEntry entry, CadDocument cad, int deckNum, string deckLabel)
    {
        var subject = deckNum switch
        {
            -1 => "deck-dkm1",
            1 => "deck-dk1",
            _ => "deck-dk0",
        };
        // Prefer hierarchy org node when present.
        var hierarchyId = deckNum switch
        {
            -1 => Model.ShipHierarchyIds.DeckM1,
            1 => Model.ShipHierarchyIds.DeckP1,
            _ => Model.ShipHierarchyIds.Deck0,
        };

        var request = DrawingTemplates.DeckPlan(hierarchyId, entry.DrawingNumber) with
        {
            Title = entry.Title,
            Label = deckLabel,
        };
        var result = UniversalDrawingEngine.Render(request, cad);
        if (result.Resolved.HasDrawableGeometry)
            return result.Html;

        // Fallback to CAD assembly id if org node has no direct entity ownership yet.
        return UniversalDrawingEngine.Render(
            request with { SubjectId = subject },
            cad).Html;
    }
}
