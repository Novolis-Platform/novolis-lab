// Fetch Kartverket NHM_DTM_25833 for the Oslofjorden / Oscarsborg educational box and
// write game heightfield + land/sea mask (same authority path as Kristiansand).
//
//   dotnet run --file d:\novolis\novolis-lab\labs\simulation\ArtillerySimulator\scripts\build-oslofjorden-map.cs -p:NovolisUseProjectReferences=true
//   dotnet run --file …\build-oslofjorden-map.cs -- --skip-fetch   (reuse existing ndh-dtm.tif)
//
#:project d:\novolis\novolis-lab\labs\simulation\ArtillerySimulator\scripts\MapPipeline.Support\MapPipeline.Support.csproj
#:property TargetFramework=net10.0
#:property ImplicitUsings=enable
#:property Nullable=enable
#:property ManagePackageVersionsCentrally=false
#:package SkiaSharp@4.153.1

using System.Globalization;
using ArtillerySimulator.MapPipeline;
using SkiaSharp;

var skipFetch = args.Contains("--skip-fetch", StringComparer.OrdinalIgnoreCase);

// Must match OslofjordenGeodesy WGS84 box (south-heavy Oscarsborg frame).
const double SouthLat = 59.40;
const double NorthLat = 59.765;
const double WestLon = 10.48;
const double EastLon = 10.78;
const int MeshGrid = 1025;

var appDir = @"d:\novolis\novolis-lab\labs\simulation\ArtillerySimulator";
var mapDir = Path.Combine(appDir, "map", "oslofjorden");
var layersDir = Path.Combine(mapDir, "layers");
Directory.CreateDirectory(layersDir);

var sw = Etrs89Utm33.ToEastingNorthing(SouthLat, WestLon);
var se = Etrs89Utm33.ToEastingNorthing(SouthLat, EastLon);
var nw = Etrs89Utm33.ToEastingNorthing(NorthLat, WestLon);
var e0 = sw.Easting;
var n0 = sw.Northing;
var eastMeters = se.Easting - sw.Easting;
var northMeters = nw.Northing - sw.Northing;
var e1 = e0 + eastMeters;
var n1 = n0 + northMeters;

// Aspect-locked raster; ImageServer soft-cap ~4096 on a side.
var aspect = eastMeters / northMeters;
var hPix = 4096;
var wPix = Math.Clamp((int)Math.Round(hPix * aspect), 1024, 4096);
hPix = Math.Clamp((int)Math.Round(wPix / aspect), 1024, 4096);

Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
    $"Oslofjorden EPSG:25833 E0={e0:F1} N0={n0:F1} → {eastMeters:F0}m E × {northMeters:F0}m N  raster {wPix}×{hPix}"));

var dtmPath = Path.Combine(layersDir, "ndh-dtm.tif");
if (!skipFetch || !File.Exists(dtmPath))
{
    using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(15) };
    var bytes = await NhmImageServer.FetchFloatTiffUtmAsync(http, NhmImageServer.Dtm25833, e0, n0, e1, n1, wPix, hPix);
    await File.WriteAllBytesAsync(dtmPath, bytes);
}
else
{
    Console.WriteLine($"Reusing {dtmPath}");
}

var dtm = TiledFloatTiff.Decode(await File.ReadAllBytesAsync(dtmPath), out var dw, out var dh);
Console.WriteLine($"DTM decoded {dw}×{dh}");

var heights = new short[wPix * hPix];
var water = new byte[wPix * hPix];
short minH = short.MaxValue, maxH = short.MinValue;
var waterCount = 0;
for (var y = 0; y < hPix; y++)
{
    var v = hPix <= 1 ? 0.0 : y / (double)(hPix - 1); // row 0 = north
    for (var x = 0; x < wPix; x++)
    {
        var i = y * wPix + x;
        var u = wPix <= 1 ? 0.0 : x / (double)(wPix - 1);
        var metersF = TiledFloatTiff.SampleBilinear(dtm, dw, dh, u * (dw - 1), v * (dh - 1));
        var wet = float.IsNaN(metersF) || metersF < 0.75f;
        if (wet)
        {
            water[i] = 1;
            waterCount++;
            metersF = float.IsNaN(metersF) ? 0f : Math.Max(0f, metersF);
        }
        else if (metersF is < -50f or > 2500f)
        {
            metersF = 0f;
        }

        var meters = (short)Math.Clamp(Math.Round(metersF), -30, 32000);
        heights[i] = meters;
        if (meters < minH) minH = meters;
        if (meters > maxH) maxH = meters;
    }
}

var sea = WaterSeaConnected.Mark(water, wPix, hPix);
var seaCount = WaterSeaConnected.FlattenSeaHeights(heights, water, sea);
File.WriteAllBytes(Path.Combine(layersDir, "sea-connected.bin"), sea);
WriteInt16Grid(Path.Combine(layersDir, "elevation.bin"), wPix, hPix, heights);
File.WriteAllBytes(Path.Combine(layersDir, "water-mask.bin"), water);

var mesh = ResampleHeights(heights, wPix, hPix, MeshGrid);
var heightfieldRel = "layers/heightfield-1025.bin";
WriteHeightGridBin(Path.Combine(mapDir, heightfieldRel), MeshGrid, mesh);
WriteLandSeaMaskBinary(
    Path.Combine(mapDir, "oslofjorden-land-sea-mask.bin"),
    water,
    wPix,
    hPix,
    (float)eastMeters,
    (float)northMeters);

