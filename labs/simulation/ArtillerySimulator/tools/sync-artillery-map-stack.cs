// Build kristiansand-map-stack.json: shared markers, north-up georef, cropped Norgeskart layers.
//
//   dotnet run --file d:\novolis\novolis-lab\labs\simulation\ArtillerySimulator\tools\sync-artillery-map-stack.cs
// (reads ../map/kristiansand-norgeskart-source-pdfs.json when CLI PDFs omitted)
//
//   dotnet run --file d:\novolis\novolis-lab\labs\simulation\ArtillerySimulator\tools\sync-artillery-map-stack.cs -- --heightmap-pdf "C:\...\height.pdf" --roads-pdf "C:\...\roads.pdf"
//
#:project d:\novolis\novolis-lab\labs\simulation\ArtillerySimulator\tools\InspectNorgeskartPdf\InspectNorgeskartPdf.csproj
#:package ProjNet@2.0.0
#:property TargetFramework=net10.0
#:property PublishAot=false
#:property PackAsTool=false
#:property ImplicitUsings=enable
#:property Nullable=enable
#:property ManagePackageVersionsCentrally=false

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Novolis.Governance.InspectNorgeskartPdf;
using Novolis.Pdf.Core;
using Novolis.Pdf.Parsing;
using Novolis.Pdf.Rendering;
using ProjNet.CoordinateSystems;
using ProjNet.CoordinateSystems.Transformations;
using SkiaSharp;

string? heightmapPdf = null;
string? roadsPdf = null;
int? dpiOverride = null;
for (var i = 0; i < args.Length - 1; i++)
{
    if (args[i] == "--heightmap-pdf")
        heightmapPdf = Path.GetFullPath(args[i + 1]);
    if (args[i] == "--roads-pdf")
        roadsPdf = Path.GetFullPath(args[i + 1]);
    if (args[i] == "--dpi" && int.TryParse(args[i + 1], out var parsed))
        dpiOverride = parsed;
}

var root = ResolveRoot();
var mapDir = Path.Combine(root, "novolis-lab", "labs", "simulation", "ArtillerySimulator", "map");
var dpi = dpiOverride ?? 220;

