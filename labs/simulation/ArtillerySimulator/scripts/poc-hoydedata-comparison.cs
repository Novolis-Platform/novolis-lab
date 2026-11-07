#:project d:\novolis\novolis-lab\labs\simulation\ArtillerySimulator\scripts\MapPipeline.Support\MapPipeline.Support.csproj
#:property OutputType=Exe
#:property TargetFramework=net10.0
#:property ImplicitUsings=enable
#:property Nullable=enable
#:property ManagePackageVersionsCentrally=false
#:package SkiaSharp@4.153.1

using System.Globalization;
using System.Text.Json;
using ArtillerySimulator.MapPipeline;
using SkiaSharp;

// POC only: fetch NHM ImageServer rasters and write a visual comparison sheet.
// Does NOT write elevation.bin / heightfield / game inputs.
//
//   dotnet run --file d:\novolis\novolis-lab\labs\simulation\ArtillerySimulator\scripts\poc-hoydedata-comparison.cs

var mapDir = @"d:\novolis\novolis-lab\labs\simulation\ArtillerySimulator\map";
var layersDir = Path.Combine(mapDir, "layers");
var outDir = Path.Combine(mapDir, "poc-hoydedata-comparison");
Directory.CreateDirectory(outDir);

var stack = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(mapDir, "kristiansand-map-stack.json")));
var wgs = stack.RootElement.GetProperty("world").GetProperty("wgs84");
var west = wgs.GetProperty("west").GetDouble();
var south = wgs.GetProperty("south").GetDouble();
var east = wgs.GetProperty("east").GetDouble();
var north = wgs.GetProperty("north").GetDouble();

// Modest size for a fast POC preview (not game mesh resolution).
const int fetchW = 1200;
var fetchH = Math.Max(1, (int)Math.Round(fetchW * ((north - south) * 111_132.0) / ((east - west) * 111_320.0 * Math.Cos((south + north) * 0.5 * Math.PI / 180.0))));

Console.WriteLine($"POC bbox WGS84 [{west:F4},{south:F4}]–[{east:F4},{north:F4}]");
Console.WriteLine($"Fetch size {fetchW}×{fetchH} → {outDir}");
Console.WriteLine("(no game heightfield / elevation.bin writes)");

var waterMaskPath = Path.Combine(layersDir, "water-mask.png");
var currentElevPath = Path.Combine(layersDir, "elevation.png");
var labRoadsPath = Path.Combine(layersDir, "roads.png");

using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(8) };

var dtmTiff = await FetchAsync(http, "NHM_DTM_25833", west, south, east, north, fetchW, fetchH);
var domTiff = await FetchAsync(http, "NHM_DOM_25833", west, south, east, north, fetchW, fetchH);
var bathyTiff = await FetchAsync(http, "NHM_DTM_TOPOBATHY_25833", west, south, east, north, fetchW, fetchH);
// LaserInnsyn "Samferdsel" = Geonorge kartdata WMS (kd_veger + jernbane + ferger), not NHM.
var samferdselPng = await FetchSamferdselWmsAsync(http, west, south, east, north, fetchW, fetchH);

await File.WriteAllBytesAsync(Path.Combine(outDir, "poc-nhm-dtm.tif"), dtmTiff);
await File.WriteAllBytesAsync(Path.Combine(outDir, "poc-nhm-dom.tif"), domTiff);
await File.WriteAllBytesAsync(Path.Combine(outDir, "poc-nhm-topobathy.tif"), bathyTiff);
await File.WriteAllBytesAsync(Path.Combine(outDir, "poc-samferdsel-wms.png"), samferdselPng);

var dtm = TiledFloatTiff.Decode(dtmTiff, out var dw, out var dh);
var dom = TiledFloatTiff.Decode(domTiff, out var domW, out var domH);
var bathy = TiledFloatTiff.Decode(bathyTiff, out var bw, out var bh);
if (domW != dw || domH != dh || bw != dw || bh != dh)
    throw new InvalidDataException($"Size mismatch DTM {dw}×{dh} DOM {domW}×{domH} BATHY {bw}×{bh}");

