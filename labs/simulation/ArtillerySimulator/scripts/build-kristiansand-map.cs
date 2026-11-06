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
    .Then(state => new ValueTask<MapDataBundle>(BuildLayerDataAsync(state)))
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
    var scaleDenominator = cfg.ScaleDenominator is > 0 ? cfg.ScaleDenominator.Value : 60_000;
    var scaleBarMeters = cfg.ScaleBarMeters is > 0 ? cfg.ScaleBarMeters.Value : 3_700;
    var crs = string.IsNullOrWhiteSpace(cfg.Crs) ? "EPSG:25833" : cfg.Crs.Trim();
    var crsName = string.IsNullOrWhiteSpace(cfg.CrsName) ? "ETRS89 / UTM zone 33N" : cfg.CrsName.Trim();

    var cutout = File.Exists(input.CutoutPath)
        ? JsonSerializer.Deserialize<CutoutDoc>(File.ReadAllText(input.CutoutPath), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
        : null;
    // Prefer inline WGS84 from source-pdfs.json when present.
    if (cfg.Wgs84 is { } inlineBox)
        cutout = new CutoutDoc { Wgs84 = inlineBox };

    Console.WriteLine($"Relief PDF: {relief}");
    Console.WriteLine($"  SHA256: {Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(relief)))}");
    Console.WriteLine($"Roads PDF:  {roads}");
    Console.WriteLine($"  SHA256: {Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(roads)))}");
    Console.WriteLine($"DPI: {dpi}");
    Console.WriteLine($"CRS: {crs} ({crsName}); printed scale 1:{scaleDenominator}; scale bar {scaleBarMeters} m");
    Console.WriteLine("Source: relief+roads PDFs for land/sea/roads; Kartverket NHM_DTM_25833 for real meters.");
    return new MapBuildState(input, relief, roads, dpi, cutout, scaleDenominator, scaleBarMeters, crs, crsName);
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

static async Task<MapDataBundle> BuildLayerDataAsync(MapLayerBundle layers)
{
    using var waterMask = SKBitmap.Decode(layers.WaterMaskPath)
        ?? throw new InvalidDataException(layers.WaterMaskPath);

    var w = layers.Width;
    var h = layers.Height;
    var water = new byte[w * h];
    for (var y = 0; y < h; y++)
    {
        for (var x = 0; x < w; x++)
            water[y * w + x] = waterMask.GetPixel(x, y).Alpha > 0 ? (byte)1 : (byte)0;
    }

    var g = layers.Georef;
    var dtmWidth = Math.Min(w, 4096);
    var dtmHeight = Math.Min(h, 4096);
    Console.WriteLine($"NDH DTM: fetching NHM_DTM_25833 @ {dtmWidth}×{dtmHeight} for WGS84 bbox…");
    var dtm = await FetchNdhDtmAsync(g.WestLon, g.SouthLat, g.EastLon, g.NorthLat, dtmWidth, dtmHeight);
    var dtmPath = Path.Combine(layers.State.Input.LayersDir, "ndh-dtm.tif");
    await File.WriteAllBytesAsync(dtmPath, dtm.TiffBytes);
    var dtmGrid = DecodeTiledFloatTiff(dtm.TiffBytes, out var dw, out var dh);
    if (dw != dtmWidth || dh != dtmHeight)
        Console.WriteLine($"NDH DTM: server returned {dw}×{dh} (requested {dtmWidth}×{dtmHeight})");

    var heights = new short[w * h];
    short minH = short.MaxValue;
    short maxH = short.MinValue;
    var landSamples = 0;
    for (var y = 0; y < h; y++)
    {
        // PDF / mask row 0 = north; DTM export row 0 = north.
        var v = h <= 1 ? 0.0 : y / (double)(h - 1);
        var dy = v * (dh - 1);
        for (var x = 0; x < w; x++)
        {
            var i = y * w + x;
            // Keep NDH under water too (gentle foreshore). Wire color still comes from the PDF mask.
            var u = w <= 1 ? 0.0 : x / (double)(w - 1);
            var dx = u * (dw - 1);
            var metersF = SampleFloatBilinear(dtmGrid, dw, dh, dx, dy);
            if (float.IsNaN(metersF) || metersF < -50f || metersF > 2500f)
                metersF = 0f;
            // Keep real NDH under lakes/rivers. Sea is ~0 in NDH; do not zero elevated water.
            var meters = (short)Math.Clamp(Math.Round(metersF), -30, 32000);
            heights[i] = meters;
            if (water[i] == 0)
                landSamples++;
            if (meters < minH) minH = meters;
            if (meters > maxH) maxH = meters;
        }
    }

    if (landSamples == 0 || minH == short.MaxValue)
        minH = maxH = 0;

    WriteElevationPreviewPng(layers.ElevationPath, heights, water, w, h, minH, maxH);

    var heightsPath = Path.Combine(layers.State.Input.LayersDir, "elevation.bin");
    var waterPath = Path.Combine(layers.State.Input.LayersDir, "water-mask.bin");
    WriteInt16Grid(heightsPath, w, h, heights);
    File.WriteAllBytes(waterPath, water);
    Console.WriteLine(
        $"data: elevation.bin + water-mask.bin ({w}×{h}, NDH meters {minH}–{maxH}, land cells {landSamples}); tiff → {dtmPath}");
    return new MapDataBundle(layers, heightsPath, waterPath, heights, water, minH, maxH);
}

static async Task<(byte[] TiffBytes, int Width, int Height)> FetchNdhDtmAsync(
    double westLon,
    double southLat,
    double eastLon,
    double northLat,
    int width,
    int height)
{
    var bbox = string.Create(
        System.Globalization.CultureInfo.InvariantCulture,
        $"{westLon},{southLat},{eastLon},{northLat}");
    var url =
        "https://hoydedata.no/arcgis/rest/services/NHM_DTM_25833/ImageServer/exportImage" +
        $"?bbox={bbox}&bboxSR=4326&imageSR=25833&size={width},{height}" +
        "&format=tiff&pixelType=F32&noDataInterpretation=esriNoDataMatchAny" +
        "&interpolation=RSP_BilinearInterpolation&f=image";
    using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
    var bytes = await http.GetByteArrayAsync(url);
    if (bytes.Length < 8 || bytes[0] != (byte)'I' || bytes[1] != (byte)'I')
        throw new InvalidDataException($"NDH export was not a little-endian TIFF ({bytes.Length} bytes).");
    return (bytes, width, height);
}

static float[] DecodeTiledFloatTiff(byte[] bytes, out int width, out int height)
{
    static uint U32(byte[] b, int o) => System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(o));
    static ushort U16(byte[] b, int o) => System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(o));

    var ifd = (int)U32(bytes, 4);
    var n = U16(bytes, ifd);
    uint w = 0, h = 0, bps = 0, comp = 0, sf = 0, tileW = 0, tileH = 0;
    var tileOffPtr = 0;
    var tileBytePtr = 0;
    var tileCount = 0;
    for (var i = 0; i < n; i++)
    {
        var o = ifd + 2 + i * 12;
        var tag = U16(bytes, o);
        var type = U16(bytes, o + 2);
        var count = U32(bytes, o + 4);
        var val = U32(bytes, o + 8);
        var s = (ushort)(val & 0xFFFF);
        switch (tag)
        {
            case 256: w = type == 3 ? s : val; break;
            case 257: h = type == 3 ? s : val; break;
            case 258: bps = type == 3 ? s : val; break;
            case 259: comp = type == 3 ? s : val; break;
            case 322: tileW = type == 3 ? s : val; break;
            case 323: tileH = type == 3 ? s : val; break;
            case 324: tileOffPtr = (int)val; tileCount = (int)count; break;
            case 325: tileBytePtr = (int)val; break;
            case 339: sf = type == 3 ? s : val; break;
        }
    }

    if (comp != 1 || bps != 32 || sf != 3 || tileW == 0 || tileH == 0 || tileCount == 0)
        throw new InvalidDataException($"Unsupported NDH TIFF (comp={comp} bps={bps} sf={sf} tile={tileW}x{tileH}).");

    width = (int)w;
    height = (int)h;
    var tilesX = (int)((w + tileW - 1) / tileW);
    var tilesY = (int)((h + tileH - 1) / tileH);
    var grid = new float[width * height];
    Array.Fill(grid, float.NaN);
    for (var ty = 0; ty < tilesY; ty++)
    {
        for (var tx = 0; tx < tilesX; tx++)
        {
            var ti = ty * tilesX + tx;
            if (ti >= tileCount)
                continue;
            var off = (int)U32(bytes, tileOffPtr + ti * 4);
            var len = (int)U32(bytes, tileBytePtr + ti * 4);
            if (off == 0 || len == 0)
                continue;
            for (var row = 0; row < tileH; row++)
            {
                var gy = ty * (int)tileH + row;
                if (gy >= height)
                    break;
                for (var col = 0; col < tileW; col++)
                {
                    var gx = tx * (int)tileW + col;
                    if (gx >= width)
                        break;
                    grid[gy * width + gx] = BitConverter.ToSingle(bytes, off + (row * (int)tileW + col) * 4);
                }
            }
        }
    }

    return grid;
}

