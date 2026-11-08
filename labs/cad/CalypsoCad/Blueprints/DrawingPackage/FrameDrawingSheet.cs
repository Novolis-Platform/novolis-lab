using System.Globalization;
using CalypsoCad.Drawing;
using Novolis.Cad.Primitives;

namespace CalypsoCad.Blueprints.DrawingPackage;

/// <summary>Declarative frame drawing — transverse projection of actual CAD geometry.</summary>
internal static class FrameDrawingSheet
{
    public static string Build(DrawingEntry entry, CadDocument cad, float zStem, string? frameId = null)
    {
        frameId ??= $"frame-F{zStem.ToString("0", CultureInfo.InvariantCulture)}";
        var request = DrawingTemplates.RingFrame(frameId, zStem, entry.DrawingNumber) with
        {
            Title = entry.Title,
            Label = entry.Label,
        };
        return UniversalDrawingEngine.Render(request, cad).Html;
    }
}
