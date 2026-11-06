using Novolis.Pdf.Abstractions;
using Novolis.Pdf.Parsing;
using Novolis.Pdf.Rendering;
using Novolis.Pdf.Rendering.Skia;
using SkiaSharp;

namespace Novolis.Governance.InspectNorgeskartPdf;

/// <summary>Rasters a Norgeskart print PDF and crops to map content (north-up, row 0 = north).</summary>
public static class NorgeskartTileStitcher
{
    public static (byte[] Png, NorgeskartMapContentFrame FramePdf, int PixelWidth, int PixelHeight) StitchCropped(
        PdfParsedDocument document,
        PdfResolvedPage page,
        PdfPageRenderPlan plan,
        double dpi)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(plan);

        var frame = NorgeskartMapContentFrame.FromPlan(plan);
        var scale = dpi / 72d;
        var cropLeft = frame.Left * scale;
        var cropTop = (page.Info.DisplayHeight - frame.Top) * scale;
        var cropWidth = frame.Width * scale;
        var cropHeight = frame.Height * scale;
        var pixelWidth = System.Math.Clamp((int)System.Math.Round(cropWidth), 1, 16_384);
        var pixelHeight = System.Math.Clamp((int)System.Math.Round(cropHeight), 1, 16_384);

        using var full = RasterFullPage(document, page, plan, dpi);
        using var cropped = new SKBitmap(pixelWidth, pixelHeight, SKColorType.Rgba8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(cropped))
        {
            canvas.Clear(SKColors.White);
            // SKRect is (left, top, right, bottom) — not (left, top, width, height).
            var srcLeft = (float)cropLeft;
            var srcTop = (float)cropTop;
            var src = new SKRect(srcLeft, srcTop, srcLeft + (float)cropWidth, srcTop + (float)cropHeight);
            var dest = new SKRect(0, 0, pixelWidth, pixelHeight);
            canvas.DrawBitmap(full, src, dest, new SKSamplingOptions(SKFilterMode.Linear));
        }

        // PdfViewportTransform.Raster already maps PDF y-up → device y-down with row 0 = north.
        using var encoded = SKImage.FromBitmap(cropped).Encode(SKEncodedImageFormat.Png, 92);
        return (encoded.ToArray(), frame, pixelWidth, pixelHeight);
    }

    /// <summary>Full-page north-up raster (no content crop).</summary>
    internal static byte[] Stitch(PdfParsedDocument document, PdfResolvedPage page, PdfPageRenderPlan plan, double dpi)
    {
        using var full = RasterFullPage(document, page, plan, dpi);
        using var encoded = SKImage.FromBitmap(full).Encode(SKEncodedImageFormat.Png, 92);
        return encoded.ToArray();
    }

    private static SKBitmap RasterFullPage(
        PdfParsedDocument document,
        PdfResolvedPage page,
        PdfPageRenderPlan plan,
        double dpi)
    {
        var info = page.Info;
        var scale = dpi / 72d;
        var width = System.Math.Clamp((int)System.Math.Ceiling(info.DisplayWidth * scale), 1, 16_384);
        var height = System.Math.Clamp((int)System.Math.Ceiling(info.DisplayHeight * scale), 1, 16_384);
        var view = PdfViewportTransform.Raster(info, dpi);
        var bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        new PdfSkiaPageRenderer().Paint(canvas, document, page, plan, view);
        return bitmap;
    }
}
