#:property OutputType=Exe
#:property TargetFramework=net10.0
#:property ImplicitUsings=enable
#:property Nullable=enable
#:property ManagePackageVersionsCentrally=false
#:package SkiaSharp@4.153.1

using SkiaSharp;

// A) elevation.png — land height only (water transparent)
// B) water-mask.png — binary silhouette (flat cyan; authoritative where-water)
// C) water-heightmap.png — blue NDH shades (may have errors)
// D) water-combined.png — height shades ONLY where water-mask says water (mask wins)
// E) heightfield-1025.bin — game mesh meters
//
//   dotnet run --file d:\novolis\novolis-lab\labs\simulation\ArtillerySimulator\scripts\build-elevation-water-layers.cs

var layersDir = @"d:\novolis\novolis-lab\labs\simulation\ArtillerySimulator\map\layers";
var mapDir = Path.GetDirectoryName(layersDir)!;
var elevBinPath = Path.Combine(layersDir, "elevation.bin");
var waterBinPath = Path.Combine(layersDir, "water-mask.bin");
var elevPngPath = Path.Combine(layersDir, "elevation.png");
var waterMaskPngPath = Path.Combine(layersDir, "water-mask.png");
var waterHeightPngPath = Path.Combine(layersDir, "water-heightmap.png");
var waterCombinedPngPath = Path.Combine(layersDir, "water-combined.png");
var waterElevBinPath = Path.Combine(layersDir, "water-elevation.bin");
var heightfieldPath = Path.Combine(layersDir, "heightfield-1025.bin");
const float eastMeters = 24008.08f;
const float northMeters = 16266.16f;
const int meshGrid = 1025;

var (w, h, heights) = ReadInt16Grid(elevBinPath);
var water = File.ReadAllBytes(waterBinPath);
if (water.Length != w * h)
    throw new InvalidDataException($"water-mask.bin length {water.Length} != {w}*{h}");

short landMin = short.MaxValue, landMax = short.MinValue;
short waterMin = short.MaxValue, waterMax = short.MinValue;
var waterCount = 0;
for (var i = 0; i < heights.Length; i++)
{
    var m = heights[i];
    if (water[i] != 0)
    {
        waterCount++;
        if (m < waterMin) waterMin = m;
        if (m > waterMax) waterMax = m;
    }
    else
    {
        if (m < landMin) landMin = m;
        if (m > landMax) landMax = m;
    }
}

if (waterCount == 0)
    throw new InvalidDataException("No water cells in water-mask.bin");
if (landMin == short.MaxValue)
    landMin = landMax = 0;

WriteLandElevationPng(elevPngPath, heights, water, w, h, landMin, landMax);
WriteBinaryWaterMaskPng(waterMaskPngPath, water, w, h);
WriteWaterHeightmapPng(waterHeightPngPath, heights, water, w, h, waterMin, waterMax);
WriteWaterElevationBin(waterElevBinPath, heights, water, w, h);

// Third layer: combine mask + heightmap. water-mask.png wins every conflict.
var stripped = WriteWaterCombinedPng(
    waterCombinedPngPath,
    waterMaskPngPath,
    waterHeightPngPath,
    heights,
    water,
    w,
    h,
    waterMin,
    waterMax);

var mesh = ResampleHeights(heights, w, h, meshGrid);
WriteHeightGridBin(heightfieldPath, meshGrid, mesh);
WriteLandSeaMaskBinary(
    Path.Combine(mapDir, "kristiansand-land-sea-mask.bin"),
    water,
    w,
    h,
    eastMeters,
    northMeters);

File.Copy(elevPngPath, Path.Combine(mapDir, "kristiansand-elevation.png"), overwrite: true);
File.Copy(waterMaskPngPath, Path.Combine(mapDir, "kristiansand-water-mask.png"), overwrite: true);
File.Copy(waterHeightPngPath, Path.Combine(mapDir, "kristiansand-water-heightmap.png"), overwrite: true);
File.Copy(waterCombinedPngPath, Path.Combine(mapDir, "kristiansand-water-combined.png"), overwrite: true);

Console.WriteLine($"elevation.png       land {landMin}–{landMax} m (water transparent)");
Console.WriteLine($"water-mask.png      binary silhouette ({waterCount} cells, {100.0 * waterCount / (w * h):F1}%) — AUTHORITATIVE");
Console.WriteLine($"water-heightmap.png water {waterMin}–{waterMax} m blue heightmap");
Console.WriteLine($"water-combined.png  mask∩height (stripped {stripped} heightmap-only pixels)");
Console.WriteLine($"heightfield-1025.bin mesh {meshGrid}×{meshGrid}");
Console.WriteLine($"water-elevation.bin int16 meters under mask");

static (int W, int H, short[] Heights) ReadInt16Grid(string path)
{
    using var stream = File.OpenRead(path);
    using var reader = new BinaryReader(stream);
    var w = reader.ReadInt32();
    var h = reader.ReadInt32();
    var heights = new short[w * h];
    for (var i = 0; i < heights.Length; i++)
        heights[i] = reader.ReadInt16();
    return (w, h, heights);
}

static void WriteLandElevationPng(string path, short[] heights, byte[] water, int w, int h, short minH, short maxH)
{
    using var bmp = new SKBitmap(w, h, SKColorType.Rgba8888, SKAlphaType.Unpremul);
    bmp.Erase(SKColors.Transparent);
    var span = Math.Max(1, maxH - minH);
    for (var y = 0; y < h; y++)
    {
        for (var x = 0; x < w; x++)
        {
            var i = y * w + x;
            if (water[i] != 0)
                continue;

            var t = (heights[i] - minH) / (float)span;
            var g = (byte)Math.Clamp(40 + t * 200, 0, 255);
            bmp.SetPixel(x, y, new SKColor(g, (byte)Math.Clamp(g + 20, 0, 255), (byte)Math.Clamp(g - 10, 0, 255), 255));
        }
    }

    using var encoded = SKImage.FromBitmap(bmp).Encode(SKEncodedImageFormat.Png, 92);
    File.WriteAllBytes(path, encoded.ToArray());
}

