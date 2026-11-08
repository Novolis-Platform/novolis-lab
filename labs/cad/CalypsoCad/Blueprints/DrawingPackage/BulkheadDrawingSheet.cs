using CalypsoCad.Drawing;
using Novolis.Cad.Primitives;

namespace CalypsoCad.Blueprints.DrawingPackage;

/// <summary>Declarative bulkhead drawing — plate mesh openings are real voids.</summary>
internal static class BulkheadDrawingSheet
{
    public static string Build(
        DrawingEntry entry,
        CadDocument cad,
        string bhId,
        float zStem,
        string note)
    {
        var request = DrawingTemplates.HeavyBulkhead(bhId, zStem, entry.DrawingNumber) with
        {
            Title = entry.Title,
            Label = entry.Label,
        };
        _ = note;
        return UniversalDrawingEngine.Render(request, cad).Html;
    }
}