using var waterSrc = SKBitmap.Decode(waterMaskPath)
    ?? throw new FileNotFoundException(waterMaskPath);
var water = ResampleAlphaMask(waterSrc, dw, dh);

using var samferdselRaw = DecodePngBytes(samferdselPng, dw, dh);
// WMS dashes/pink are cartographic — do NOT morph-fix. Draw contiguous NVDB centerlines instead.
// Tunnels are still car links in NVDB geometry → solid primary (surface drape).
using var nvdbRoads = await FetchNvdbCarRoadsRasterAsync(http, west, south, east, north, dw, dh);
using var labRoads = LoadScaled(labRoadsPath, dw, dh);

using var dtmBare = MakeHeatBitmap(dtm, water, dw, dh, waterAsBlue: true, label: "Fetched NHM_DTM (meters only)");
using var domBare = MakeHeatBitmap(dom, water, dw, dh, waterAsBlue: true, label: "Fetched NHM_DOM (meters only)");
using var bathyBare = MakeHeatBitmap(bathy, water, dw, dh, waterAsBlue: false, label: "Fetched TOPOBATHY (meters only)");
using var nDsmBare = MakeDiffBitmap(dom, dtm, water, dw, dh);

SavePng(Path.Combine(outDir, "01-fetched-dtm-land.png"), dtmBare);
SavePng(Path.Combine(outDir, "02-fetched-dom.png"), domBare);
SavePng(Path.Combine(outDir, "03-fetched-topobathy.png"), bathyBare);
SavePng(Path.Combine(outDir, "04-dom-minus-dtm.png"), nDsmBare);

using var dtmWithRoads = WithRoads(dtmBare, nvdbRoads, "FETCHED DTM + NVDB (red=E/R/S/F, gray=K/P)");
using var domWithRoads = WithRoads(domBare, nvdbRoads, "FETCHED DOM + NVDB (red=E/R/S/F, gray=K/P)");
SavePng(Path.Combine(outDir, "01b-fetched-dtm-plus-roads.png"), dtmWithRoads);
SavePng(Path.Combine(outDir, "02b-fetched-dom-plus-roads.png"), domWithRoads);
SavePng(Path.Combine(outDir, "05-fetched-samferdsel-only.png"), samferdselRaw);
SavePng(Path.Combine(outDir, "05b-samferdsel-normalized.png"), nvdbRoads);
SavePng(Path.Combine(outDir, "05c-nvdb-car-roads.png"), nvdbRoads);

using var current = LoadScaled(currentElevPath, dw, dh);
using var panelCurrent = ComposeCurrent(current, water, labRoads, dw, dh);
SavePng(Path.Combine(outDir, "00-current-lab-stack.png"), panelCurrent);

using var sheet = BuildComparisonSheet(
    ("CURRENT lab roads.png", panelCurrent),
    ("WMS raw Samferdsel", LabelBitmap(samferdselRaw, "WMS raw (style only)")),
    ("NVDB hierarchy", LabelBitmap(nvdbRoads, "NVDB: red=Europavei/Riksvei/Stamvei/Fylkesvei · gray=Kommunevei/øvrig")),
    ("FETCHED DTM + NVDB", dtmWithRoads),
    ("FETCHED DOM + NVDB", domWithRoads));
SavePng(Path.Combine(outDir, "COMPARE-sheet.png"), sheet);

WriteReadme(outDir, west, south, east, north, fetchW, fetchH, dtm, water, dw, dh);
Console.WriteLine($"Wrote comparison → {Path.Combine(outDir, "COMPARE-sheet.png")}");

static Task<byte[]> FetchAsync(
    HttpClient http,
    string service,
    double west,
    double south,
    double east,
    double north,
    int width,
    int height) =>
    NhmImageServer.FetchFloatTiffAsync(http, service, west, south, east, north, width, height);

