// Diagnose embedded images in the configured relief/roads PDFs.
#:project d:\novolis\novolis-lab\labs\simulation\ArtillerySimulator\scripts\MapPipeline.Support\MapPipeline.Support.csproj
#:property TargetFramework=net10.0
#:property PublishAot=false
#:property ImplicitUsings=enable
#:property Nullable=enable
#:property ManagePackageVersionsCentrally=false

using System.Text.Json;
using Novolis.Pdf.Core;
using Novolis.Pdf.Parsing;
using Novolis.Pdf.Rendering;
using Novolis.Pdf.Rendering.Skia;
using SkiaSharp;

var mapDir = @"d:\novolis\novolis-lab\labs\simulation\ArtillerySimulator\map";
var cfg = JsonSerializer.Deserialize<Cfg>(
    File.ReadAllText(Path.Combine(mapDir, "kristiansand-norgeskart-source-pdfs.json")),
    new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

await DumpAsync("RELIEF", cfg.ReliefPdf!);
await DumpAsync("ROADS", cfg.RoadsPdf!);
await ProbePaintAsync(cfg.ReliefPdf!);
return 0;

static async Task DumpAsync(string label, string pdfPath)
{
    Console.WriteLine($"=== {label}: {pdfPath}");
    await using var src = PdfSources.OpenFile(pdfPath);
    var doc = await PdfParser.ParseAsync(src);
    var page = PdfPageTree.Resolve(doc)[0];
    var plan = PdfContentStreamInterpreter.BuildPlan(doc, page);
    var i = 0;
    foreach (var cmd in plan.Commands)
    {
        if (cmd is not PdfImageCommand img)
            continue;
        using var bmp = PdfSkiaImageDecoder.Decode(img);
        long r = 0, g = 0, b = 0, n = 0;
        var cyanish = 0;
        var grayish = 0;
        for (var y = 0; y < bmp.Height; y += 8)
        for (var x = 0; x < bmp.Width; x += 8)
        {
            var c = bmp.GetPixel(x, y);
            r += c.Red; g += c.Green; b += c.Blue; n++;
            if (c.Blue > c.Red + 15 && c.Blue > c.Green + 5 && c.Green > 100)
                cyanish++;
            var spread = Math.Max(c.Red, Math.Max(c.Green, c.Blue)) - Math.Min(c.Red, Math.Min(c.Green, c.Blue));
            if (spread < 8)
                grayish++;
        }

        Console.WriteLine(
            $"  [{i}] {img.Width}x{img.Height} fmt={img.PixelFormat} mask={img.IsImageMask} " +
            $"ctm=[{img.Matrix.A:F1},{img.Matrix.D:F1} @ {img.Matrix.E:F1},{img.Matrix.F:F1}] " +
            $"avgRGB=({r / n},{g / n},{b / n}) cyanish={cyanish}/{n} grayish={grayish}/{n}");
        i++;
    }

    Console.WriteLine($"  total images={i} commands={plan.Commands.Count}");
}

static async Task ProbePaintAsync(string pdfPath)
{
    await using var src = PdfSources.OpenFile(pdfPath);
    var doc = await PdfParser.ParseAsync(src);
    var page = PdfPageTree.Resolve(doc)[0];
    foreach (var flags in new[]
             {
                 PdfPaintFlags.ImagesOnly,
                 PdfPaintFlags.Images | PdfPaintFlags.Paths | PdfPaintFlags.Clips,
                 PdfPaintFlags.All,
             })
    {
        var result = PdfMapPageRasterizer.RasterCropped(doc, page, 72, flags);
        using var bmp = SKBitmap.Decode(result.PngBytes)!;
        var cyanish = 0;
        var n = 0;
        for (var y = 0; y < bmp.Height; y += 4)
        for (var x = 0; x < bmp.Width; x += 4)
        {
            var c = bmp.GetPixel(x, y);
            n++;
            if (c.Blue > c.Red + 15 && c.Blue > c.Green + 5 && c.Green > 100)
                cyanish++;
        }

        var outPath = Path.Combine(
            @"d:\novolis\novolis-lab\labs\simulation\ArtillerySimulator\map",
            $"_probe-{flags}.png");
        await File.WriteAllBytesAsync(outPath, result.PngBytes);
        Console.WriteLine($"PAINT {flags}: {result.PixelWidth}x{result.PixelHeight} cyanish={cyanish}/{n} → {outPath}");
    }
}

sealed class Cfg
{
    public string? ReliefPdf { get; set; }
    public string? RoadsPdf { get; set; }
}