static float SampleFloatBilinear(float[] src, int w, int h, double x, double y)
{
    var x0 = (int)Math.Floor(x);
    var y0 = (int)Math.Floor(y);
    var x1 = Math.Min(x0 + 1, w - 1);
    var y1 = Math.Min(y0 + 1, h - 1);
    x0 = Math.Clamp(x0, 0, w - 1);
    y0 = Math.Clamp(y0, 0, h - 1);
    var tx = x - x0;
    var ty = y - y0;
    var a = src[y0 * w + x0];
    var b = src[y0 * w + x1];
    var c = src[y1 * w + x0];
    var d = src[y1 * w + x1];
    static float OrZero(float v) => float.IsNaN(v) ? 0f : v;
    var top = OrZero(a) + (OrZero(b) - OrZero(a)) * (float)tx;
    var bot = OrZero(c) + (OrZero(d) - OrZero(c)) * (float)tx;
    return top + (bot - top) * (float)ty;
}

static void WriteElevationPreviewPng(string path, short[] heights, byte[] water, int w, int h, short minH, short maxH)
{
    using var bmp = new SKBitmap(w, h, SKColorType.Rgba8888, SKAlphaType.Premul);
    var span = Math.Max(1, maxH - minH);
    for (var y = 0; y < h; y++)
    {
        for (var x = 0; x < w; x++)
        {
            var i = y * w + x;
            if (water[i] != 0)
            {
                bmp.SetPixel(x, y, new SKColor(81, 130, 255, 255));
                continue;
            }

            var t = (heights[i] - minH) / (float)span;
            var g = (byte)Math.Clamp(40 + t * 200, 0, 255);
            bmp.SetPixel(x, y, new SKColor(g, (byte)Math.Clamp(g + 20, 0, 255), (byte)Math.Clamp(g - 10, 0, 255), 255));
        }
    }

    using var encoded = SKImage.FromBitmap(bmp).Encode(SKEncodedImageFormat.Png, 92);
    File.WriteAllBytes(path, encoded.ToArray());
}