/// <summary>
/// Same source LaserInnsyn2 uses for the "Samferdsel" checkbox:
/// geonorge.layers → wms.kartdata sublayers kd_veger, kd_jernbane, kd_ferger.
/// </summary>
static async Task<byte[]> FetchSamferdselWmsAsync(
    HttpClient http,
    double west,
    double south,
    double east,
    double north,
    int width,
    int height)
{
    // WMS 1.3.0 axis order for EPSG:4326 is lat,lon → south,west,north,east.
    var bbox = string.Create(CultureInfo.InvariantCulture, $"{south},{west},{north},{east}");
    var url =
        "https://wms.geonorge.no/skwms1/wms.kartdata" +
        "?SERVICE=WMS&VERSION=1.3.0&REQUEST=GetMap" +
        "&LAYERS=kd_veger,kd_jernbane,kd_ferger&STYLES=,," +
        $"&CRS=EPSG:4326&BBOX={bbox}&WIDTH={width}&HEIGHT={height}" +
        "&FORMAT=image/png&TRANSPARENT=TRUE";
    Console.WriteLine("GET LaserInnsyn Samferdsel (wms.kartdata kd_veger/jernbane/ferger)…");
    var bytes = await http.GetByteArrayAsync(url);
    if (bytes.Length < 8 || bytes[0] != 0x89 || bytes[1] != (byte)'P')
        throw new InvalidDataException($"Samferdsel WMS: not PNG ({bytes.Length} bytes)");
    Console.WriteLine($"  {bytes.Length:N0} bytes");
    return bytes;
}

static SKBitmap DecodePngBytes(byte[] png, int expectW, int expectH)
{
    using var decoded = SKBitmap.Decode(png) ?? throw new InvalidDataException("Samferdsel PNG decode failed");
    if (decoded.Width == expectW && decoded.Height == expectH)
        return decoded.Copy();
    return decoded.Resize(new SKImageInfo(expectW, expectH), new SKSamplingOptions(SKFilterMode.Nearest))
        ?? throw new InvalidDataException("Samferdsel PNG resize failed");
}