static void WriteBinaryWaterMaskPng(string path, byte[] water, int w, int h)
{
    using var bmp = new SKBitmap(w, h, SKColorType.Rgba8888, SKAlphaType.Unpremul);
    bmp.Erase(SKColors.Transparent);
    var cyan = new SKColor(224, 247, 250, 255);
    for (var y = 0; y < h; y++)
    {
        for (var x = 0; x < w; x++)
        {
            if (water[y * w + x] == 0)
                continue;
            bmp.SetPixel(x, y, cyan);
        }
    }

    using var encoded = SKImage.FromBitmap(bmp).Encode(SKEncodedImageFormat.Png, 100);
    File.WriteAllBytes(path, encoded.ToArray());
}

static void WriteWaterHeightmapPng(string path, short[] heights, byte[] water, int w, int h, short minH, short maxH)
{
    using var bmp = new SKBitmap(w, h, SKColorType.Rgba8888, SKAlphaType.Unpremul);
    bmp.Erase(SKColors.Transparent);
    var lo = Math.Min(minH, (short)0);
    var hi = Math.Max(maxH, (short)1);
    var span = Math.Max(1f, hi - lo);
    for (var y = 0; y < h; y++)
    {
        for (var x = 0; x < w; x++)
        {
            var i = y * w + x;
            if (water[i] == 0)
                continue;
            bmp.SetPixel(x, y, ShadeBlue(heights[i], lo, span));
        }
    }

    using var encoded = SKImage.FromBitmap(bmp).Encode(SKEncodedImageFormat.Png, 92);
    File.WriteAllBytes(path, encoded.ToArray());
}

/// <summary>
/// Unbreakable rule: water-mask.png defines water. Heightmap shades apply only on mask-water;
/// heightmap-only pixels are stripped. Mask-water with missing heightmap uses NDH shade.
/// </summary>
static int WriteWaterCombinedPng(
    string path,
    string maskPath,
    string heightPath,
    short[] heights,
    byte[] waterBin,
    int w,
    int h,
    short waterMin,
    short waterMax)
{
    using var mask = SKBitmap.Decode(maskPath) ?? throw new FileNotFoundException(maskPath);
    using var height = SKBitmap.Decode(heightPath) ?? throw new FileNotFoundException(heightPath);
    if (mask.Width != w || mask.Height != h || height.Width != w || height.Height != h)
        throw new InvalidDataException($"Size mismatch mask {mask.Width}×{mask.Height} height {height.Width}×{height.Height} expect {w}×{h}");

    using var bmp = new SKBitmap(w, h, SKColorType.Rgba8888, SKAlphaType.Unpremul);
    bmp.Erase(SKColors.Transparent);
    var lo = Math.Min(waterMin, (short)0);
    var hi = Math.Max(waterMax, (short)1);
    var span = Math.Max(1f, hi - lo);
    var stripped = 0;
    for (var y = 0; y < h; y++)
    {
        for (var x = 0; x < w; x++)
        {
            var i = y * w + x;
            var m = mask.GetPixel(x, y);
            var hc = height.GetPixel(x, y);
            var maskWater = m.Alpha > 0 || waterBin[i] != 0;
            var heightWater = hc.Alpha > 0;

            if (!maskWater)
            {
                // Mask wins: heightmap-only "water" is not water.
                if (heightWater)
                    stripped++;
                continue;
            }

            if (heightWater)
                bmp.SetPixel(x, y, new SKColor(hc.Red, hc.Green, hc.Blue, 255));
            else
                bmp.SetPixel(x, y, ShadeBlue(heights[i], lo, span));
        }
    }

    using var encoded = SKImage.FromBitmap(bmp).Encode(SKEncodedImageFormat.Png, 92);
    File.WriteAllBytes(path, encoded.ToArray());
    return stripped;
}

static SKColor ShadeBlue(short meters, short lo, float span)
{
    var t = Math.Clamp((meters - lo) / span, 0f, 1f);
    var r = (byte)Math.Clamp(18 + t * 90, 0, 255);
    var g = (byte)Math.Clamp(70 + t * 160, 0, 255);
    var b = (byte)Math.Clamp(160 + t * 95, 0, 255);
    return new SKColor(r, g, b, 255);
}

static void WriteWaterElevationBin(string path, short[] heights, byte[] water, int w, int h)
{
    using var stream = File.Create(path);
    using var writer = new BinaryWriter(stream);
    writer.Write(w);
    writer.Write(h);
    for (var i = 0; i < heights.Length; i++)
        writer.Write(water[i] != 0 ? heights[i] : (short)-32768);
}

static short[] ResampleHeights(short[] src, int sw, int sh, int grid)
{
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

static short SampleBilinear(short[] src, int w, int h, double x, double y)
{
    var x0 = (int)Math.Floor(x);
    var y0 = (int)Math.Floor(y);
    var x1 = Math.Min(x0 + 1, w - 1);
    var y1 = Math.Min(y0 + 1, h - 1);
    var tx = x - x0;
    var ty = y - y0;
    x0 = Math.Clamp(x0, 0, w - 1);
    y0 = Math.Clamp(y0, 0, h - 1);
    var a = src[y0 * w + x0];
    var b = src[y0 * w + x1];
    var c = src[y1 * w + x0];
    var d = src[y1 * w + x1];
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
