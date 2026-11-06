using Novolis.Pdf.Abstractions;
using Novolis.Pdf.Rendering;
using Novolis.Pdf.Rendering.Skia;
using SkiaSharp;

namespace Novolis.Governance.InspectNorgeskartPdf;

internal static class NorgeskartImageComposite
{
    internal static byte[] RenderImageTiles(PdfPageRenderPlan plan, PdfPageInfo page, double dpi)
    {
        var view = PdfViewportTransform.Raster(page, dpi);
        var width = System.Math.Clamp((int)System.Math.Ceiling(page.DisplayWidth * dpi / 72d), 1, 16_384);
        var height = System.Math.Clamp((int)System.Math.Ceiling(page.DisplayHeight * dpi / 72d), 1, 16_384);
        using var bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.White);
            canvas.SetMatrix(ToSkia(view));
            foreach (var command in plan.Commands)
            {
                if (command is not PdfImageCommand image)
                    continue;

                using var tile = PdfSkiaImageDecoder.Decode(image);
                canvas.Save();
                var pageView = FromSkia(canvas.TotalMatrix);
                canvas.SetMatrix(ToSkia(pageView.Multiply(image.Matrix)));
                // Unit square: CTM maps image space to page points.
                canvas.DrawBitmap(tile, new SKRect(0, 0, 1, 1), new SKSamplingOptions(SKFilterMode.Linear));
                canvas.Restore();
            }
        }

        using var encoded = SKImage.FromBitmap(bitmap).Encode(SKEncodedImageFormat.Png, 92);
        return encoded.ToArray();
    }

    private static PdfMatrix FromSkia(SKMatrix matrix) =>
        new(matrix.ScaleX, matrix.SkewY, matrix.SkewX, matrix.ScaleY, matrix.TransX, matrix.TransY);

    private static SKMatrix ToSkia(PdfMatrix matrix) =>
        new()
        {
            ScaleX = (float)matrix.A,
            SkewX = (float)matrix.C,
            TransX = (float)matrix.E,
            SkewY = (float)matrix.B,
            ScaleY = (float)matrix.D,
            TransY = (float)matrix.F,
            Persp0 = 0,
            Persp1 = 0,
            Persp2 = 1,
        };
}
