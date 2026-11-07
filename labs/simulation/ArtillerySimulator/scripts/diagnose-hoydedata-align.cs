// Overlay NVDB (EPSG:25833 frame) on hoydedata NHM DTM — no PDF roads as truth.
// Marks Dronningens gate controls A/B.
//
//   dotnet run --file d:\novolis\novolis-lab\labs\simulation\ArtillerySimulator\scripts\diagnose-hoydedata-align.cs
//
#:project d:\novolis\novolis-lab\labs\simulation\ArtillerySimulator\scripts\MapPipeline.Support\MapPipeline.Support.csproj
#:property TargetFramework=net10.0
#:property ImplicitUsings=enable
#:property Nullable=enable
#:property ManagePackageVersionsCentrally=false
#:package SkiaSharp@4.153.1

using System.Globalization;
using System.Text.Json;
using ArtillerySimulator.MapPipeline;
using SkiaSharp;

var mapDir = @"d:\novolis\novolis-lab\labs\simulation\ArtillerySimulator\map";
var layersDir = Path.Combine(mapDir, "layers");
var outDir = Path.Combine(layersDir, "align-diagnose");
Directory.CreateDirectory(outDir);

var stack = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(mapDir, "kristiansand-map-stack.json")));
var world = stack.RootElement.GetProperty("world");
var eastM = (float)world.GetProperty("eastMeters").GetDouble();
var northM = (float)world.GetProperty("northMeters").GetDouble();
var e0 = world.TryGetProperty("utmOriginEasting", out var e0El)
    ? e0El.GetDouble()
    : 78006.06073350424;
var n0 = world.TryGetProperty("utmOriginNorthing", out var n0El)
    ? n0El.GetDouble()
    : 6456827.344437925;

var dtmPath = Path.Combine(layersDir, "ndh-dtm.tif");
if (!File.Exists(dtmPath))
    throw new FileNotFoundException("Missing ndh-dtm.tif — run fetch-hoydedata-nhm.cs first.", dtmPath);
var dtmBytes = await File.ReadAllBytesAsync(dtmPath);
var dtm = TiledFloatTiff.Decode(dtmBytes, out var dw, out var dh);
Console.WriteLine($"DTM {dw}×{dh}  frame E0={e0:F1} N0={n0:F1}  {eastM:F0}×{northM:F0} m");

var segs = NvdbRoadsBinary.Read(Path.Combine(layersDir, "nvdb-roads.bin"));
var waterPath = Path.Combine(layersDir, "water-mask.bin");
byte[]? water = File.Exists(waterPath) ? await File.ReadAllBytesAsync(waterPath) : null;
int ww = 0, wh = 0;
if (water is not null)
{
    // water-mask.bin is full PDF raster size from georef
    using var geo = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(layersDir, "georef.json")));
    ww = geo.RootElement.GetProperty("PixelWidth").GetInt32();
    wh = geo.RootElement.GetProperty("PixelHeight").GetInt32();
}

const int outW = 1200;
var outH = Math.Max(1, (int)Math.Round(outW * (northM / eastM)));
using var bmp = new SKBitmap(outW, outH, SKColorType.Rgba8888, SKAlphaType.Premul);
using var canvas = new SKCanvas(bmp);

// Hillshade from DTM (north-up UV = scenario UV)
float Sample(float u, float v) =>
    // u east 0..1, v north from south 0..1; TIFF row 0 = north
    TiledFloatTiff.SampleBilinear(dtm, dw, dh, u * (dw - 1), (1.0 - v) * (dh - 1));

for (var y = 0; y < outH; y++)
{
    var v = outH <= 1 ? 0f : 1f - y / (float)(outH - 1); // image y down; v north fraction from south
    for (var x = 0; x < outW; x++)
    {
        var u = outW <= 1 ? 0f : x / (float)(outW - 1);
        var h = Sample(u, v);
        SKColor col;
        if (float.IsNaN(h) || h < -5f)
            col = new SKColor(20, 50, 100);
        else
        {
            // simple slope shade
            var hu = Sample(Math.Clamp(u + 1f / outW, 0, 1), v);
            var hv = Sample(u, Math.Clamp(v + 1f / outH, 0, 1));
            var dzx = (hu - h);
            var dzy = (hv - h);
            var lit = Math.Clamp(0.55f + (-dzx * 0.7f + dzy * 0.4f) * 0.08f, 0.2f, 1f);
            var g = (byte)Math.Clamp(40 + h * 1.8f * lit, 40, 220);
            col = new SKColor((byte)(g * 0.85f), g, (byte)(g * 0.7f));
        }

        if (water is not null && ww > 0)
        {
            var ix = (int)Math.Clamp(u * (ww - 1), 0, ww - 1);
            var iy = (int)Math.Clamp((1f - v) * (wh - 1), 0, wh - 1);
            if (water[iy * ww + ix] != 0)
                col = new SKColor(30, 70, 140);
        }

        bmp.SetPixel(x, y, col);
    }
}

void DrawSeg(NvdbRoadsBinary.Segment seg, SKPaint paint)
{
    using var path = new SKPath();
    for (var i = 0; i < seg.Points.Length; i++)
    {
        var px = seg.Points[i].X / eastM * (outW - 1);
        var py = (1f - seg.Points[i].Z / northM) * (outH - 1);
        if (i == 0) path.MoveTo(px, py);
        else path.LineTo(px, py);
    }
    canvas.DrawPath(path, paint);
}

