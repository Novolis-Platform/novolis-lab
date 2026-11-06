// Raster Norgeskart print PDFs (heightmap + roads) into ArtillerySimulator map PNGs.
//
//   dotnet run --file d:\novolis\novolis-lab\labs\simulation\ArtillerySimulator\tools\import-artillery-norgeskart-pdf.cs -- --heightmap "C:\Users\frank\Downloads\63271497-8460-4ad9-9552-201b4e5ce72e.pdf" --roads "C:\Users\frank\Downloads\5fca740a-3d22-4d30-b66b-0cff7cef09c7.pdf"
//
#:project d:\novolis\novolis-lab\labs\simulation\ArtillerySimulator\tools\InspectNorgeskartPdf\InspectNorgeskartPdf.csproj
#:property TargetFramework=net10.0
#:property PublishAot=false
#:property PackAsTool=false
#:property ImplicitUsings=enable
#:property Nullable=enable

using Novolis.Governance.InspectNorgeskartPdf;
using Novolis.Pdf.Core;
using Novolis.Pdf.Parsing;
using Novolis.Pdf.Rendering;

string? heightmapPdf = null;
string? roadsPdf = null;
var dpi = 220;
for (var i = 0; i < args.Length - 1; i++)
{
    if (args[i] == "--heightmap")
        heightmapPdf = Path.GetFullPath(args[i + 1]);
    if (args[i] == "--roads")
        roadsPdf = Path.GetFullPath(args[i + 1]);
    if (args[i] == "--dpi" && int.TryParse(args[i + 1], out var parsed))
        dpi = parsed;
}

if (heightmapPdf is null && roadsPdf is null)
{
    Console.Error.WriteLine("Specify --heightmap <pdf> and/or --roads <pdf> (optional --dpi 220).");
    return 1;
}

var root = ResolveRoot();
var mapDir = Path.Combine(root, "novolis-lab", "labs", "simulation", "ArtillerySimulator", "map");
Directory.CreateDirectory(mapDir);

if (heightmapPdf is not null)
{
    var outPath = Path.Combine(mapDir, "kristiansand-reference.png");
    await RasterAsync(heightmapPdf, outPath, dpi);
    Console.WriteLine($"Heightmap → {outPath}");
}

if (roadsPdf is not null)
{
    var outPath = Path.Combine(mapDir, "kristiansand-roads-reference.png");
    await RasterAsync(roadsPdf, outPath, dpi);
    Console.WriteLine($"Roads → {outPath}");
}

Console.WriteLine("Next: dotnet run --file d:\\novolis\\novolis-lab\\labs\\simulation\\ArtillerySimulator\\tools\\sync-artillery-map-stack.cs -- ...");
return 0;

static async Task RasterAsync(string pdfPath, string pngOut, int dpi)
{
    if (!File.Exists(pdfPath))
        throw new FileNotFoundException($"PDF not found: {pdfPath}");

    await using var source = PdfSources.OpenFile(pdfPath);
    var document = await PdfParser.ParseAsync(source);
    var pages = PdfPageTree.Resolve(document);
    var plan = PdfContentStreamInterpreter.BuildPlan(document, pages[0]);
    var (png, _, _, _) = NorgeskartTileStitcher.StitchCropped(document, pages[0], plan, dpi);
    await File.WriteAllBytesAsync(pngOut, png);
}

static string ResolveRoot()
{
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir is not null)
    {
        if (Directory.Exists(Path.Combine(dir.FullName, "novolis-lab"))
            && Directory.Exists(Path.Combine(dir.FullName, "novolis-governance")))
            return dir.FullName;
        dir = dir.Parent;
    }

    throw new InvalidOperationException("Could not find novolis platform root (novolis-lab + novolis-governance).");
}
