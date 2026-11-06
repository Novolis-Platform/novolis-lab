#:property TargetFramework=net10.0
#:property ManagePackageVersionsCentrally=false
#:property ImplicitUsings=enable
#:property Nullable=enable
#:project d:\novolis\novolis-lab\labs\simulation\ArtillerySimulator\scripts\MapPipeline.Support\MapPipeline.Support.csproj

using Novolis.Pdf.Core;
using Novolis.Pdf.Parsing;
using Novolis.Pdf.Rendering;
using Novolis.Pdf.Rendering.Skia;
using SkiaSharp;

var relief = @"C:\Users\frank\Downloads\63271497-8460-4ad9-9552-201b4e5ce72e.pdf";
await using var src = PdfSources.OpenFile(relief);
var doc = await PdfParser.ParseAsync(src);
var page = PdfPageTree.Resolve(doc)[0];
var info = page.Info;
Console.WriteLine($"Media/Display: {info.DisplayWidth:F2} x {info.DisplayHeight:F2}");
var plan = PdfContentStreamInterpreter.BuildPlan(doc, page);
var i = 0;
foreach (var c in plan.Commands)
{
    if (c is not PdfImageCommand img || img.IsImageMask) continue;
    var m = img.Matrix;
    Console.WriteLine($"img[{i}] {img.Width}x{img.Height} rect=({m.E:F1},{m.F:F1})-({m.E+m.A:F1},{m.F+m.D:F1}) A={m.A:F1} D={m.D:F1}");
    i++;
}

var dpi = 72d;
var result = PdfMapPageRasterizer.RasterCropped(doc, page, dpi, PdfPaintFlags.ImagesOnly);
using var bmp = SKBitmap.Decode(result.PngBytes)!;
Console.WriteLine($"crop {result.PixelWidth}x{result.PixelHeight} frame={result.ContentFrame.Left:F1},{result.ContentFrame.Bottom:F1}-{result.ContentFrame.Right:F1},{result.ContentFrame.Top:F1}");

// Find non-near-black bounding box
int minX=bmp.Width, minY=bmp.Height, maxX=0, maxY=0;
long nonBlack=0;
for (int y=0;y<bmp.Height;y++)
for (int x=0;x<bmp.Width;x++)
{
    var p = bmp.GetPixel(x,y);
    if (p.Red < 8 && p.Green < 8 && p.Blue < 8) continue;
    nonBlack++;
    if (x<minX) minX=x; if (y<minY) minY=y; if (x>maxX) maxX=x; if (y>maxY) maxY=y;
}
Console.WriteLine($"nonBlack={nonBlack} bbox=({minX},{minY})-({maxX},{maxY})");

// Also paint ImagesOnly WITHOUT portrait tiles conceptually: check if painting only landscape sizes
