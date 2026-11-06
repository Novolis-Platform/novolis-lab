using Novolis.Governance.InspectNorgeskartPdf;
using Novolis.Pdf.Abstractions;
using Novolis.Pdf.Core;
using Novolis.Pdf.Parsing;
using Novolis.Pdf.Rendering;
using Novolis.Pdf.Rendering.Skia;
using Novolis.Pdf.Text;

if (args.Length == 1 && args[0] == "--utm-probe")
{
    UtmProbe.Print();
    return 0;
}

if (args.Length == 2 && args[0] == "--dump-spans")
{
    var spanPdf = Path.GetFullPath(args[1]);
    await using var spanSource = PdfSources.OpenFile(spanPdf);
    var spanDoc = await PdfParser.ParseAsync(spanSource);
    var outPath = Path.ChangeExtension(spanPdf, ".spans.txt");
    NorgeskartPdfSpanDump.Dump(spanDoc, outPath);
    return 0;
}

if (args.Length < 2)
{
    Console.Error.WriteLine("Usage: InspectNorgeskartPdf <pdf> <png-out>");
    return 1;
}

var pdfPath = Path.GetFullPath(args[0]);
var pngOut = Path.GetFullPath(args[1]);

await using var source = PdfSources.OpenFile(pdfPath);
var document = await PdfParser.ParseAsync(source);
var text = string.Concat(new PdfTextExtractor().ExtractDocument(document).Select(s => s.Text));
Console.WriteLine(text);

var dpi = 300;
if (args.Length > 2 && int.TryParse(args[2], out var parsed))
    dpi = parsed;
var pages = PdfPageTree.Resolve(document);
var plan = PdfContentStreamInterpreter.BuildPlan(document, pages[0]);
Console.WriteLine($"Commands: {plan.Commands.Count}, images: {plan.Commands.OfType<PdfImageCommand>().Count()}");
foreach (var img in plan.Commands.OfType<PdfImageCommand>())
{
    var m = img.Matrix;
    Console.WriteLine(
        $"  {img.Width}x{img.Height} fmt={img.PixelFormat} ctm=[{m.A:F3},{m.B:F3},{m.C:F3},{m.D:F3},{m.E:F1},{m.F:F1}]");
}

var dumpDir = Path.Combine(Path.GetDirectoryName(pngOut)!, "pdf-images");
await PdfImageDumper.DumpAsync(document, dumpDir);

var renderer = new PdfSkiaPageRenderer();
var page = pages[0];
var scale = dpi / 72d;
var pixelWidth = (int)Math.Ceiling(page.Info.DisplayWidth * scale);
var pixelHeight = (int)Math.Ceiling(page.Info.DisplayHeight * scale);
Console.WriteLine($"Page {pixelWidth}×{pixelHeight} px @ {dpi} DPI");
if (args.Contains("--tiles-only"))
{
    var composite = NorgeskartImageComposite.RenderImageTiles(plan, page.Info, dpi);
    await File.WriteAllBytesAsync(pngOut, composite);
    Console.WriteLine($"PNG {pngOut} ({composite.Length} bytes, image-tile composite @ {dpi} DPI)");
    return 0;
}

var stitched = NorgeskartTileStitcher.Stitch(document, page, plan, dpi);
await File.WriteAllBytesAsync(pngOut, stitched);
Console.WriteLine($"PNG {pngOut} ({stitched.Length} bytes, stitched tiles @ {dpi} DPI)");
return 0;
