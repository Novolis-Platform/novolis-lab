using Novolis.Pdf.Abstractions;
using Novolis.Pdf.Parsing;
using Novolis.Pdf.Rendering;
using Novolis.Pdf.Rendering.Skia;
using SkiaSharp;

namespace Novolis.Governance.InspectNorgeskartPdf;

internal static class PdfImageDumper
{
    public static async Task DumpAsync(PdfParsedDocument document, string dumpDir, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(dumpDir);
        var pages = PdfPageTree.Resolve(document);
        var index = 0;
        foreach (var page in pages)
        {
            var plan = PdfContentStreamInterpreter.BuildPlan(document, page);
            foreach (var image in plan.Commands.OfType<PdfImageCommand>())
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var bitmap = Decode(image);
                if (bitmap is null)
                    continue;

                using var encoded = bitmap.Encode(SKEncodedImageFormat.Png, 100);
                if (encoded is null)
                    continue;

                var path = Path.Combine(
                    dumpDir,
                    $"p{page.Info.Index:D2}-img{index:D2}-{bitmap.Width}x{bitmap.Height}.png");
                await File.WriteAllBytesAsync(path, encoded.ToArray(), cancellationToken);
                Console.WriteLine(path);
                index++;
            }
        }
    }

    private static SKBitmap? Decode(PdfImageCommand command)
    {
        try
        {
            return PdfSkiaImageDecoder.Decode(command);
        }
        catch
        {
            return null;
        }
    }
}
