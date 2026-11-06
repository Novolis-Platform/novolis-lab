// Kristiansand map pipeline (WorkflowEngine.Pipeline): two Norgeskart PDFs → layers → data → composite → mesh.
//
//   dotnet run --file d:\novolis\novolis-lab\labs\simulation\ArtillerySimulator\scripts\build-kristiansand-map.cs -p:NovolisUseProjectReferences=true
//
#:project d:\novolis\novolis-lab\labs\simulation\ArtillerySimulator\scripts\MapPipeline.Support\MapPipeline.Support.csproj
#:property TargetFramework=net10.0
#:property PublishAot=false
#:property PackAsTool=false
#:property ImplicitUsings=enable
#:property Nullable=enable
#:property ManagePackageVersionsCentrally=false

using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Novolis.Pdf.Core;
using Novolis.Pdf.Parsing;
using Novolis.Pdf.Rendering;
using Novolis.Pdf.Rendering.Skia;
using Novolis.Pdf.Text;
using Novolis.WorkflowEngine.Pipeline;
using SkiaSharp;
using PipelineFactory = Novolis.WorkflowEngine.Pipeline.Pipeline;

var appDir = ResolveAppDir();
var mapDir = Path.Combine(appDir, "map");
var layersDir = Path.Combine(mapDir, "layers");
var configPath = Path.Combine(mapDir, "kristiansand-norgeskart-source-pdfs.json");
var cutoutPath = Path.Combine(mapDir, "kristiansand-norgeskart-cutout.json");

var pipeline = PipelineFactory
    .Start<MapBuildInput>()
    .Then(LoadConfig)
    .Then(state => new ValueTask<MapBuildState>(ExtractPdfImagesAsync(state)))
    .Then(state => new ValueTask<MapLayerBundle>(RasterFourLayersAsync(state)))
    .Then(BuildLayerData)
    .Then(CompositeLayers)
    .Then(WriteMeshAndGameOutputs);

var result = await pipeline.ExecuteResultAsync(new MapBuildInput(appDir, mapDir, layersDir, configPath, cutoutPath));
if (!result.IsSuccess)
{
    Console.Error.WriteLine(result.Error?.Message ?? "pipeline failed");
    return 1;
}

Console.WriteLine(result.Value);
return 0;