static MapDataBundle CompositeLayers(MapDataBundle data)
{
    var layers = data.Layers;
    using var relief = SKBitmap.Decode(layers.ReliefSourcePath)!;
    using var elevation = SKBitmap.Decode(layers.ElevationPath)!;
    using var water = SKBitmap.Decode(layers.WaterMaskPath)!;
    using var roads = SKBitmap.Decode(layers.RoadsPath)!;

    using var composite = new SKBitmap(layers.Width, layers.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
    using (var canvas = new SKCanvas(composite))
    {
        canvas.Clear(SKColors.Transparent);
        var sample = new SKSamplingOptions(SKFilterMode.Linear);
        canvas.DrawBitmap(relief, 0, 0, sample);
        canvas.DrawBitmap(roads, 0, 0, sample);
    }

    var outPath = Path.Combine(layers.State.Input.MapDir, "kristiansand-layered-composite.png");
    using var encoded = SKImage.FromBitmap(composite).Encode(SKEncodedImageFormat.Png, 92);
    File.WriteAllBytes(outPath, encoded.ToArray());

    File.Copy(layers.ReliefSourcePath, Path.Combine(layers.State.Input.MapDir, "kristiansand-reference.png"), overwrite: true);
    File.Copy(layers.RoadsPath, Path.Combine(layers.State.Input.MapDir, "kristiansand-roads-reference.png"), overwrite: true);
    File.Copy(layers.LandMaskPath, Path.Combine(layers.State.Input.MapDir, "kristiansand-land-sea-mask.png"), overwrite: true);
    File.Copy(layers.WaterMaskPath, Path.Combine(layers.State.Input.MapDir, "kristiansand-water-mask.png"), overwrite: true);
    File.Copy(layers.ElevationPath, Path.Combine(layers.State.Input.MapDir, "kristiansand-elevation.png"), overwrite: true);
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
    // NDH meters on ~7 m PDF grid, resampled to 1025 (~23 m/cell) for the game mesh.
    const int grid = 1025;
    var meters = ResampleHeights(data.Heights, data.Layers.Width, data.Layers.Height, grid);

    // Land/sea mask binary for the game (KLSM) — full source resolution.
    WriteLandSeaMaskBinary(
        Path.Combine(mapDir, "kristiansand-land-sea-mask.bin"),
        data.Water,
        data.Layers.Width,
        data.Layers.Height,
        (float)east,
        (float)north);

    var heightfieldRel = "layers/heightfield-1025.bin";
    WriteHeightGridBin(Path.Combine(mapDir, heightfieldRel), grid, meters);
    WriteElevationLoaderCs(gameDir, heightfieldRel, grid);
    WriteWireframePreview(mapDir, meters, grid, east, north, data.MinHeight, data.MaxHeight);
    WriteStackJson(mapDir, data, grid, heightfieldRel);

    return $"OK — NDH meters {data.MinHeight}–{data.MaxHeight}; mesh {grid}×{grid} (~{east / (grid - 1):F0} m/cell); layers {data.Layers.State.Input.LayersDir}";
}

static LayerGeoref BuildGeoref(MapBuildState state, PdfMapRasterResult elevation)
{
    // PDF footer: A3 landscape, Koordinatsystem ETRS89/UTM sone 33N, scale bar 0…3700 m, 1:60 000.
    // Ground meters = map-frame size in PDF points × (0.0254 m/inch / 72) × scaleDenominator.
    var metersPerPdfPoint = (0.0254 / 72.0) * state.ScaleDenominator;
    var east = elevation.FramePdf.Width * metersPerPdfPoint;
    var north = elevation.FramePdf.Height * metersPerPdfPoint;
    var scaleBarPdfPoints = state.ScaleBarMeters / metersPerPdfPoint;
    var metersPerPixel = east / Math.Max(1, elevation.PixelWidth - 1);

    double south;
    double northLat;
    double west;
    double eastLon;
    if (state.Cutout?.Wgs84 is { } box
        && box.North > box.South
        && box.East > box.West)
    {
        // Keep the configured center; force lat/lon spans to the PDF-derived meter extent.
        var centerLat = (box.South + box.North) / 2.0;
        var centerLon = (box.West + box.East) / 2.0;
        var halfLat = (north / 2.0) / 111_132.0;
        var metersPerDegLon = 111_320.0 * Math.Cos(centerLat * Math.PI / 180.0);
        var halfLon = (east / 2.0) / metersPerDegLon;
        south = centerLat - halfLat;
        northLat = centerLat + halfLat;
        west = centerLon - halfLon;
        eastLon = centerLon + halfLon;
    }
    else
    {
        south = northLat = west = eastLon = 0;
    }

    Console.WriteLine(
        $"georef: {east:F1} m E × {north:F1} m N @ 1:{state.ScaleDenominator} " +
        $"(scale bar {state.ScaleBarMeters:F0} m ≈ {scaleBarPdfPoints:F1} pt); " +
        $"{metersPerPixel:F3} m/px; {state.Crs}");

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
        state.RoadsPdf,
        state.Crs,
        state.CrsName,
        state.ScaleDenominator,
        state.ScaleBarMeters,
        metersPerPdfPoint,
        metersPerPixel);
}