/// <summary>
/// Contiguous car roads from NVDB vegnett, split by vegsystem.vegkategori:
/// main (red) = E Europavei, R Riksvei, S Stamvei, F Fylkesvei;
/// local-local (gray) = K Kommunevei + P privat / remaining.
/// </summary>
static async Task<SKBitmap> FetchNvdbCarRoadsRasterAsync(
    HttpClient http,
    double west,
    double south,
    double east,
    double north,
    int width,
    int height)
{
    Console.WriteLine("GET NVDB vegnett segmentert (trafikantgruppe=K, hierarchy E/R/S/F vs K/P)…");

    var localWkts = new List<string>(24_000);
    var mainWkts = new List<string>(8_000);
    string? start = null;
    var pages = 0;
    var counts = new Dictionary<string, int>(StringComparer.Ordinal);
    while (true)
    {
        var url =
            "https://nvdbapiles.atlas.vegvesen.no/vegnett/api/v4/veglenkesekvenser/segmentert" +
            string.Create(CultureInfo.InvariantCulture,
                $"?kartutsnitt={west},{south},{east},{north}&srid=4326&antall=1000&trafikantgruppe=K");
        if (start is not null)
            url += "&start=" + Uri.EscapeDataString(start);

        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.TryAddWithoutValidation("Accept", "application/vnd.vegvesen.nvdb-v3-rev2+json");
        req.Headers.TryAddWithoutValidation("X-Client", "Novolis-ArtillerySimulator-POC");
        using var resp = await http.SendAsync(req);
        resp.EnsureSuccessStatusCode();
        await using var stream = await resp.Content.ReadAsStreamAsync();
        using var doc = await JsonDocument.ParseAsync(stream);
        var root = doc.RootElement;
        if (!root.TryGetProperty("objekter", out var objekter))
            break;

        foreach (var obj in objekter.EnumerateArray())
        {
            if (!obj.TryGetProperty("geometri", out var geom)
                || !geom.TryGetProperty("wkt", out var wktEl))
                continue;
            var wkt = wktEl.GetString();
            if (string.IsNullOrEmpty(wkt))
                continue;

            var kategori = ReadVegkategori(obj);
            counts.TryGetValue(kategori, out var n);
            counts[kategori] = n + 1;

            if (IsMainVegkategori(kategori))
                mainWkts.Add(wkt);
            else
                localWkts.Add(wkt);
        }

        pages++;
        start = null;
        if (root.TryGetProperty("metadata", out var meta)
            && meta.TryGetProperty("neste", out var neste)
            && neste.ValueKind == JsonValueKind.Object
            && neste.TryGetProperty("start", out var startEl)
            && startEl.ValueKind == JsonValueKind.String)
        {
            start = startEl.GetString();
        }

        if (string.IsNullOrEmpty(start) || objekter.GetArrayLength() == 0)
            break;

        if (pages % 5 == 0)
            Console.WriteLine($"  … main={mainWkts.Count:N0} local={localWkts.Count:N0} ({pages} pages)");
    }

    var bmp = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
    using var canvas = new SKCanvas(bmp);
    canvas.Clear(SKColors.Transparent);

    var invW = 1.0 / Math.Max(1e-12, east - west);
    var invH = 1.0 / Math.Max(1e-12, north - south);

    // Local-local under main so E18/E39/FV/RV stay readable.
    using (var localPaint = new SKPaint
    {
        Color = new SKColor(168, 168, 168, 210),
        IsAntialias = true,
        Style = SKPaintStyle.Stroke,
        StrokeWidth = 1.15f,
        StrokeCap = SKStrokeCap.Round,
        StrokeJoin = SKStrokeJoin.Round,
    })
    {
        foreach (var wkt in localWkts)
            TryDrawLineStringZ(canvas, localPaint, wkt, west, south, north, width, height, invW, invH);
    }

    using (var mainPaint = new SKPaint
    {
        Color = new SKColor(210, 35, 42, 255),
        IsAntialias = true,
        Style = SKPaintStyle.Stroke,
        StrokeWidth = 2.6f,
        StrokeCap = SKStrokeCap.Round,
        StrokeJoin = SKStrokeJoin.Round,
    })
    {
        foreach (var wkt in mainWkts)
            TryDrawLineStringZ(canvas, mainPaint, wkt, west, south, north, width, height, invW, invH);
    }

    Console.WriteLine(
        $"  main(E/R/S/F)={mainWkts.Count:N0} local(K/P/…)={localWkts.Count:N0} pages={pages}");
    foreach (var kv in counts.OrderByDescending(static x => x.Value))
        Console.WriteLine($"    vegkategori {kv.Key}: {kv.Value:N0}");
    return bmp;
}

/// <summary>E Europavei, R Riksvei, S Stamvei, F Fylkesvei — critical main roads.</summary>
static bool IsMainVegkategori(string kategori) =>
    kategori is "E" or "R" or "S" or "F";

static string ReadVegkategori(JsonElement obj)
{
    if (obj.TryGetProperty("vegsystemreferanse", out var vsr)
        && vsr.TryGetProperty("vegsystem", out var vs)
        && vs.TryGetProperty("vegkategori", out var kat)
        && kat.ValueKind == JsonValueKind.String)
    {
        var s = kat.GetString();
        if (!string.IsNullOrEmpty(s))
            return s;
    }

    return "?";
}