static MapBuildState LoadConfig(MapBuildInput input)
{
    if (!File.Exists(input.ConfigPath))
        throw new FileNotFoundException("Missing source PDF config.", input.ConfigPath);

    var cfg = JsonSerializer.Deserialize<SourcePdfConfig>(
        File.ReadAllText(input.ConfigPath),
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
        ?? throw new InvalidDataException(input.ConfigPath);

    if (string.IsNullOrWhiteSpace(cfg.ReliefPdf) || string.IsNullOrWhiteSpace(cfg.RoadsPdf))
        throw new InvalidDataException("reliefPdf and roadsPdf are required.");

    var relief = Path.GetFullPath(cfg.ReliefPdf.Trim());
    var roads = Path.GetFullPath(cfg.RoadsPdf.Trim());
    if (!File.Exists(relief))
        throw new FileNotFoundException("Relief PDF not found.", relief);
    if (!File.Exists(roads))
        throw new FileNotFoundException("Roads PDF not found.", roads);

    var dpi = cfg.Dpi is > 0 ? cfg.Dpi.Value : 220;
    var cutout = File.Exists(input.CutoutPath)
        ? JsonSerializer.Deserialize<CutoutDoc>(File.ReadAllText(input.CutoutPath), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
        : null;

    Console.WriteLine($"Relief PDF: {relief}");
    Console.WriteLine($"  SHA256: {Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(relief)))}");
    Console.WriteLine($"Roads PDF:  {roads}");
    Console.WriteLine($"  SHA256: {Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(roads)))}");
    Console.WriteLine($"DPI: {dpi}");
    Console.WriteLine("Source: these two PDF files only (no DEM/OSM/NDH in this pipeline).");
    return new MapBuildState(input, relief, roads, dpi, cutout);
}

static async Task<MapBuildState> ExtractPdfImagesAsync(MapBuildState state)
{
    // No raw tile dumps — only derived transparent masks/layers are written under layers/.
    return state;
}

static async Task<MapLayerBundle> RasterFourLayersAsync(MapBuildState state)
{
    // Wipe previous layer outputs so leftover wrong raw-* / placenames junk cannot linger.
    if (Directory.Exists(state.Input.LayersDir))
        Directory.Delete(state.Input.LayersDir, recursive: true);
    Directory.CreateDirectory(state.Input.LayersDir);

    await using var reliefSrc = PdfSources.OpenFile(state.ReliefPdf);
    var reliefDoc = await PdfParser.ParseAsync(reliefSrc);
    var reliefPage = PdfPageTree.Resolve(reliefDoc)[0];
    var reliefPlan = PdfContentStreamInterpreter.BuildPlan(reliefDoc, reliefPage);

    await using var roadsSrc = PdfSources.OpenFile(state.RoadsPdf);
    var roadsDoc = await PdfParser.ParseAsync(roadsSrc);
    var roadsPage = PdfPageTree.Resolve(roadsDoc)[0];
    var roadsPlan = PdfContentStreamInterpreter.BuildPlan(roadsDoc, roadsPage);

    // Crop both PDFs to the same map rectangle (shared strip placements).
    var sharedFrame = PdfSharedImageFrame.FromMatchingImages(reliefPlan, roadsPlan);
    Console.WriteLine(
        $"Map frame: {sharedFrame.Width:F2}×{sharedFrame.Height:F2} pt " +
        $"({sharedFrame.Left:F2},{sharedFrame.Bottom:F2})–({sharedFrame.Right:F2},{sharedFrame.Top:F2})");

    // Relief PDF: paint EVERY image (hoydedata hillshade + cyan water), not only shared topo strips.
    var reliefRaster = PdfMapPageRasterizer.RasterCropped(
        reliefDoc, reliefPage, state.Dpi, sharedFrame, PdfPaintFlags.ImagesOnly, imageContentKeys: null);
    var reliefSourcePath = Path.Combine(state.Input.LayersDir, "relief-source.png");
    await File.WriteAllBytesAsync(reliefSourcePath, reliefRaster.PngBytes);
    Console.WriteLine($"relief-source: {reliefRaster.PixelWidth}×{reliefRaster.PixelHeight} from relief PDF (all images)");

    var waterPath = Path.Combine(state.Input.LayersDir, "water-mask.png");
    var landPath = Path.Combine(state.Input.LayersDir, "land-mask.png");
    var elevationPath = Path.Combine(state.Input.LayersDir, "elevation.png");
    SplitReliefMasks(reliefRaster.PngBytes, waterPath, landPath, elevationPath);
    Console.WriteLine($"water-mask / land-mask / elevation (transparent where N/A) → {state.Input.LayersDir}");

    // Roads PDF on the same pixel grid (shared frame). Keep red roads; transparent elsewhere.
    var roadsRaster = PdfMapPageRasterizer.RasterCropped(
        roadsDoc, roadsPage, state.Dpi, sharedFrame, PdfPaintFlags.ImagesOnly, imageContentKeys: null);
    if (reliefRaster.PixelWidth != roadsRaster.PixelWidth || reliefRaster.PixelHeight != roadsRaster.PixelHeight)
        throw new InvalidDataException(
            $"Frame raster size mismatch: relief {reliefRaster.PixelWidth}×{reliefRaster.PixelHeight} vs roads {roadsRaster.PixelWidth}×{roadsRaster.PixelHeight}.");

    var roadsPath = Path.Combine(state.Input.LayersDir, "roads.png");
    WriteRoadsTransparent(roadsRaster.PngBytes, roadsPath);
    Console.WriteLine($"roads: red ink on transparent → {roadsPath}");

    var georef = BuildGeoref(state, reliefRaster);
    var georefPath = Path.Combine(state.Input.LayersDir, "georef.json");
    await File.WriteAllTextAsync(
        georefPath,
        JsonSerializer.Serialize(georef, new JsonSerializerOptions { WriteIndented = true }));

    return new MapLayerBundle(
        state,
        reliefSourcePath,
        elevationPath,
        waterPath,
        landPath,
        roadsPath,
        georefPath,
        reliefRaster.PixelWidth,
        reliefRaster.PixelHeight,
        georef);
}

static MapDataBundle BuildLayerData(MapLayerBundle layers)
{
    using var elevation = SKBitmap.Decode(layers.ElevationPath)
        ?? throw new InvalidDataException(layers.ElevationPath);
    using var shoreline = SKBitmap.Decode(layers.ShorelinePath)
        ?? throw new InvalidDataException(layers.ShorelinePath);

    var w = layers.Width;
    var h = layers.Height;
    var heights = new short[w * h];
    var water = new byte[w * h];
    short minH = short.MaxValue;
    short maxH = short.MinValue;
    for (var y = 0; y < h; y++)
    {
        for (var x = 0; x < w; x++)
        {
            var i = y * w + x;
            var c = elevation.GetPixel(x, y);
            // North-up image: row 0 = north. Luminance → relative elevation (hillshade proxy).
            var lum = (short)((c.Red * 30 + c.Green * 59 + c.Blue * 11) / 100);
            heights[i] = lum;
            if (lum < minH) minH = lum;
            if (lum > maxH) maxH = lum;
            water[i] = shoreline.GetPixel(x, y).Alpha > 0 ? (byte)1 : (byte)0;
        }
    }

    var heightsPath = Path.Combine(layers.State.Input.LayersDir, "elevation.bin");
    var waterPath = Path.Combine(layers.State.Input.LayersDir, "shoreline.bin");
    WriteInt16Grid(heightsPath, w, h, heights);
    File.WriteAllBytes(waterPath, water);
    Console.WriteLine($"data: elevation.bin + shoreline.bin ({w}×{h}, height {minH}–{maxH})");
    return new MapDataBundle(layers, heightsPath, waterPath, heights, water, minH, maxH);
}

static MapDataBundle CompositeLayers(MapDataBundle data)
{
    var layers = data.Layers;
    using var elevation = SKBitmap.Decode(layers.ElevationPath)!;
    using var shoreline = SKBitmap.Decode(layers.ShorelinePath)!;
    using var roads = SKBitmap.Decode(layers.RoadsPath)!;
    using var placenames = SKBitmap.Decode(layers.PlacenamesPath)!;

    using var composite = new SKBitmap(layers.Width, layers.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
    using (var canvas = new SKCanvas(composite))
    {
        canvas.Clear(SKColors.White);
        var sample = new SKSamplingOptions(SKFilterMode.Linear);
        // Bottom → top: elevation, shoreline tint, roads, placenames.
        canvas.DrawBitmap(elevation, 0, 0, sample);
        using (var paint = new SKPaint { Color = new SKColor(81, 130, 255, 160), BlendMode = SKBlendMode.SrcOver })
            canvas.DrawBitmap(shoreline, 0, 0, sample, paint);
        using (var paint = new SKPaint { ColorFilter = SKColorFilter.CreateBlendMode(new SKColor(255, 40, 40, 180), SKBlendMode.SrcIn) })
            canvas.DrawBitmap(roads, 0, 0, sample, paint);
        canvas.DrawBitmap(placenames, 0, 0, sample);
    }

    var outPath = Path.Combine(layers.State.Input.MapDir, "kristiansand-layered-composite.png");
    using var encoded = SKImage.FromBitmap(composite).Encode(SKEncodedImageFormat.Png, 92);
    File.WriteAllBytes(outPath, encoded.ToArray());

    // Also publish primary game-facing reference as elevation (PDF-exclusive images-only).
    File.Copy(layers.ElevationPath, Path.Combine(layers.State.Input.MapDir, "kristiansand-reference.png"), overwrite: true);
    File.Copy(layers.RoadsPath, Path.Combine(layers.State.Input.MapDir, "kristiansand-roads-reference.png"), overwrite: true);
    File.Copy(layers.ShorelinePath, Path.Combine(layers.State.Input.MapDir, "kristiansand-land-sea-mask.png"), overwrite: true);
    Console.WriteLine($"composite: {outPath}");
    return data;
}

static string WriteMeshAndGameOutputs(MapDataBundle data)
{
    var mapDir = data.Layers.State.Input.MapDir;
    var gameDir = Path.Combine(data.Layers.State.Input.AppDir, "Game");
    Directory.CreateDirectory(gameDir);

    var g = data.Layers.Georef;
    var east = g.EastMeters;
    var north = g.NorthMeters;
    var grid = 81;
    var meters = ResampleHeights(data.Heights, data.Layers.Width, data.Layers.Height, grid);

    // Land/sea mask binary for the game (KLSM).
    WriteLandSeaMaskBinary(
        Path.Combine(mapDir, "kristiansand-land-sea-mask.bin"),
        data.Water,
        data.Layers.Width,
        data.Layers.Height,
        (float)east,
        (float)north);

    WriteElevationCs(gameDir, meters, grid);
    WriteStackJson(mapDir, data);

    return $"OK — layers in {data.Layers.State.Input.LayersDir}; mesh grid {grid}×{grid}; game sources updated under {gameDir}";
}

static LayerGeoref BuildGeoref(MapBuildState state, PdfMapRasterResult elevation)
{
    var aspect = elevation.PixelWidth / (double)elevation.PixelHeight;
    double east;
    double north;
    double south;
    double northLat;
    double west;
    double eastLon;
    if (state.Cutout?.Wgs84 is { } box)
    {
        south = box.South;
        northLat = box.North;
        west = box.West;
        eastLon = box.East;
        // Match relief aspect to meters: keep east from cutout, derive north.
        east = (eastLon - west) * 111_320.0 * Math.Cos((south + northLat) / 2.0 * Math.PI / 180.0);
        north = east / aspect;
        var latSpan = north / 111_132.0;
        var center = (south + northLat) / 2.0;
        south = center - latSpan / 2.0;
        northLat = center + latSpan / 2.0;
    }
    else
    {
        east = elevation.PixelWidth;
        north = elevation.PixelHeight;
        south = northLat = west = eastLon = 0;
    }

    return new LayerGeoref(
        elevation.PixelWidth,
        elevation.PixelHeight,
        elevation.Dpi,
        true,
        "topLeftNorthWest",
        east,
        north,
        south,
        northLat,
        west,
        eastLon,
        elevation.FramePdf.Left,
        elevation.FramePdf.Bottom,
        elevation.FramePdf.Right,
        elevation.FramePdf.Top,
        state.ReliefPdf,
        state.RoadsPdf);
}

static void WriteShorelineMask(byte[] elevationPng, string outPath)
{
    using var src = SKBitmap.Decode(elevationPng) ?? throw new InvalidDataException("elevation png");
    using var mask = new SKBitmap(src.Width, src.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
    for (var y = 0; y < src.Height; y++)
    {
        for (var x = 0; x < src.Width; x++)
        {
            var c = src.GetPixel(x, y);
            var water = IsWater(c.Red, c.Green, c.Blue);
            mask.SetPixel(x, y, water ? new SKColor(81, 130, 255, 255) : new SKColor(0, 0, 0, 0));
        }
    }

    using var encoded = SKImage.FromBitmap(mask).Encode(SKEncodedImageFormat.Png, 92);
    File.WriteAllBytes(outPath, encoded.ToArray());
}

static bool IsWater(byte r, byte g, byte b)
{
    if (r + g + b < 36)
        return true;
    if (b >= 155 && b > r + 18 && b > g + 4 && g >= 120)
        return true;
    if (b >= 248 && r <= 95 && g is >= 110 and <= 155)
        return true;
    var dr = r - 0x51;
    var dg = g - 0x82;
    var db = b - 0xFF;
    if (dr * dr + dg * dg + db * db <= 40 * 40)
        return true;
    var spread = Math.Max(r, Math.Max(g, b)) - Math.Min(r, Math.Min(g, b));
    if (spread < 5)
    {
        var avg = (r + g + b) / 3;
        if (avg is >= 148 and <= 200)
            return true;
    }

    return false;
}

static void WritePlacenamesLayer(
    PdfParsedDocument document,
    PdfResolvedPage page,
    int width,
    int height,
    PdfMapContentFrame frame,
    string pngPath,
    string jsonPath)
{
    var spans = new PdfTextExtractor().ExtractPage(document, page.Info.Index);
    var labels = new List<object>();
    using var bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
    using (var canvas = new SKCanvas(bitmap))
    {
        canvas.Clear(SKColors.Transparent);
        using var paint = new SKPaint { Color = new SKColor(20, 20, 20, 220), IsAntialias = true };
        using var font = new SKFont { Size = 14 };
        foreach (var span in spans)
        {
            var text = span.Text?.Trim();
            if (string.IsNullOrWhiteSpace(text) || text.Length < 2)
                continue;
            // Map PDF y-up bounds into cropped north-up pixels.
            var u = (span.Bounds.Left - frame.Left) / frame.Width;
            var vFromBottom = (span.Bounds.Bottom - frame.Bottom) / frame.Height;
            var px = (float)(u * (width - 1));
            var py = (float)((1.0 - vFromBottom) * (height - 1));
            if (px < 0 || py < 0 || px >= width || py >= height)
                continue;
            canvas.DrawText(text, px, py, SKTextAlign.Left, font, paint);
            labels.Add(new { text, x = px, y = py, pdf = new { span.Bounds.Left, span.Bounds.Bottom, span.Bounds.Right, span.Bounds.Top } });
        }
    }

    using var encoded = SKImage.FromBitmap(bitmap).Encode(SKEncodedImageFormat.Png, 92);
    File.WriteAllBytes(pngPath, encoded.ToArray());
    File.WriteAllText(jsonPath, JsonSerializer.Serialize(labels, new JsonSerializerOptions { WriteIndented = true }));
}

static async Task<int> ExportSharedImagesAsync(
    PdfParsedDocument document,
    PdfResolvedPage page,
    PdfPageRenderPlan plan,
    IReadOnlySet<string> sharedKeys,
    string outputDirectory)
{
    Directory.CreateDirectory(outputDirectory);
    var index = 0;
    foreach (var command in plan.Commands)
    {
        if (command is not PdfImageCommand image || image.IsImageMask)
            continue;
        if (!sharedKeys.Contains(PdfSharedImageFrame.PlacementKey(image)))
            continue;

        using var bitmap = PdfSkiaImageDecoder.Decode(image);
        using var encoded = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        var path = Path.Combine(
            outputDirectory,
            $"p{page.Info.Index:D2}-img{index:D2}-{image.Width}x{image.Height}.png");
        await File.WriteAllBytesAsync(path, encoded.ToArray()).ConfigureAwait(false);
        index++;
    }

    return index;
}

static void WriteInt16Grid(string path, int w, int h, short[] values)
{
    using var stream = File.Create(path);
    using var writer = new BinaryWriter(stream);
    writer.Write(w);
    writer.Write(h);
    foreach (var v in values)
        writer.Write(v);
}

static void WriteLandSeaMaskBinary(string path, byte[] water, int w, int h, float east, float north)
{
    var ppm = (w - 1) / east;
    using var stream = File.Create(path);
    using var writer = new BinaryWriter(stream);
    writer.Write(0x4D534C4Bu);
    writer.Write((ushort)1);
    writer.Write((ushort)w);
    writer.Write((ushort)h);
    writer.Write(ppm);
    writer.Write(0f);
    writer.Write(0f);
    writer.Write(east);
    writer.Write(north);
    writer.Write(water);
}

static short[] ResampleHeights(short[] src, int sw, int sh, int grid)
{
    var dest = new short[grid * grid];
    for (var j = 0; j < grid; j++)
    {
        var v = j / (double)(grid - 1);
        var sy = (1.0 - v) * (sh - 1); // j=0 south in game meters → image bottom
        for (var i = 0; i < grid; i++)
        {
            var u = i / (double)(grid - 1);
            var sx = u * (sw - 1);
            dest[j * grid + i] = SampleBilinear(src, sw, sh, sx, sy);
        }
    }

    return dest;
}

static short SampleBilinear(short[] src, int w, int h, double x, double y)
{
    var x0 = (int)Math.Floor(x);
    var y0 = (int)Math.Floor(y);
    var x1 = Math.Min(x0 + 1, w - 1);
    var y1 = Math.Min(y0 + 1, h - 1);
    var tx = x - x0;
    var ty = y - y0;
    var a = src[y0 * w + x0];
    var b = src[y0 * w + x1];
    var c = src[y1 * w + x0];
    var d = src[y1 * w + x1];
    var top = a + (b - a) * tx;
    var bot = c + (d - c) * tx;
    return (short)Math.Round(top + (bot - top) * ty);
}

static void WriteElevationCs(string gameDir, short[] meters, int grid)
{
    var path = Path.Combine(gameDir, "KristiansandElevation.cs");
    using var writer = new StreamWriter(path);
    writer.WriteLine("// <auto-generated />");
    writer.WriteLine("namespace ArtillerySimulator.Game;");
    writer.WriteLine();
    writer.WriteLine("internal static class KristiansandElevation");
    writer.WriteLine("{");
    writer.WriteLine($"    public const int Count = {grid};");
    writer.WriteLine("    public static readonly short[] Meters =");
    writer.WriteLine("    [");
    for (var i = 0; i < meters.Length; i++)
    {
        if (i % 16 == 0)
            writer.Write("        ");
        writer.Write(meters[i]);
        writer.Write(i + 1 == meters.Length ? "\n" : ", ");
        if (i % 16 == 15)
            writer.WriteLine();
    }

    writer.WriteLine("    ];");
    writer.WriteLine("}");
}

static void WriteStackJson(string mapDir, MapDataBundle data)
{
    var g = data.Layers.Georef;
    var doc = new
    {
        schema = "novolis.artillery.map-stack/2",
        citation = "Built only from relief+roads Norgeskart PDFs via scripts/build-kristiansand-map.cs",
        world = new
        {
            northUp = true,
            origin = "southwest_corner_meters",
            eastMeters = g.EastMeters,
            northMeters = g.NorthMeters,
            wgs84 = new { south = g.SouthLat, north = g.NorthLat, west = g.WestLon, east = g.EastLon },
            extentSource = "relief-images-only-aspect",
        },
        layers = new object[]
        {
            new { id = "elevation", imageFile = "layers/elevation.png", role = "elevation", pixelWidth = g.PixelWidth, pixelHeight = g.PixelHeight, northUp = true, pixelOrigin = "topLeftNorthWest", sourcePdfPath = g.ReliefPdf },
            new { id = "shoreline", imageFile = "layers/shoreline.png", role = "shoreline", pixelWidth = g.PixelWidth, pixelHeight = g.PixelHeight, northUp = true, pixelOrigin = "topLeftNorthWest", sourcePdfPath = g.ReliefPdf },
            new { id = "roads", imageFile = "layers/roads.png", role = "roads", pixelWidth = g.PixelWidth, pixelHeight = g.PixelHeight, northUp = true, pixelOrigin = "topLeftNorthWest", sourcePdfPath = g.RoadsPdf },
            new { id = "placenames", imageFile = "layers/placenames.png", role = "placenames", pixelWidth = g.PixelWidth, pixelHeight = g.PixelHeight, northUp = true, pixelOrigin = "topLeftNorthWest", sourcePdfPath = g.RoadsPdf },
        },
    };
    File.WriteAllText(
        Path.Combine(mapDir, "kristiansand-map-stack.json"),
        JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = true }));
}

static string ResolveAppDir()
{
    var env = Environment.GetEnvironmentVariable("NOVOLIS_ROOT");
    var root = string.IsNullOrWhiteSpace(env) ? @"d:\novolis" : env;
    return Path.Combine(root, "novolis-lab", "labs", "simulation", "ArtillerySimulator");
}

sealed record MapBuildInput(string AppDir, string MapDir, string LayersDir, string ConfigPath, string CutoutPath);

sealed record MapBuildState(MapBuildInput Input, string ReliefPdf, string RoadsPdf, int Dpi, CutoutDoc? Cutout);

sealed record MapLayerBundle(
    MapBuildState State,
    string ElevationPath,
    string ShorelinePath,
    string RoadsPath,
    string PlacenamesPath,
    string PlacenamesJsonPath,
    string GeorefPath,
    int Width,
    int Height,
    LayerGeoref Georef);

sealed record MapDataBundle(
    MapLayerBundle Layers,
    string HeightsPath,
    string WaterPath,
    short[] Heights,
    byte[] Water,
    short MinHeight,
    short MaxHeight);

sealed record LayerGeoref(
    int PixelWidth,
    int PixelHeight,
    double Dpi,
    bool NorthUp,
    string PixelOrigin,
    double EastMeters,
    double NorthMeters,
    double SouthLat,
    double NorthLat,
    double WestLon,
    double EastLon,
    double PdfLeft,
    double PdfBottom,
    double PdfRight,
    double PdfTop,
    string ReliefPdf,
    string RoadsPdf);

sealed class SourcePdfConfig
{
    public string? ReliefPdf { get; set; }
    public string? RoadsPdf { get; set; }
    public int? Dpi { get; set; }
}

sealed class CutoutDoc
{
    public Wgs84Box? Wgs84 { get; set; }
}

sealed class Wgs84Box
{
    public double South { get; set; }
    public double North { get; set; }
    public double West { get; set; }
    public double East { get; set; }
}