/// <summary>
/// Splits the hoydedata relief paint into transparent layers:
/// water = pale cyan, land mask = opaque white on land, elevation = hillshade on land only.
/// </summary>
static void SplitReliefMasks(byte[] reliefPng, string waterPath, string landPath, string elevationPath)
{
    using var src = SKBitmap.Decode(reliefPng) ?? throw new InvalidDataException("relief png");
    using var water = new SKBitmap(src.Width, src.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
    using var land = new SKBitmap(src.Width, src.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
    using var elev = new SKBitmap(src.Width, src.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
    water.Erase(SKColors.Transparent);
    land.Erase(SKColors.Transparent);
    elev.Erase(SKColors.Transparent);

    for (var y = 0; y < src.Height; y++)
    {
        for (var x = 0; x < src.Width; x++)
        {
            var c = src.GetPixel(x, y);
            if (c.Alpha < 8)
                continue;

            if (IsHoydedataWater(c.Red, c.Green, c.Blue))
            {
                water.SetPixel(x, y, new SKColor(c.Red, c.Green, c.Blue, 255));
                continue;
            }

            land.SetPixel(x, y, new SKColor(255, 255, 255, 255));
            var lum = (byte)((c.Red * 30 + c.Green * 59 + c.Blue * 11) / 100);
            elev.SetPixel(x, y, new SKColor(lum, lum, lum, 255));
        }
    }

    WritePng(water, waterPath);
    WritePng(land, landPath);
    WritePng(elev, elevationPath);
}

/// <summary>Keeps red road ink; everything else becomes transparent.</summary>
static void WriteRoadsTransparent(byte[] roadsPng, string outPath)
{
    using var src = SKBitmap.Decode(roadsPng) ?? throw new InvalidDataException("roads png");
    using var dest = new SKBitmap(src.Width, src.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
    dest.Erase(SKColors.Transparent);
    for (var y = 0; y < src.Height; y++)
    {
        for (var x = 0; x < src.Width; x++)
        {
            var c = src.GetPixel(x, y);
            if (c.Alpha < 8)
                continue;
            // Norgeskart roads: red channel dominates (solid + dashed).
            if (c.Red >= 140 && c.Red > c.Green + 25 && c.Red > c.Blue + 25)
                dest.SetPixel(x, y, new SKColor(c.Red, c.Green, c.Blue, 255));
        }
    }

    WritePng(dest, outPath);
}

static void WritePng(SKBitmap bitmap, string path)
{
    using var encoded = SKImage.FromBitmap(bitmap).Encode(SKEncodedImageFormat.Png, 100);
    File.WriteAllBytes(path, encoded.ToArray());
}

/// <summary>Pale cyan/turquoise flat water fill from hoydedata.no relief prints (~#E0F7FA).</summary>
static bool IsHoydedataWater(byte r, byte g, byte b)
{
    // Strict pale cyan fill from hoydedata.no (~#E0F7FA). Avoid hillside hillshade blues.
    if (b >= 210 && g >= 200 && r >= 160 && r <= g - 10 && r <= b - 14 && Math.Abs(g - b) <= 28)
        return true;
    if (b >= 195 && g >= 185 && r <= 175 && b > r + 35 && g > r + 25 && Math.Abs(g - b) <= 22)
        return true;
    return false;
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

static void WriteElevationLoaderCs(string gameDir, string heightfieldRel, int grid)
{
    var path = Path.Combine(gameDir, "KristiansandElevation.cs");
    var relLiteral = heightfieldRel.Replace("\\", "/", StringComparison.Ordinal);
    File.WriteAllText(path, $$"""
        // <auto-generated />
        // Kartverket NHM_DTM_25833 meters (sea/lake classification at draw time).
        namespace ArtillerySimulator.Game;

        internal static class KristiansandElevation
        {
            public const int Count = {{grid}};
            public static readonly short[] Meters = Load();

            private static short[] Load()
            {
                var path = Path.Combine(AppContext.BaseDirectory, "map", "{{relLiteral}}");
                if (!File.Exists(path))
                    throw new FileNotFoundException("Missing NDH heightfield bin.", path);

                var bytes = File.ReadAllBytes(path);
                if (bytes.Length < 8)
                    throw new InvalidDataException(path);

                var w = BitConverter.ToInt32(bytes, 0);
                var h = BitConverter.ToInt32(bytes, 4);
                if (w != Count || h != Count)
                    throw new InvalidDataException($"Heightfield {w}×{h}, expected {Count}×{Count}.");

                var meters = new short[Count * Count];
                Buffer.BlockCopy(bytes, 8, meters, 0, meters.Length * sizeof(short));
                return meters;
            }
        }
        """);
}

static void WriteHeightGridBin(string path, int grid, short[] meters)
{
    using var stream = File.Create(path);
    using var writer = new BinaryWriter(stream);
    writer.Write(grid);
    writer.Write(grid);
    foreach (var v in meters)
        writer.Write(v);
}

/// <summary>Orthographic wireframe preview (north-up) for QA of the game heightfield.</summary>
static void WriteWireframePreview(
    string mapDir,
    short[] meters,
    int grid,
    double eastMeters,
    double northMeters,
    short minH,
    short maxH)
{
    const int outW = 2400;
    var outH = Math.Max(1, (int)Math.Round(outW * (northMeters / eastMeters)));
    using var bmp = new SKBitmap(outW, outH, SKColorType.Rgba8888, SKAlphaType.Premul);
    using var canvas = new SKCanvas(bmp);
    canvas.Clear(new SKColor(12, 16, 22, 255));

    var span = Math.Max(1, maxH - minH);
    using var landPaint = new SKPaint
    {
        Color = new SKColor(72, 118, 78, 220),
        StrokeWidth = 1f,
        IsAntialias = false,
        Style = SKPaintStyle.Stroke,
    };
    using var waterPaint = new SKPaint
    {
        Color = new SKColor(40, 90, 140, 180),
        StrokeWidth = 1f,
        IsAntialias = false,
        Style = SKPaintStyle.Stroke,
    };
    using var ridgePaint = new SKPaint
    {
        Color = new SKColor(210, 230, 190, 255),
        StrokeWidth = 1.25f,
        IsAntialias = false,
        Style = SKPaintStyle.Stroke,
    };

    float X(int i) => (float)(i / (double)(grid - 1) * (outW - 1));
    float Y(int j) => (float)((1.0 - j / (double)(grid - 1)) * (outH - 1)); // j=0 south → image bottom
    short H(int i, int j) => meters[j * grid + i];

    for (var j = 0; j < grid; j++)
    {
        for (var i = 0; i < grid - 1; i++)
        {
            var a = H(i, j);
            var b = H(i + 1, j);
            var paint = a == 0 && b == 0 ? waterPaint : (a > minH + span * 0.65 || b > minH + span * 0.65 ? ridgePaint : landPaint);
            canvas.DrawLine(X(i), Y(j), X(i + 1), Y(j), paint);
        }
    }

    for (var i = 0; i < grid; i++)
    {
        for (var j = 0; j < grid - 1; j++)
        {
            var a = H(i, j);
            var b = H(i, j + 1);
            var paint = a == 0 && b == 0 ? waterPaint : (a > minH + span * 0.65 || b > minH + span * 0.65 ? ridgePaint : landPaint);
            canvas.DrawLine(X(i), Y(j), X(i), Y(j + 1), paint);
        }
    }

    var path = Path.Combine(mapDir, "kristiansand-wireframe-preview.png");
    using var encoded = SKImage.FromBitmap(bmp).Encode(SKEncodedImageFormat.Png, 92);
    File.WriteAllBytes(path, encoded.ToArray());
    Console.WriteLine($"wireframe preview: {path} ({grid}×{grid})");
}

static void WriteStackJson(string mapDir, MapDataBundle data, int meshGrid, string heightfieldRel)
{
    var g = data.Layers.Georef;
    var doc = new
    {
        schema = "novolis.artillery.map-stack/3",
        citation = "Land/sea/roads from Norgeskart PDFs; meters from Kartverket NHM_DTM_25833 (NDH).",
        world = new
        {
            northUp = true,
            origin = "southwest_corner_meters",
            crs = g.Crs,
            crsName = g.CrsName,
            scaleDenominator = g.ScaleDenominator,
            scaleBarMeters = g.ScaleBarMeters,
            metersPerPdfPoint = g.MetersPerPdfPoint,
            metersPerPixel = g.MetersPerPixel,
            eastMeters = g.EastMeters,
            northMeters = g.NorthMeters,
            meshGrid,
            meshCellMeters = g.EastMeters / Math.Max(1, meshGrid - 1),
            elevationSource = "NHM_DTM_25833",
            wgs84 = new { south = g.SouthLat, north = g.NorthLat, west = g.WestLon, east = g.EastLon },
            extentSource = "pdf-footer-scale-x-map-frame",
        },
        layers = new object[]
        {
            new { id = "relief-source", imageFile = "layers/relief-source.png", role = "relief_source", pixelWidth = g.PixelWidth, pixelHeight = g.PixelHeight, northUp = true, pixelOrigin = "topLeftNorthWest", sourcePdfPath = g.ReliefPdf },
            new { id = "water-mask", imageFile = "layers/water-mask.png", role = "water", pixelWidth = g.PixelWidth, pixelHeight = g.PixelHeight, northUp = true, pixelOrigin = "topLeftNorthWest", sourcePdfPath = g.ReliefPdf },
            new { id = "land-mask", imageFile = "layers/land-mask.png", role = "land", pixelWidth = g.PixelWidth, pixelHeight = g.PixelHeight, northUp = true, pixelOrigin = "topLeftNorthWest", sourcePdfPath = g.ReliefPdf },
            new { id = "elevation", imageFile = "layers/elevation.png", role = "elevation_preview", pixelWidth = g.PixelWidth, pixelHeight = g.PixelHeight, northUp = true, pixelOrigin = "topLeftNorthWest", source = "NHM_DTM_25833" },
            new { id = "ndh-dtm", imageFile = "layers/ndh-dtm.tif", role = "dtm", source = "https://hoydedata.no/arcgis/rest/services/NHM_DTM_25833/ImageServer" },
            new { id = "roads", imageFile = "layers/roads.png", role = "roads", pixelWidth = g.PixelWidth, pixelHeight = g.PixelHeight, northUp = true, pixelOrigin = "topLeftNorthWest", sourcePdfPath = g.RoadsPdf },
            new { id = "heightfield", imageFile = heightfieldRel, role = "heightfield", grid = meshGrid, northUp = true, source = "NHM_DTM_25833" },
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

sealed record MapBuildState(
    MapBuildInput Input,
    string ReliefPdf,
    string RoadsPdf,
    int Dpi,
    CutoutDoc? Cutout,
    double ScaleDenominator,
    double ScaleBarMeters,
    string Crs,
    string CrsName);

sealed record MapLayerBundle(
    MapBuildState State,
    string ReliefSourcePath,
    string ElevationPath,
    string WaterMaskPath,
    string LandMaskPath,
    string RoadsPath,
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
    string RoadsPdf,
    string Crs,
    string CrsName,
    double ScaleDenominator,
    double ScaleBarMeters,
    double MetersPerPdfPoint,
    double MetersPerPixel);

sealed class SourcePdfConfig
{
    public string? ReliefPdf { get; set; }
    public string? RoadsPdf { get; set; }
    public int? Dpi { get; set; }
    public string? Crs { get; set; }
    public string? CrsName { get; set; }
    public double? ScaleDenominator { get; set; }
    public double? ScaleBarMeters { get; set; }
    public Wgs84Box? Wgs84 { get; set; }
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