/// <summary>
/// NVDB srid=4326 LINESTRING Z is lat lon z (observed).
/// </summary>
static bool TryDrawLineStringZ(
    SKCanvas canvas,
    SKPaint paint,
    string wkt,
    double west,
    double south,
    double north,
    int width,
    int height,
    double invW,
    double invH)
{
    const string prefix = "LINESTRING Z (";
    var idx = wkt.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
    if (idx < 0)
    {
        // Some payloads omit the Z token.
        idx = wkt.IndexOf("LINESTRING (", StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
            return false;
        idx += "LINESTRING (".Length;
    }
    else
    {
        idx += prefix.Length;
    }

    var end = wkt.LastIndexOf(')');
    if (end <= idx)
        return false;

    var body = wkt.AsSpan(idx, end - idx);
    using var path = new SKPath();
    var moved = false;
    foreach (var part in body.ToString().Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
    {
        var bits = part.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (bits.Length < 2)
            continue;
        if (!double.TryParse(bits[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var lat))
            continue;
        if (!double.TryParse(bits[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var lon))
            continue;

        var px = (float)((lon - west) * invW * (width - 1));
        var py = (float)((north - lat) * invH * (height - 1));
        if (!moved)
        {
            path.MoveTo(px, py);
            moved = true;
        }
        else
        {
            path.LineTo(px, py);
        }
    }

    if (!moved || path.PointCount < 2)
        return false;

    canvas.DrawPath(path, paint);
    return true;
}

static SKBitmap LabelBitmap(SKBitmap src, string label)
{
    var bmp = new SKBitmap(src.Width, src.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
    using var canvas = new SKCanvas(bmp);
    canvas.Clear(SKColors.Black);
    canvas.DrawBitmap(src, 0, 0, new SKSamplingOptions(SKFilterMode.Nearest));
    DrawLabel(canvas, bmp.Width, label);
    return bmp;
}

static byte[] ResampleAlphaMask(SKBitmap src, int w, int h)
{
    using var scaled = src.Resize(new SKImageInfo(w, h), new SKSamplingOptions(SKFilterMode.Nearest));
    var water = new byte[w * h];
    for (var y = 0; y < h; y++)
    {
        for (var x = 0; x < w; x++)
            water[y * w + x] = scaled.GetPixel(x, y).Alpha > 0 ? (byte)1 : (byte)0;
    }

    return water;
}

static SKBitmap LoadScaled(string path, int w, int h)
{
    using var src = SKBitmap.Decode(path) ?? throw new FileNotFoundException(path);
    return src.Resize(new SKImageInfo(w, h), new SKSamplingOptions(SKFilterMode.Linear))
        ?? throw new InvalidDataException(path);
}

static SKBitmap ComposeCurrent(SKBitmap elev, byte[] water, SKBitmap roads, int w, int h)
{
    var bmp = new SKBitmap(w, h, SKColorType.Rgba8888, SKAlphaType.Unpremul);
    using var canvas = new SKCanvas(bmp);
    canvas.Clear(new SKColor(8, 12, 18, 255));
    canvas.DrawBitmap(elev, 0, 0, new SKSamplingOptions(SKFilterMode.Linear));
    using var waterPaint = new SKPaint { Color = new SKColor(48, 110, 210, 200) };
    for (var y = 0; y < h; y++)
    {
        for (var x = 0; x < w; x++)
        {
            if (water[y * w + x] == 0)
                continue;
            // Only fill where elev is transparent / missing land.
            if (elev.GetPixel(x, y).Alpha < 8)
                canvas.DrawPoint(x, y, waterPaint);
        }
    }

    canvas.DrawBitmap(roads, 0, 0, new SKSamplingOptions(SKFilterMode.Nearest));
    DrawLabel(canvas, w, "CURRENT: elevation.png + water-mask + roads.png");
    return bmp;
}

static SKBitmap MakeHeatBitmap(float[] grid, byte[] water, int w, int h, bool waterAsBlue, string label)
{
    float min = float.MaxValue, max = float.MinValue;
    for (var i = 0; i < grid.Length; i++)
    {
        var v = grid[i];
        if (float.IsNaN(v) || v < -50f || v > 2500f)
            continue;
        if (waterAsBlue && water[i] != 0)
            continue;
        if (v < min) min = v;
        if (v > max) max = v;
    }

    if (min > max)
    {
        min = 0;
        max = 1;
    }

    var span = Math.Max(1f, max - min);
    var bmp = new SKBitmap(w, h, SKColorType.Rgba8888, SKAlphaType.Unpremul);
    for (var y = 0; y < h; y++)
    {
        for (var x = 0; x < w; x++)
        {
            var i = y * w + x;
            var v = grid[i];
            if (waterAsBlue && water[i] != 0)
            {
                bmp.SetPixel(x, y, new SKColor(48, 110, 210, 255));
                continue;
            }

            if (float.IsNaN(v) || v < -50f || v > 2500f)
            {
                bmp.SetPixel(x, y, new SKColor(0, 0, 0, 255));
                continue;
            }

            var t = Math.Clamp((v - min) / span, 0f, 1f);
            bmp.SetPixel(x, y, Heat(t));
        }
    }

    using (var canvas = new SKCanvas(bmp))
        DrawLabel(canvas, w, $"{label}  [{min:F0}–{max:F0} m]");
    return bmp;
}

static SKBitmap MakeDiffBitmap(float[] dom, float[] dtm, byte[] water, int w, int h)
{
    var bmp = new SKBitmap(w, h, SKColorType.Rgba8888, SKAlphaType.Unpremul);
    for (var y = 0; y < h; y++)
    {
        for (var x = 0; x < w; x++)
        {
            var i = y * w + x;
            if (water[i] != 0)
            {
                bmp.SetPixel(x, y, new SKColor(20, 30, 40, 255));
                continue;
            }

            var a = dom[i];
            var b = dtm[i];
            if (float.IsNaN(a) || float.IsNaN(b))
            {
                bmp.SetPixel(x, y, SKColors.Black);
                continue;
            }

            var d = Math.Clamp(a - b, 0f, 40f) / 40f;
            var r = (byte)Math.Clamp(40 + d * 200, 0, 255);
            var g = (byte)Math.Clamp(20 + d * 40, 0, 255);
            var bl = (byte)Math.Clamp(60 + d * 160, 0, 255);
            bmp.SetPixel(x, y, new SKColor(r, g, bl, 255));
        }
    }

    using (var canvas = new SKCanvas(bmp))
        DrawLabel(canvas, w, "DOM − DTM (surface above bare ground, 0–40 m)");
    return bmp;
}

/// <summary>Composite transparent road overlay (lab PDF or live LaserInnsyn WMS).</summary>
static SKBitmap WithRoads(SKBitmap baseMap, SKBitmap roads, string label)
{
    var bmp = new SKBitmap(baseMap.Width, baseMap.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
    using var canvas = new SKCanvas(bmp);
    canvas.DrawBitmap(baseMap, 0, 0, new SKSamplingOptions(SKFilterMode.Nearest));
    canvas.DrawBitmap(roads, 0, 0, new SKSamplingOptions(SKFilterMode.Nearest));
    DrawLabel(canvas, bmp.Width, label);
    return bmp;
}

static SKColor Heat(float t)
{
    // LaserInnsyn-ish brown → yellow (land only).
    var r = (byte)Math.Clamp(40 + t * 200, 0, 255);
    var g = (byte)Math.Clamp(30 + t * 160, 0, 255);
    var b = (byte)Math.Clamp(20 + t * 40, 0, 255);
    return new SKColor(r, g, b, 255);
}

static void DrawLabel(SKCanvas canvas, int w, string text)
{
    using var bg = new SKPaint { Color = new SKColor(0, 0, 0, 160) };
    canvas.DrawRect(0, 0, w, 28, bg);
    using var font = new SKFont(SKTypeface.Default, 14);
    using var paint = new SKPaint { Color = SKColors.White, IsAntialias = true };
    canvas.DrawText(text, 8, 19, SKTextAlign.Left, font, paint);
}

static SKBitmap BuildComparisonSheet(params (string Title, SKBitmap Bitmap)[] panels)
{
    const int gap = 12;
    const int labelH = 0;
    var cellW = panels[0].Bitmap.Width;
    var cellH = panels[0].Bitmap.Height;
    var cols = panels.Length <= 3 ? panels.Length : 3;
    var rows = (panels.Length + cols - 1) / cols;
    var sheet = new SKBitmap(cols * cellW + (cols + 1) * gap, rows * cellH + (rows + 1) * gap + labelH, SKColorType.Rgba8888, SKAlphaType.Unpremul);
    using var canvas = new SKCanvas(sheet);
    canvas.Clear(new SKColor(16, 18, 22, 255));
    for (var i = 0; i < panels.Length; i++)
    {
        var col = i % cols;
        var row = i / cols;
        var x = gap + col * (cellW + gap);
        var y = gap + row * (cellH + gap);
        canvas.DrawBitmap(panels[i].Bitmap, x, y, new SKSamplingOptions(SKFilterMode.Nearest));
    }

    return sheet;
}

static void SavePng(string path, SKBitmap bmp)
{
    using var encoded = SKImage.FromBitmap(bmp).Encode(SKEncodedImageFormat.Png, 90);
    File.WriteAllBytes(path, encoded.ToArray());
}

static void WriteReadme(
    string outDir,
    double west,
    double south,
    double east,
    double north,
    int fw,
    int fh,
    float[] dtm,
    byte[] water,
    int w,
    int h)
{
    float landMin = float.MaxValue, landMax = float.MinValue;
    var landN = 0;
    for (var i = 0; i < dtm.Length; i++)
    {
        if (water[i] != 0)
            continue;
        var v = dtm[i];
        if (float.IsNaN(v) || v < -50 || v > 2500)
            continue;
        landN++;
        if (v < landMin) landMin = v;
        if (v > landMax) landMax = v;
    }

    var md = $"""
        # Hoydedata POC comparison (not imported)

        Visual-only fetch from Kartverket ImageServer. **Does not** update
        `elevation.bin`, `heightfield-1025.bin`, or the running sim.

        ## Bbox
        WGS84 `{west:F6},{south:F6}` → `{east:F6},{north:F6}` (EPSG:25833 export)
        Fetch raster: {fw}×{fh}

        ## Fetched DTM land stats
        samples={landN}, range={landMin:F1}–{landMax:F1} m (mask water excluded)

        ## Important
        Hoydedata NHM ImageServers are **meters only**. LaserInnsyn’s **Samferdsel**
        checkbox is Geonorge WMS `wms.kartdata` — a **styled raster** (dashes / pink tunnels
        are cartography, not topology). Contiguous roads come from **NVDB vegnett**
        (`trafikantgruppe=K`) centerlines, colored by `vegsystem.vegkategori`:
        **red** = E Europavei / R Riksvei / S Stamvei / F Fylkesvei (critical);
        **gray** = K Kommunevei + P privat / remaining (local-local, design only).

        ## Files
        | File | Meaning |
        | --- | --- |
        | `COMPARE-sheet.png` | Side-by-side panels |
        | `00-current-lab-stack.png` | Current elevation + water-mask + lab `roads.png` |
        | `01-fetched-dtm-land.png` | Live `NHM_DTM_25833` (meters only) |
        | `01b-fetched-dtm-plus-roads.png` | DTM + NVDB car centerlines |
        | `02-fetched-dom.png` | Live `NHM_DOM_25833` (meters only) |
        | `02b-fetched-dom-plus-roads.png` | DOM + NVDB car centerlines |
        | `03-fetched-topobathy.png` | Live `NHM_DTM_TOPOBATHY_25833` |
        | `04-dom-minus-dtm.png` | Surface above bare ground |
        | `05-fetched-samferdsel-only.png` | Raw WMS (style only — do not morph) |
        | `05b-samferdsel-normalized.png` / `05c-nvdb-car-roads.png` | NVDB solid car roads |
        | `poc-samferdsel-wms.png` | Raw WMS GetMap PNG |
        | `poc-nhm-*.tif` | Raw float32 GeoTIFF exports |

        ## Rebuild
        ```powershell
        dotnet run --file d:\novolis\novolis-lab\labs\simulation\ArtillerySimulator\scripts\poc-hoydedata-comparison.cs
        ```
        """;
    File.WriteAllText(Path.Combine(outDir, "README.md"), md);
}

