// Rebuild game elevation + DTM-derived water + heightfield on EPSG:25833 UTM frame.
// Uses existing map/layers/ndh-dtm.tif (no re-fetch). Does not touch PDF water authority files
// as gameplay source — water comes from DTM so coasts match NVDB.
//
//   dotnet run --file d:\novolis\novolis-lab\labs\simulation\ArtillerySimulator\scripts\rebuild-utm-game-layers.cs
//
#:project d:\novolis\novolis-lab\labs\simulation\ArtillerySimulator\scripts\MapPipeline.Support\MapPipeline.Support.csproj
#:property TargetFramework=net10.0
#:property ImplicitUsings=enable
#:property Nullable=enable
#:property ManagePackageVersionsCentrally=false
#:package SkiaSharp@4.153.1

using System.Text.Json;
using ArtillerySimulator.MapPipeline;
using SkiaSharp;

var mapDir = @"d:\novolis\novolis-lab\labs\simulation\ArtillerySimulator\map";
var layersDir = Path.Combine(mapDir, "layers");
var geodesyDir = @"d:\novolis\novolis-lab\labs\simulation\ArtillerySimulator\Kristiansand\Geodesy";
var dtmPath = Path.Combine(layersDir, "ndh-dtm.tif");
if (!File.Exists(dtmPath))
    throw new FileNotFoundException("Missing ndh-dtm.tif — run fetch-hoydedata-nhm.cs first.", dtmPath);

using var geo = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(layersDir, "georef.json")));
var w = geo.RootElement.GetProperty("PixelWidth").GetInt32();
var h = geo.RootElement.GetProperty("PixelHeight").GetInt32();
var eastM = (float)geo.RootElement.GetProperty("EastMeters").GetDouble();
var northM = (float)geo.RootElement.GetProperty("NorthMeters").GetDouble();
const int meshGrid = 1025;

var dtm = TiledFloatTiff.Decode(await File.ReadAllBytesAsync(dtmPath), out var dw, out var dh);
Console.WriteLine($"DTM {dw}×{dh} → raster {w}×{h} → mesh {meshGrid} (UTM UV, north-up TIFF row0=north)");

// Sample DTM into PDF-sized grid. Water = nodata or near-sea-level; then sea-connected → 0 m.
var heights = new short[w * h];
var water = new byte[w * h];
short minH = short.MaxValue, maxH = short.MinValue;
var waterCount = 0;
for (var y = 0; y < h; y++)
{
    var v = h <= 1 ? 0.0 : y / (double)(h - 1); // row 0 = north
    for (var x = 0; x < w; x++)
    {
        var i = y * w + x;
        var u = w <= 1 ? 0.0 : x / (double)(w - 1);
        var metersF = TiledFloatTiff.SampleBilinear(dtm, dw, dh, u * (dw - 1), v * (dh - 1));
        var wet = float.IsNaN(metersF) || metersF < 0.75f;
        if (wet)
        {
            water[i] = 1;
            waterCount++;
            metersF = float.IsNaN(metersF) ? 0f : Math.Max(0f, metersF);
        }
        else if (metersF < -50f || metersF > 2500f)
        {
            metersF = 0f;
        }

        var meters = (short)Math.Clamp(Math.Round(metersF), -30, 32000);
        heights[i] = meters;
        if (meters < minH) minH = meters;
        if (meters > maxH) maxH = meters;
    }
}

var sea = WaterSeaConnected.Mark(water, w, h);
var seaCount = WaterSeaConnected.FlattenSeaHeights(heights, water, sea);
File.WriteAllBytes(Path.Combine(layersDir, "sea-connected.bin"), sea);
WriteInt16Grid(Path.Combine(layersDir, "elevation.bin"), w, h, heights);
File.WriteAllBytes(Path.Combine(layersDir, "water-mask.bin"), water);
WriteWaterElevationBin(Path.Combine(layersDir, "water-elevation.bin"), heights, water, w, h);
WriteLandSeaMaskBinary(Path.Combine(mapDir, "kristiansand-land-sea-mask.bin"), water, w, h, eastM, northM);

WriteLandElevationPng(Path.Combine(layersDir, "elevation.png"), heights, water, w, h, minH, maxH);
WriteBinaryWaterMaskPng(Path.Combine(layersDir, "water-mask.png"), water, w, h);
File.Copy(Path.Combine(layersDir, "elevation.png"), Path.Combine(mapDir, "kristiansand-elevation.png"), overwrite: true);
File.Copy(Path.Combine(layersDir, "water-mask.png"), Path.Combine(mapDir, "kristiansand-water-mask.png"), overwrite: true);