using var localP = new SKPaint
{
    Color = new SKColor(255, 180, 60, 160),
    StrokeWidth = 1.0f,
    Style = SKPaintStyle.Stroke,
    IsAntialias = true,
};
using var mainP = new SKPaint
{
    Color = new SKColor(255, 40, 50, 230),
    StrokeWidth = 2.2f,
    Style = SKPaintStyle.Stroke,
    IsAntialias = true,
};
foreach (var s in segs.Where(static s => !s.IsMain))
    DrawSeg(s, localP);
foreach (var s in segs.Where(static s => s.IsMain))
    DrawSeg(s, mainP);

// F471 (Dronningens gate) live in yellow — filter vegnummer in API response
using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(3) };
var A = (lat: 58.1433529, lon: 7.9939879);
var B = (lat: 58.1487535, lon: 8.0038317);
var pad = 0.01;
var bboxW = Math.Min(A.lon, B.lon) - pad;
var bboxE = Math.Max(A.lon, B.lon) + pad;
var bboxS = Math.Min(A.lat, B.lat) - pad;
var bboxN = Math.Max(A.lat, B.lat) + pad;
using var yellow = new SKPaint
{
    Color = new SKColor(255, 230, 0, 250),
    StrokeWidth = 3.5f,
    Style = SKPaintStyle.Stroke,
    IsAntialias = true,
    StrokeCap = SKStrokeCap.Round,
};
var scratch = new List<(double Lat, double Lon)>();
string? start = null;
var f471 = 0;
while (true)
{
    var url =
        "https://nvdbapiles.atlas.vegvesen.no/vegnett/api/v4/veglenkesekvenser/segmentert" +
        string.Create(CultureInfo.InvariantCulture,
            $"?kartutsnitt={bboxW},{bboxS},{bboxE},{bboxN}&srid=4326&antall=1000&trafikantgruppe=K");
    if (start is not null) url += "&start=" + Uri.EscapeDataString(start);
    using var req = new HttpRequestMessage(HttpMethod.Get, url);
    req.Headers.TryAddWithoutValidation("Accept", "application/vnd.vegvesen.nvdb-v3-rev2+json");
    req.Headers.TryAddWithoutValidation("X-Client", "Novolis-ArtillerySimulator");
    using var resp = await http.SendAsync(req);
    resp.EnsureSuccessStatusCode();
    using var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync());
    var root = doc.RootElement;
    if (!root.TryGetProperty("objekter", out var objs)) break;
    foreach (var obj in objs.EnumerateArray())
    {
        if (!obj.TryGetProperty("vegsystemreferanse", out var vsr)
            || !vsr.TryGetProperty("vegsystem", out var vs)
            || !vs.TryGetProperty("nummer", out var n)
            || !n.TryGetInt32(out var nr) || nr != 471)
            continue;
        if (!vs.TryGetProperty("vegkategori", out var k) || k.GetString() != "F") continue;
        if (!obj.TryGetProperty("geometri", out var g) || !g.TryGetProperty("wkt", out var wktEl)) continue;
        if (!NvdbVegnett.TryParseLineStringZ(wktEl.GetString() ?? "", scratch)) continue;
        f471++;
        using var path = new SKPath();
        for (var i = 0; i < scratch.Count; i++)
        {
            var (x, z) = NvdbRoadHierarchy.LonLatToWorldMeters(scratch[i].Lat, scratch[i].Lon, e0, n0);
            var px = x / eastM * (outW - 1);
            var py = (1f - z / northM) * (outH - 1);
            if (i == 0) path.MoveTo(px, py);
            else path.LineTo(px, py);
        }
        canvas.DrawPath(path, yellow);
    }
    start = null;
    if (root.TryGetProperty("metadata", out var meta)
        && meta.TryGetProperty("neste", out var neste)
        && neste.ValueKind == JsonValueKind.Object
        && neste.TryGetProperty("start", out var sEl)
        && sEl.ValueKind == JsonValueKind.String)
        start = sEl.GetString();
    if (string.IsNullOrEmpty(start) || objs.GetArrayLength() == 0) break;
}
Console.WriteLine($"F471 segments drawn: {f471}");

void Mark(double lat, double lon, SKColor col)
{
    var (x, z) = NvdbRoadHierarchy.LonLatToWorldMeters(lat, lon, e0, n0);
    var px = x / eastM * (outW - 1);
    var py = (1f - z / northM) * (outH - 1);
    canvas.DrawCircle(px, py, 8, new SKPaint { Color = col, Style = SKPaintStyle.Fill });
    canvas.DrawCircle(px, py, 8, new SKPaint { Color = SKColors.White, Style = SKPaintStyle.Stroke, StrokeWidth = 2 });
    Console.WriteLine(FormattableString.Invariant($"mark ({lat:F6},{lon:F6}) → world=({x:F1},{z:F1}) px=({px:F1},{py:F1})"));
}
Mark(A.lat, A.lon, new SKColor(255, 0, 255));
Mark(B.lat, B.lon, new SKColor(40, 140, 255));

var outPath = Path.Combine(outDir, "hoydedata-dtm-plus-nvdb-utm.png");
using var enc = SKImage.FromBitmap(bmp).Encode(SKEncodedImageFormat.Png, 92);
await File.WriteAllBytesAsync(outPath, enc.ToArray());
Console.WriteLine($"Wrote {outPath}");
Console.WriteLine("hillshade=NHM DTM  red=NVDB main  orange=local  yellow=F near A–B  magenta=A  blue=B");
Console.WriteLine("(PDF roads intentionally omitted)");
return 0;
