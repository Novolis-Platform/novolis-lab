#:property TargetFramework=net10.0
#:property ManagePackageVersionsCentrally=false
#:property ImplicitUsings=enable
#:property Nullable=enable
#:project d:\novolis\novolis-lab\labs\simulation\ArtillerySimulator\scripts\MapPipeline.Support\MapPipeline.Support.csproj

using System.Security.Cryptography;
using Novolis.Pdf.Core;
using Novolis.Pdf.Parsing;
using Novolis.Pdf.Rendering;

var relief = @"C:\Users\frank\Downloads\63271497-8460-4ad9-9552-201b4e5ce72e.pdf";
await using var src = PdfSources.OpenFile(relief);
var doc = await PdfParser.ParseAsync(src);
var page = PdfPageTree.Resolve(doc)[0];
var plan = PdfContentStreamInterpreter.BuildPlan(doc, page);
Console.WriteLine($"diagnostics: {plan.Diagnostics.Count}");
foreach (var d in plan.Diagnostics.Take(30))
    Console.WriteLine($"  {d.Code} {d.Message}");

var i = 0;
foreach (var c in plan.Commands)
{
    if (c is not PdfImageCommand img) continue;
    var encLen = img.EncodedImage?.Length ?? 0;
    var sampLen = img.Samples?.Length ?? 0;
    var expectedRgb = img.Width * img.Height * 3L;
    var expectedGray = img.Width * (long)img.Height;
    var payload = encLen > 0 ? img.EncodedImage! : img.Samples!;
    var hash16 = Convert.ToHexString(SHA256.HashData(payload)).Substring(0, 16);
    Console.WriteLine($"[{i}] {img.Width}x{img.Height} fmt={img.PixelFormat} bpc={img.BitsPerComponent} mask={img.IsImageMask} enc={encLen} samples={sampLen} expectGray={expectedGray} expectRgb={expectedRgb} ratioG={(sampLen>0?(double)sampLen/expectedGray:0):F3} ratioR={(sampLen>0?(double)sampLen/expectedRgb:0):F3} hash16={hash16}");
    Console.WriteLine($"    head: {Convert.ToHexString(payload.AsSpan(0, Math.Min(32, payload.Length)))}");
    i++;
}

string Describe(PdfObject? o)
{
    o = doc.Resolve(o);
    return o switch
    {
        null => "null",
        PdfNameObject n => "/" + n.Value,
        PdfArrayObject a => "[" + string.Join(" ", a.Items.Select(Describe)) + "]",
        PdfDictionaryObject => "<<dict>>",
        PdfStreamObject s => $"<<stream enc={s.EncodedBytes.Length}>>",
        PdfNumberObject n => n.Value.ToString(),
        _ => o.GetType().Name,
    };
}

Console.WriteLine("--- Image XObjects ---");
foreach (var (num, ind) in doc.Objects.OrderBy(kv => kv.Key))
{
    if (ind.Value is not PdfStreamObject stream && doc.Resolve(ind.Value) is not PdfStreamObject)
        continue;
    stream = ind.Value as PdfStreamObject ?? (PdfStreamObject)doc.Resolve(ind.Value)!;
    var st = stream.Dictionary.Get<PdfNameObject>(PdfNames.Subtype)?.Value;
    if (st != "Image") continue;
    stream.Dictionary.TryGetInteger(PdfNames.Width, out var w);
    stream.Dictionary.TryGetInteger(PdfNames.Height, out var h);
    if (w is not (1183 or 2363)) continue;
    var filter = stream.Dictionary.Get(PdfNames.Filter);
    var cs = stream.Dictionary.Get(PdfNames.ColorSpace);
    var bpc = stream.Dictionary.TryGetInteger(PdfNames.BitsPerComponent, out var bv) ? bv : -1;
    var smask = stream.Dictionary.Get(new PdfName("SMask"));
    var dp = stream.Dictionary.Get(PdfNames.DecodeParms);
    Console.WriteLine($"obj {num}: {w}x{h} Filter={Describe(filter)} CS={Describe(cs)} BPC={bpc} SMask={Describe(smask)} DecodeParms={Describe(dp)} Enc={stream.EncodedBytes.Length}");
}
