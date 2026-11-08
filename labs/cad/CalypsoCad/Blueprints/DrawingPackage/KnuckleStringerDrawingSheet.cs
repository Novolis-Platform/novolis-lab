using CalypsoCad.Drawing;
using Novolis.Cad.Primitives;

namespace CalypsoCad.Blueprints.DrawingPackage;

/// <summary>Declarative knuckle drawing — geometry from hierarchy via <see cref="UniversalDrawingEngine"/>.</summary>
internal static class KnuckleStringerDrawingSheet
{
    public static string Build(DrawingEntry entry, CadDocument cad, int vertex)
    {
        var subject = $"knuckle-v{vertex}";
        var request = DrawingTemplates.KnuckleStringer(subject, entry.DrawingNumber) with
        {
            Title = entry.Title,
            Label = entry.Label,
        };
        return UniversalDrawingEngine.Render(request, cad).Html;
    }
}