var mesh = ResampleHeights(heights, w, h, meshGrid);
var heightfieldRel = "layers/heightfield-1025.bin";
WriteHeightGridBin(Path.Combine(mapDir, heightfieldRel), meshGrid, mesh);
WriteElevationLoaderCs(geodesyDir, heightfieldRel, meshGrid);
WriteWireframePreview(mapDir, mesh, meshGrid, eastM, northM, minH, maxH, water, w, h);
PatchStackJson(Path.Combine(mapDir, "kristiansand-map-stack.json"));

Console.WriteLine(FormattableString.Invariant(
    $"elevation {minH}–{maxH} m; water cells={waterCount:N0}; sea-flattened={seaCount:N0}"));
Console.WriteLine($"heightfield → {Path.Combine(mapDir, heightfieldRel)}");
Console.WriteLine("Game layers rebuilt on UTM hoydedata — rebuild/run Silk host.");
return 0;

static void WriteInt16Grid(string path, int width, int height, short[] values)
{
    using var fs = File.Create(path);
    using var bw = new BinaryWriter(fs);
    bw.Write(width);
    bw.Write(height);
    foreach (var v in values)
        bw.Write(v);
}

static void WriteWaterElevationBin(string path, short[] heights, byte[] waterMask, int width, int height)
{
    var outH = new short[width * height];
    for (var i = 0; i < outH.Length; i++)
        outH[i] = waterMask[i] != 0 ? heights[i] : (short)-32768;
    WriteInt16Grid(path, width, height, outH);
}

static void WriteLandSeaMaskBinary(string path, byte[] waterMask, int width, int height, float east, float north)
{
    var ppm = (width - 1) / east;
    using var stream = File.Create(path);
    using var writer = new BinaryWriter(stream);
    writer.Write(0x4D534C4Bu);
    writer.Write((ushort)1);
    writer.Write((ushort)width);
    writer.Write((ushort)height);
    writer.Write(ppm);
    writer.Write(0f);
    writer.Write(0f);
    writer.Write(east);
    writer.Write(north);
    writer.Write(waterMask);
}

static short[] ResampleHeights(short[] src, int sw, int sh, int grid)
{
    // j=0 south in game meters → image bottom (north-up raster row h-1)
    var dest = new short[grid * grid];
    for (var j = 0; j < grid; j++)
    {
        var v = j / (double)(grid - 1);
        var sy = (1.0 - v) * (sh - 1);
        for (var i = 0; i < grid; i++)
        {
            var u = i / (double)(grid - 1);
            var sx = u * (sw - 1);
            dest[j * grid + i] = SampleBilinear(src, sw, sh, sx, sy);
        }
    }

    return dest;
}