if (heightmapPdf is null || roadsPdf is null)
{
    var configPath = Path.Combine(mapDir, "kristiansand-norgeskart-source-pdfs.json");
    if (File.Exists(configPath)
        && JsonSerializer.Deserialize<NorgeskartSourceConfig>(
            File.ReadAllText(configPath),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) is { } cfg)
    {
        if (heightmapPdf is null && !string.IsNullOrWhiteSpace(cfg.ReliefPdf))
            heightmapPdf = Path.GetFullPath(cfg.ReliefPdf.Trim());
        if (roadsPdf is null && !string.IsNullOrWhiteSpace(cfg.RoadsPdf))
            roadsPdf = Path.GetFullPath(cfg.RoadsPdf.Trim());
        if (dpiOverride is null && cfg.Dpi is > 0)
            dpi = cfg.Dpi;
        if (heightmapPdf is not null || roadsPdf is not null)
            Console.WriteLine($"Using PDF paths from {configPath}");
    }
}
var cutoutPath = Path.Combine(mapDir, "kristiansand-norgeskart-cutout.json");
var stackPath = Path.Combine(mapDir, "kristiansand-map-stack.json");
var cutout = JsonSerializer.Deserialize<CutoutDoc>(
    File.ReadAllText(cutoutPath),
    new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
    ?? throw new InvalidDataException($"Invalid cutout: {cutoutPath}");

var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
var stack = File.Exists(stackPath)
    ? JsonSerializer.Deserialize<MapStackDoc>(File.ReadAllText(stackPath), jsonOptions) ?? NewStack(cutout)
    : NewStack(cutout);
if (stack.World.EastMeters <= 0)
    stack.World = NewStack(cutout).World;

LayerRecord? relief = null;
LayerRecord? roads = null;
if (heightmapPdf is not null)
{
    Console.WriteLine($"relief PDF: {heightmapPdf}");
    relief = await ImportLayerAsync(heightmapPdf, dpi, mapDir, "relief", "kristiansand-reference.png", "land_sea_mask");
    stack.Layers.RemoveAll(l => l.Id == "relief");
    stack.Layers.Add(relief);
}

if (roadsPdf is not null)
{
    Console.WriteLine($"roads PDF: {roadsPdf}");
    roads = await ImportLayerAsync(roadsPdf, dpi, mapDir, "roads", "kristiansand-roads-reference.png", "roads_overlay");
    stack.Layers.RemoveAll(l => l.Id == "roads");
    stack.Layers.Add(roads);
}

if (relief is not null && roads is not null)
{
    if (relief.PixelWidth != roads.PixelWidth || relief.PixelHeight != roads.PixelHeight)
    {
        Console.WriteLine(
            $"WARNING: relief {relief.PixelWidth}×{relief.PixelHeight} vs roads {roads.PixelWidth}×{roads.PixelHeight} — "
            + "PDF map windows differ; resampling roads onto relief pixel grid.");
        roads = await ResampleRoadsOntoReliefAsync(mapDir, relief, roads);
        stack.Layers.RemoveAll(l => l.Id == "roads");
        stack.Layers.Add(roads);
        Console.WriteLine($"roads aligned: {roads.PixelWidth}×{roads.PixelHeight} px");
    }
    else
        Console.WriteLine("Relief and roads layers share pixel grid (stack-aligned).");
}

ApplyWorldFrameFromRelief(stack, relief ?? stack.Layers.FirstOrDefault(l => l.Id == "relief"));
WriteStack(stackPath, stack);
Console.WriteLine($"Wrote {stackPath}");
Console.WriteLine(
    "Run extract: dotnet run --file d:\\novolis\\novolis-lab\\labs\\simulation\\ArtillerySimulator\\tools\\extract-artillery-map.cs -- --scenario kristiansand");
return 0;

static MapStackDoc NewStack(CutoutDoc cutout) =>
    new()
    {
        Schema = "novolis.artillery.map-stack/1",
        Citation = "Aligned map layers for Defense of Kristiansand. North is up. Edit markers when survey anchors are known.",
        World = new WorldFrame
        {
            NorthUp = true,
            Origin = "southwest_corner_meters",
            Wgs84 = cutout.Wgs84,
            EastMeters = cutout.EastMeters,
            NorthMeters = cutout.NorthMeters,
            ExtentSource = "cutout-json-provisional",
        },
        Markers = DefaultMarkers(cutout),
    };

static List<MarkerRecord> DefaultMarkers(CutoutDoc cutout) =>
    [
        new() { Id = "nw", Label = "North-west corner", Wgs84 = new Wgs84PointJson { Lat = cutout.Wgs84.North, Lon = cutout.Wgs84.West } },
        new() { Id = "ne", Label = "North-east corner", Wgs84 = new Wgs84PointJson { Lat = cutout.Wgs84.North, Lon = cutout.Wgs84.East } },
        new() { Id = "sw", Label = "South-west corner", Wgs84 = new Wgs84PointJson { Lat = cutout.Wgs84.South, Lon = cutout.Wgs84.West } },
        new() { Id = "se", Label = "South-east corner", Wgs84 = new Wgs84PointJson { Lat = cutout.Wgs84.South, Lon = cutout.Wgs84.East } },
    ];

static void ApplyWorldFrameFromRelief(MapStackDoc stack, LayerRecord? relief)
{
    if (relief is null)
        return;

    var aspect = relief.PixelWidth / (double)relief.PixelHeight;
    var east = stack.World.EastMeters;
    var north = east / aspect;
    stack.World.NorthMeters = north;
    stack.World.ExtentSource = "relief-crop-aspect-matched-east";
    stack.World.Wgs84 = FitWgs84Aspect(stack.World.Wgs84, aspect);
    Console.WriteLine(
        $"World frame: {stack.World.EastMeters:F1} m × {stack.World.NorthMeters:F1} m (aspect {aspect:F4}, north-up)");
}

static Wgs84BoxJson FitWgs84Aspect(Wgs84BoxJson box, double eastOverNorth)
{
    var centerLat = (box.South + box.North) / 2.0;
    var centerLon = (box.West + box.East) / 2.0;
    var latSpan = box.North - box.South;
    var lonSpan = box.East - box.West;
    var currentAspect = lonSpan / latSpan;
    if (currentAspect > eastOverNorth)
    {
        var newLatSpan = lonSpan / eastOverNorth;
        return new Wgs84BoxJson
        {
            South = centerLat - newLatSpan / 2,
            North = centerLat + newLatSpan / 2,
            West = box.West,
            East = box.East,
        };
    }

    var newLonSpan = latSpan * eastOverNorth;
    return new Wgs84BoxJson
    {
        South = box.South,
        North = box.North,
        West = centerLon - newLonSpan / 2,
        East = centerLon + newLonSpan / 2,
    };
}

static async Task<LayerRecord> ImportLayerAsync(
    string pdfPath,
    int dpi,
    string mapDir,
    string id,
    string imageFile,
    string role)
{
    await using var source = PdfSources.OpenFile(pdfPath);
    var document = await PdfParser.ParseAsync(source);
    var pages = PdfPageTree.Resolve(document);
    var plan = PdfContentStreamInterpreter.BuildPlan(document, pages[0]);
    var (png, framePdf, pw, ph) = NorgeskartTileStitcher.StitchCropped(document, pages[0], plan, dpi);
    var outPath = Path.Combine(mapDir, imageFile);
    await File.WriteAllBytesAsync(outPath, png);
    Console.WriteLine($"{id}: {pw}×{ph} px (PDF map frame {framePdf.Width:F1}×{framePdf.Height:F1} pt) → {outPath}");

    return new LayerRecord
    {
        Id = id,
        Role = role,
        ImageFile = imageFile,
        SourcePdf = Path.GetFileName(pdfPath),
        SourcePdfPath = pdfPath,
        Dpi = dpi,
        PixelWidth = pw,
        PixelHeight = ph,
        NorthUp = true,
        PixelOrigin = "topLeftNorthWest",
        PdfMapFrame = new PdfFrameRecord(framePdf.Left, framePdf.Bottom, framePdf.Right, framePdf.Top),
        MarkerPixels = CornerPixels(pw, ph),
    };
}

static async Task<LayerRecord> ResampleRoadsOntoReliefAsync(string mapDir, LayerRecord relief, LayerRecord roads)
{
    var roadsPath = Path.Combine(mapDir, roads.ImageFile);
    using var src = SKBitmap.Decode(roadsPath)
        ?? throw new InvalidDataException($"Could not decode roads raster: {roadsPath}");
    using var dest = new SKBitmap(relief.PixelWidth, relief.PixelHeight, SKColorType.Rgba8888, SKAlphaType.Premul);
    using (var canvas = new SKCanvas(dest))
    {
        canvas.Clear(SKColors.White);
        canvas.DrawBitmap(
            src,
            new SKRect(0, 0, relief.PixelWidth, relief.PixelHeight),
            new SKSamplingOptions(SKFilterMode.Linear));
    }

    using var encoded = SKImage.FromBitmap(dest).Encode(SKEncodedImageFormat.Png, 92);
    await File.WriteAllBytesAsync(roadsPath, encoded.ToArray());

    return new LayerRecord
    {
        Id = roads.Id,
        Role = roads.Role,
        ImageFile = roads.ImageFile,
        SourcePdf = roads.SourcePdf,
        SourcePdfPath = roads.SourcePdfPath,
        Dpi = roads.Dpi,
        PixelWidth = relief.PixelWidth,
        PixelHeight = relief.PixelHeight,
        NorthUp = roads.NorthUp,
        PixelOrigin = roads.PixelOrigin,
        PdfMapFrame = roads.PdfMapFrame,
        MarkerPixels = CornerPixels(relief.PixelWidth, relief.PixelHeight),
    };
}

static Dictionary<string, PixelPointJson> CornerPixels(int width, int height) =>
    new(StringComparer.Ordinal)
    {
        ["nw"] = new PixelPointJson { X = 0, Y = 0 },
        ["ne"] = new PixelPointJson { X = width - 1, Y = 0 },
        ["sw"] = new PixelPointJson { X = 0, Y = height - 1 },
        ["se"] = new PixelPointJson { X = width - 1, Y = height - 1 },
    };

static void WriteStack(string path, MapStackDoc stack)
{
    var options = new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    File.WriteAllText(path, JsonSerializer.Serialize(stack, options));
}

static string ResolveRoot()
{
    var env = Environment.GetEnvironmentVariable("NOVOLIS_ROOT");
    if (!string.IsNullOrWhiteSpace(env))
        return env;
    return @"d:\novolis";
}

sealed class CutoutDoc
{
    [JsonPropertyName("wgs84")]
    public Wgs84BoxJson Wgs84 { get; set; } = new();

    public double EastMeters =>
        (Wgs84.East - Wgs84.West)
        * 111_320.0
        * Math.Cos((Wgs84.South + Wgs84.North) / 2.0 * Math.PI / 180.0);

    public double NorthMeters => (Wgs84.North - Wgs84.South) * 111_132.0;
}

sealed class MapStackDoc
{
    public string Schema { get; set; } = "novolis.artillery.map-stack/1";
    public string Citation { get; set; } = "";
    public WorldFrame World { get; set; } = new();
    public List<MarkerRecord> Markers { get; set; } = [];
    public List<LayerRecord> Layers { get; set; } = [];
}

sealed class WorldFrame
{
    public bool NorthUp { get; set; } = true;
    public string Origin { get; set; } = "southwest_corner_meters";
    public Wgs84BoxJson Wgs84 { get; set; } = new();
    public double EastMeters { get; set; }
    public double NorthMeters { get; set; }
    public string ExtentSource { get; set; } = "";
}

sealed class MarkerRecord
{
    public string Id { get; set; } = "";
    public string Label { get; set; } = "";
    public Wgs84PointJson Wgs84 { get; set; } = new();
    public string? Notes { get; set; }
}

sealed class LayerRecord
{
    public string Id { get; set; } = "";
    public string Role { get; set; } = "";
    public string ImageFile { get; set; } = "";
    public string? SourcePdf { get; set; }
    /// <summary>Absolute path last used by sync (audit).</summary>
    public string? SourcePdfPath { get; set; }
    public int Dpi { get; set; }
    public int PixelWidth { get; set; }
    public int PixelHeight { get; set; }
    public bool NorthUp { get; set; } = true;
    public string PixelOrigin { get; set; } = "topLeftNorthWest";
    public PdfFrameRecord? PdfMapFrame { get; set; }
    public Dictionary<string, PixelPointJson> MarkerPixels { get; set; } = new();
}

sealed record PdfFrameRecord(double Left, double Bottom, double Right, double Top);

sealed class Wgs84BoxJson
{
    public double South { get; set; }
    public double North { get; set; }
    public double West { get; set; }
    public double East { get; set; }
}

sealed class Wgs84PointJson
{
    public double Lat { get; set; }
    public double Lon { get; set; }
}

sealed class PixelPointJson
{
    public int X { get; set; }
    public int Y { get; set; }
}

sealed class NorgeskartSourceConfig
{
    [JsonPropertyName("reliefPdf")]
    public string? ReliefPdf { get; set; }

    [JsonPropertyName("roadsPdf")]
    public string? RoadsPdf { get; set; }

    [JsonPropertyName("dpi")]
    public int Dpi { get; set; }

    [JsonPropertyName("citation")]
    public string? Citation { get; set; }
}