WriteLandElevationPng(Path.Combine(layersDir, "elevation.png"), heights, water, wPix, hPix, minH, maxH);
WriteBinaryWaterMaskPng(Path.Combine(layersDir, "water-mask.png"), water, wPix, hPix);

WriteElevationLoaderCs(Path.Combine(appDir, "Oslofjorden"), heightfieldRel, MeshGrid);
WriteGeorefJson(Path.Combine(layersDir, "georef.json"), wPix, hPix, e0, n0, eastMeters, northMeters);
WriteStackJson(Path.Combine(mapDir, "oslofjorden-map-stack.json"), e0, n0, eastMeters, northMeters, wPix, hPix, minH, maxH);

Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
    $"elevation {minH}–{maxH} m; water={waterCount:N0}; sea-flattened={seaCount:N0}"));
Console.WriteLine($"heightfield → {Path.Combine(mapDir, heightfieldRel)}");
Console.WriteLine("Rebuild/run ArtillerySimulator with ProjectReferences to pick up Oslofjorden NDH.");
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

static void WriteHeightGridBin(string path, int grid, short[] meters)
{
    using var stream = File.Create(path);
    using var writer = new BinaryWriter(stream);
    writer.Write(grid);
    writer.Write(grid);
    foreach (var v in meters)
        writer.Write(v);
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

static void WriteElevationLoaderCs(string dir, string heightfieldRel, int grid)
{
    Directory.CreateDirectory(dir);
    var path = Path.Combine(dir, "OslofjordenElevation.cs");
    var relLiteral = heightfieldRel.Replace("\\", "/", StringComparison.Ordinal);
    File.WriteAllText(path, $$"""
        // <auto-generated />
        // Kartverket NHM_DTM_25833 meters (Oslofjorden / Oscarsborg UTM frame).
        namespace ArtillerySimulator;

        internal static class OslofjordenElevation
        {
            public const int Count = {{grid}};
            public static readonly short[] Meters = Load();

            private static short[] Load()
            {
                var path = Path.Combine(AppContext.BaseDirectory, "map", "oslofjorden", "{{relLiteral}}");
                if (!File.Exists(path))
                {
                    path = Path.GetFullPath(Path.Combine(
                        AppContext.BaseDirectory, "..", "..", "..", "map", "oslofjorden", "{{relLiteral}}"));
                }

                if (!File.Exists(path))
                    throw new FileNotFoundException(
                        "Missing Oslofjorden NDH heightfield. Run scripts/build-oslofjorden-map.cs.",
                        path);

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

static void WriteGeorefJson(string path, int w, int h, double e0, double n0, double eastM, double northM)
{
    // File-based apps disable reflection JSON; write the small georef by hand.
    File.WriteAllText(path, string.Create(CultureInfo.InvariantCulture, $$"""
        {
          "PixelWidth": {{w}},
          "PixelHeight": {{h}},
          "EastMeters": {{eastM}},
          "NorthMeters": {{northM}},
          "UtmOriginEasting": {{e0}},
          "UtmOriginNorthing": {{n0}},
          "Crs": "EPSG:25833",
          "ElevationSource": "NHM_DTM_25833"
        }
        """));
}

static void WriteStackJson(
    string path,
    double e0,
    double n0,
    double eastM,
    double northM,
    int w,
    int h,
    short minH,
    short maxH)
{
    File.WriteAllText(path, string.Create(CultureInfo.InvariantCulture, $$"""
        {
          "schema": "novolis.artillery.map-stack/3",
          "citation": "Oslofjorden / Oscarsborg educational cutout from Kartverket NHM_DTM_25833 (hoydedata.no).",
          "world": {
            "northUp": true,
            "origin": "southwest_corner_meters",
            "crs": "EPSG:25833",
            "eastMeters": {{eastM}},
            "northMeters": {{northM}},
            "meshGrid": {{MeshGrid}},
            "elevationSource": "NHM_DTM_25833",
            "elevationMinMeters": {{minH}},
            "elevationMaxMeters": {{maxH}},
            "wgs84": { "south": {{SouthLat}}, "north": {{NorthLat}}, "west": {{WestLon}}, "east": {{EastLon}} },
            "utmOriginEasting": {{e0}},
            "utmOriginNorthing": {{n0}},
            "raster": { "width": {{w}}, "height": {{h}} }
          }
        }
        """));
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

    using var enc = SKImage.FromBitmap(bmp).Encode(SKEncodedImageFormat.Png, 85);
    File.WriteAllBytes(path, enc.ToArray());
}

static void WriteBinaryWaterMaskPng(string path, byte[] waterMask, int width, int height)
{
    using var bmp = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
    bmp.Erase(SKColors.Transparent);
    for (var y = 0; y < height; y++)
    for (var x = 0; x < width; x++)
    {
        if (waterMask[y * width + x] == 0)
            continue;
        bmp.SetPixel(x, y, new SKColor(40, 140, 220, 255));
    }

    using var enc = SKImage.FromBitmap(bmp).Encode(SKEncodedImageFormat.Png, 85);
    File.WriteAllBytes(path, enc.ToArray());
}
