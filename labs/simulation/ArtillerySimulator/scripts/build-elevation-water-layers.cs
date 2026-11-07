#:property OutputType=Exe
#:property TargetFramework=net10.0
#:property ImplicitUsings=enable
#:property Nullable=enable
#:property ManagePackageVersionsCentrally=false
#:package SkiaSharp@4.153.1

using SkiaSharp;

// A) elevation.png — land height only (water transparent)
// B) water-mask.png — binary silhouette (authoritative where-water)
// C) water-heightmap.png — blue shades: sea=0, inland lakes/rivers keep NDH
// D) water-combined.png — height only where water-mask says water (mask wins)
// E) heightfield-1025.bin — game mesh (sea-connected water flattened to 0)
//
// Open-sea rule: flood-fill water from image border → force 0 m.
// Inland water (not sea-connected) keeps NDH so ponds sit on hills.
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
var seaBinPath = Path.Combine(layersDir, "sea-connected.bin");
var heightfieldPath = Path.Combine(layersDir, "heightfield-1025.bin");
const float eastMeters = 24008.08f;
const float northMeters = 16266.16f;
const int meshGrid = 1025;

var (w, h, rawHeights) = ReadInt16Grid(elevBinPath);
var water = File.ReadAllBytes(waterBinPath);
if (water.Length != w * h)
    throw new InvalidDataException($"water-mask.bin length {water.Length} != {w}*{h}");

var sea = MarkSeaConnected(water, w, h);
var heights = ApplySeaFlatten(rawHeights, water, sea);
File.WriteAllBytes(seaBinPath, sea);

short landMin = short.MaxValue, landMax = short.MinValue;
short inlandMin = short.MaxValue, inlandMax = short.MinValue;
var waterCount = 0;
var seaCount = 0;
var inlandCount = 0;
for (var i = 0; i < heights.Length; i++)
{
    if (water[i] == 0)
    {
        if (rawHeights[i] < landMin) landMin = rawHeights[i];
        if (rawHeights[i] > landMax) landMax = rawHeights[i];
        continue;
    }

    waterCount++;
    if (sea[i] != 0)
    {
        seaCount++;
        continue;
    }

    inlandCount++;
    if (heights[i] < inlandMin) inlandMin = heights[i];
    if (heights[i] > inlandMax) inlandMax = heights[i];
}

if (waterCount == 0)
    throw new InvalidDataException("No water cells in water-mask.bin");
if (landMin == short.MaxValue)
    landMin = landMax = 0;
if (inlandCount == 0)
{
    inlandMin = 0;
    inlandMax = 1;
}

// Land preview still uses raw land NDH; water holes transparent.
WriteLandElevationPng(elevPngPath, rawHeights, water, w, h, landMin, landMax);
WriteBinaryWaterMaskPng(waterMaskPngPath, water, w, h);
// Water products use flattened heights (sea = 0).
WriteWaterHeightmapPng(waterHeightPngPath, heights, water, w, h, inlandMin, inlandMax);
WriteWaterElevationBin(waterElevBinPath, heights, water, w, h);

var stripped = WriteWaterCombinedPng(
    waterCombinedPngPath,
    waterMaskPngPath,
    waterHeightPngPath,
    heights,
    water,
    w,
    h,
    inlandMin,
    inlandMax);

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
Console.WriteLine($"water-mask.png      binary ({waterCount} cells) — AUTHORITATIVE");
Console.WriteLine($"sea-connected.bin   {seaCount} cells → 0 m ({100.0 * seaCount / waterCount:F1}% of water)");
Console.WriteLine($"inland water        {inlandCount} cells keep NDH {inlandMin}–{inlandMax} m");
Console.WriteLine($"water-heightmap.png sea flat + inland blue heightmap");
Console.WriteLine($"water-combined.png  mask∩height (stripped {stripped} heightmap-only)");
Console.WriteLine($"heightfield-1025.bin mesh {meshGrid}×{meshGrid} (sea flattened)");

/// <summary>4-connected flood fill from border water → open sea / fjords connected to ocean.</summary>
static byte[] MarkSeaConnected(byte[] water, int w, int h)
{
    var sea = new byte[w * h];
    var queue = new Queue<int>();

    void TryEnqueue(int i)
    {
        if (water[i] == 0 || sea[i] != 0)
            return;
        sea[i] = 1;
        queue.Enqueue(i);
    }

    for (var x = 0; x < w; x++)
    {
        TryEnqueue(x);
        TryEnqueue((h - 1) * w + x);
    }

    for (var y = 0; y < h; y++)
    {
        TryEnqueue(y * w);
        TryEnqueue(y * w + (w - 1));
    }

    while (queue.Count > 0)
    {
        var i = queue.Dequeue();
        var x = i % w;
        var y = i / w;
        if (x > 0) TryEnqueue(i - 1);
        if (x + 1 < w) TryEnqueue(i + 1);
        if (y > 0) TryEnqueue(i - w);
        if (y + 1 < h) TryEnqueue(i + w);
    }

    return sea;
}

/// <summary>Sea-connected water → 0 m; inland mask-water keeps NDH; land unchanged.</summary>
static short[] ApplySeaFlatten(short[] raw, byte[] water, byte[] sea)
{
    var heights = (short[])raw.Clone();
    for (var i = 0; i < heights.Length; i++)
    {
        if (water[i] != 0 && sea[i] != 0)
            heights[i] = 0;
    }

    return heights;
}

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
