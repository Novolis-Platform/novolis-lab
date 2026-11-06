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
var plan = PdfContentStreamInterpreter.BuildPlan(doc, page);
var frame = PdfMapContentFrame.FromImageCommands(plan);
// Paint ALL images (incl. portrait hoydedata tiles)
var result = PdfMapPageRasterizer.RasterCropped(doc, page, 120, frame, PdfPaintFlags.ImagesOnly, imageContentKeys: null);
var outDir = @"d:\novolis\novolis-lab\labs\simulation\ArtillerySimulator\map\layers\_probe";
Directory.CreateDirectory(outDir);
await File.WriteAllBytesAsync(Path.Combine(outDir, "relief-all-images.png"), result.PngBytes);
using var bmp = SKBitmap.Decode(result.PngBytes)!;
// sample water-ish pixels
long cyan=0, gray=0, other=0;
for (int y=0;y<bmp.Height;y+=4)
for (int x=0;x<bmp.Width;x+=4)
{
  var p=bmp.GetPixel(x,y);
  var max=Math.Max(p.Red,Math.Max(p.Green,p.Blue));
  var min=Math.Min(p.Red,Math.Min(p.Green,p.Blue));
  var sat = max==0?0:(max-min)/(double)max;
  var isCyan = p.Blue > 180 && p.Green > 180 && p.Blue >= p.Red + 20 && p.Green >= p.Red + 10;
  var isGray = sat < 0.08 && max > 20 && max < 250;
  if (isCyan) cyan++; else if (isGray) gray++; else other++;
}
Console.WriteLine($"size={bmp.Width}x{bmp.Height} cyan~{cyan} gray~{gray} other~{other}");