static short SampleBilinear(short[] src, int width, int height, double x, double y)
{
    var x0 = (int)Math.Floor(x);
    var y0 = (int)Math.Floor(y);
    var x1 = Math.Min(x0 + 1, width - 1);
    var y1 = Math.Min(y0 + 1, height - 1);
    var tx = x - x0;
    var ty = y - y0;
    var a = src[y0 * width + x0];
    var b = src[y0 * width + x1];
    var c = src[y1 * width + x0];
    var d = src[y1 * width + x1];
    var top = a + (b - a) * tx;
    var bot = c + (d - c) * tx;
    return (short)Math.Round(top + (bot - top) * ty);
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

static void WriteElevationLoaderCs(string geodesyDir, string heightfieldRel, int grid)
{
    Directory.CreateDirectory(geodesyDir);
    var path = Path.Combine(geodesyDir, "KristiansandElevation.cs");
    var relLiteral = heightfieldRel.Replace("\\", "/", StringComparison.Ordinal);
    File.WriteAllText(path, $$"""
        // <auto-generated />
        // Kartverket NHM_DTM_25833 meters (UTM frame; sea/lake at draw time from DTM water).
        namespace ArtillerySimulator;

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
    Console.WriteLine($"Wrote {path}");
}

static void WriteLandElevationPng(string path, short[] heights, byte[] waterMask, int width, int height, short minH, short maxH)
{
    using var bmp = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
    bmp.Erase(SKColors.Transparent);
    var span = Math.Max(1, maxH - minH);
    for (var y = 0; y < height; y++)
    for (var x = 0; x < width; x++)
    {
        var i = y * width + x;
        if (waterMask[i] != 0)
            continue;
        var t = (heights[i] - minH) / (float)span;
        var g = (byte)Math.Clamp(40 + t * 200, 0, 255);
        bmp.SetPixel(x, y, new SKColor(30, g, 40, 255));
    }

    using var enc = SKImage.FromBitmap(bmp).Encode(SKEncodedImageFormat.Png, 90);
    File.WriteAllBytes(path, enc.ToArray());
}

static void WriteBinaryWaterMaskPng(string path, byte[] waterMask, int width, int height)
{
    using var bmp = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
    bmp.Erase(SKColors.Transparent);
    for (var y = 0; y < height; y++)
    for (var x = 0; x < width; x++)
    {
        if (waterMask[y * width + x] != 0)
            bmp.SetPixel(x, y, new SKColor(0, 200, 220, 255));
    }

    using var enc = SKImage.FromBitmap(bmp).Encode(SKEncodedImageFormat.Png, 90);
    File.WriteAllBytes(path, enc.ToArray());
}

static void WriteWireframePreview(
    string mapDir,
    short[] meters,
    int grid,
    float eastMeters,
    float northMeters,
    short minH,
    short maxH,
    byte[] waterFull,
    int waterW,
    int waterH)
{
    const int outW = 2400;
    var outH = Math.Max(1, (int)Math.Round(outW * (northMeters / eastMeters)));
    using var bmp = new SKBitmap(outW, outH, SKColorType.Rgba8888, SKAlphaType.Premul);
    using var canvas = new SKCanvas(bmp);
    canvas.Clear(new SKColor(12, 16, 22, 255));

    var span = Math.Max(1, maxH - minH);
    using var landPaint = new SKPaint { Color = new SKColor(72, 118, 78, 220), StrokeWidth = 1f, Style = SKPaintStyle.Stroke };
    using var waterPaint = new SKPaint { Color = new SKColor(40, 90, 160, 200), StrokeWidth = 1f, Style = SKPaintStyle.Stroke };

    for (var j = 0; j < grid - 1; j += 8)
    {
        for (var i = 0; i < grid - 1; i += 8)
        {
            var i1 = Math.Min(i + 8, grid - 1);
            var j1 = Math.Min(j + 8, grid - 1);
            float X(int ii) => ii / (float)(grid - 1) * (outW - 1);
            float Y(int jj) => (1f - jj / (float)(grid - 1)) * (outH - 1); // north-up image
            var mx = (i + i1) * 0.5f / (grid - 1) * (waterW - 1);
            var myNorth = (1.0 - (j + j1) * 0.5 / (grid - 1)) * (waterH - 1);
            var wi = (int)Math.Clamp(Math.Round(mx), 0, waterW - 1);
            var wj = (int)Math.Clamp(Math.Round(myNorth), 0, waterH - 1);
            var paint = waterFull[wj * waterW + wi] != 0 ? waterPaint : landPaint;
            canvas.DrawLine(X(i), Y(j), X(i1), Y(j), paint);
            canvas.DrawLine(X(i), Y(j), X(i), Y(j1), paint);
            _ = meters[j * grid + i];
            _ = span;
        }
    }

    var outPath = Path.Combine(mapDir, "layers", "align-diagnose", "utm-game-wireframe.png");
    Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
    using var enc = SKImage.FromBitmap(bmp).Encode(SKEncodedImageFormat.Png, 90);
    File.WriteAllBytes(outPath, enc.ToArray());
    Console.WriteLine($"preview → {outPath}");
}

static void PatchStackJson(string path)
{
    if (!File.Exists(path))
        return;
    var text = File.ReadAllText(path);
    text = text.Replace(
        "Water silhouette from Norgeskart relief PDF; meters from Kartverket NHM_DTM_25833",
        "Water + meters from Kartverket NHM_DTM_25833 (UTM); roads from NVDB",
        StringComparison.Ordinal);
    text = text.Replace(
        "\"authority\": \"where_water_unbreakable\",\n      \"sourcePdfPath\"",
        "\"authority\": \"dtm_near_sea_level\",\n      \"source\": \"NHM_DTM_25833\",\n      \"sourcePdfPath\"",
        StringComparison.Ordinal);
    File.WriteAllText(path, text);
}
