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

// Soft-mask DeviceGray companions for the 1183x1604 tiles (obj 31, 35)
foreach (var num in new[] { 20, 24, 31, 35, 42, 46 })
{
    var stream = (PdfStreamObject)doc.Objects[num].Value!;
    stream.Dictionary.TryGetInteger(PdfNames.Width, out var w);
    stream.Dictionary.TryGetInteger(PdfNames.Height, out var h);
    var samples = PdfStreamDecoder.Decode(stream, PdfLimits.Default);
    long z = 0, mid = 0, hi = 0;
    foreach (var b in samples)
    {
        if (b == 0) z++;
        else if (b < 128) mid++;
        else hi++;
    }
    Console.WriteLine($"SMask obj {num}: {w}x{h} bytes={samples.Length} zero={z} mid={mid} hi={hi} pctZero={100.0*z/samples.Length:F1}%");
}

// Paint ONLY landscape shared tiles (2363-wide), skip 1183 portrait
var page = PdfPageTree.Resolve(doc)[0];
var plan = PdfContentStreamInterpreter.BuildPlan(doc, page);
var roads = @"C:\Users\frank\Downloads\5fca740a-3d22-4d30-b66b-0cff7cef09c7.pdf";
await using var rsrc = PdfSources.OpenFile(roads);
var rdoc = await PdfParser.ParseAsync(rsrc);
var rpage = PdfPageTree.Resolve(rdoc)[0];
var rplan = PdfContentStreamInterpreter.BuildPlan(rdoc, rpage);
var frame = PdfSharedImageFrame.FromMatchingImages(plan, rplan);
Console.WriteLine($"shared frame {frame.Width:F1}x{frame.Height:F1}");

// Count how many image commands are portrait vs landscape
var port = 0; var land = 0;
foreach (var c in plan.Commands)
{
    if (c is not PdfImageCommand img || img.IsImageMask) continue;
    if (img.Width < img.Height) port++; else land++;
}
Console.WriteLine($"relief images landscape={land} portrait={port}");
