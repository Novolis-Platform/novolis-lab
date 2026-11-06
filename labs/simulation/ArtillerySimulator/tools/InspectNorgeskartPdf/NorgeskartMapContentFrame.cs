using Novolis.Pdf.Abstractions;
using Novolis.Pdf.Rendering;

namespace Novolis.Governance.InspectNorgeskartPdf;

/// <summary>Map drawable extent inside a Norgeskart print PDF (PDF user space, y-up, north-up).</summary>
public readonly record struct NorgeskartMapContentFrame(
    double Left,
    double Bottom,
    double Right,
    double Top)
{
    public double Width => Right - Left;

    public double Height => Top - Bottom;

    public static NorgeskartMapContentFrame FromPlan(PdfPageRenderPlan plan)
    {
        var minX = double.MaxValue;
        var minY = double.MaxValue;
        var maxX = double.MinValue;
        var maxY = double.MinValue;
        foreach (var command in plan.Commands)
        {
            if (command is not PdfImageCommand image)
                continue;

            var m = image.Matrix;
            minX = System.Math.Min(minX, m.E);
            minY = System.Math.Min(minY, m.F);
            maxX = System.Math.Max(maxX, m.E + m.A);
            maxY = System.Math.Max(maxY, m.F + m.D);
        }

        if (minX >= maxX || minY >= maxY)
            throw new InvalidDataException("Could not infer map content frame from PDF image tiles.");

        return new NorgeskartMapContentFrame(minX, minY, maxX, maxY);
    }
}
