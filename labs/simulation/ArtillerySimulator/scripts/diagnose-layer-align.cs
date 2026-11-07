// Transparent collage of water / PDF roads / NVDB / DTM to diagnose georef offset.
//
//   dotnet run --file d:\novolis\novolis-lab\labs\simulation\ArtillerySimulator\scripts\diagnose-layer-align.cs
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

var layersDir = @"d:\novolis\novolis-lab\labs\simulation\ArtillerySimulator\map\layers";
var outDir = Path.Combine(layersDir, "align-diagnose");
Directory.CreateDirectory(outDir);

const float EastM = 24008.08f;
const float NorthM = 16266.16f;

using var waterPng = SKBitmap.Decode(Path.Combine(layersDir, "water-mask.png"))
    ?? throw new FileNotFoundException("water-mask.png");
using var pdfRoads = SKBitmap.Decode(Path.Combine(layersDir, "roads.png"))
    ?? throw new FileNotFoundException("roads.png");
using var elevPng = SKBitmap.Decode(Path.Combine(layersDir, "elevation.png"));
var w = waterPng.Width;
var h = waterPng.Height;
var water = File.ReadAllBytes(Path.Combine(layersDir, "water-mask.bin"));
if (water.Length != w * h)
    throw new InvalidDataException($"water-mask.bin {water.Length} != {w}×{h}");

var segs = NvdbRoadsBinary.Read(Path.Combine(layersDir, "nvdb-roads.bin"));

// Scaled working size for collage tiles (full-res crops stay full later).
const int tileW = 900;
var tileH = Math.Max(1, (int)Math.Round(tileW * (h / (double)w)));

SKBitmap ScaleTo(SKBitmap src, int dw, int dh)
{
    var dst = new SKBitmap(dw, dh, SKColorType.Rgba8888, SKAlphaType.Unpremul);
    using var c = new SKCanvas(dst);
    c.Clear(SKColors.Transparent);
    c.DrawBitmap(src, SKRect.Create(dw, dh));
    return dst;
}

SKBitmap MakeWaterOnly()
{
    var bmp = new SKBitmap(w, h, SKColorType.Rgba8888, SKAlphaType.Unpremul);
    for (var y = 0; y < h; y++)
    for (var x = 0; x < w; x++)
    {
        bmp.SetPixel(x, y, water[y * w + x] != 0
            ? new SKColor(40, 110, 220, 255)
            : new SKColor(20, 22, 24, 255));
    }
    return bmp;
}

SKBitmap MakePdfRoadsOnly()
{
    var bmp = new SKBitmap(w, h, SKColorType.Rgba8888, SKAlphaType.Unpremul);
    bmp.Erase(SKColors.Transparent);
    for (var y = 0; y < h; y++)
    for (var x = 0; x < w; x++)
    {
        var c = pdfRoads.GetPixel(x, y);
        if (c.Alpha < 40) continue;
        // Primary-ish red → lime; other ink → pale green
        var primary = c.Red > 140 && c.Red > c.Green + 30 && c.Red > c.Blue + 30;
        bmp.SetPixel(x, y, primary
            ? new SKColor(0, 255, 90, 230)
            : new SKColor(120, 255, 160, 160));
    }
    return bmp;
}

SKBitmap MakeNvdbRoadsOnly()
{
    var bmp = new SKBitmap(w, h, SKColorType.Rgba8888, SKAlphaType.Unpremul);
    using var canvas = new SKCanvas(bmp);
    canvas.Clear(SKColors.Transparent);
    using var local = new SKPaint
    {
        Color = new SKColor(255, 200, 60, 180),
        StrokeWidth = 1.1f,
        IsAntialias = true,
        Style = SKPaintStyle.Stroke,
        StrokeCap = SKStrokeCap.Round,
    };
    using var main = new SKPaint
    {
        Color = new SKColor(255, 40, 50, 240),
        StrokeWidth = 2.4f,
        IsAntialias = true,
        Style = SKPaintStyle.Stroke,
        StrokeCap = SKStrokeCap.Round,
    };
    void Draw(NvdbRoadsBinary.Segment seg, SKPaint paint)
    {
        using var path = new SKPath();
        for (var i = 0; i < seg.Points.Length; i++)
        {
            var px = seg.Points[i].X / EastM * (w - 1);
            var py = (1f - seg.Points[i].Z / NorthM) * (h - 1);
            if (i == 0) path.MoveTo(px, py);
            else path.LineTo(px, py);
        }
        canvas.DrawPath(path, paint);
    }
    foreach (var s in segs.Where(static s => !s.IsMain))
        Draw(s, local);
    foreach (var s in segs.Where(static s => s.IsMain))
        Draw(s, main);
    return bmp;
}

SKBitmap MakeElevTint()
{
    var bmp = new SKBitmap(w, h, SKColorType.Rgba8888, SKAlphaType.Unpremul);
    if (elevPng is null)
    {
        bmp.Erase(new SKColor(30, 30, 30));
        return bmp;
    }
    for (var y = 0; y < h; y++)
    for (var x = 0; x < w; x++)
    {
        var c = elevPng.GetPixel(
            Math.Min(x, elevPng.Width - 1),
            Math.Min(y, elevPng.Height - 1));
        if (c.Alpha < 20)
            bmp.SetPixel(x, y, new SKColor(20, 22, 24, 255));
        else
            bmp.SetPixel(x, y, new SKColor(c.Red, c.Green, c.Blue, 255));
    }
    return bmp;
}

static void BlendOver(SKBitmap dst, SKBitmap src, byte alphaScale = 255)
{
    for (var y = 0; y < dst.Height; y++)
    for (var x = 0; x < dst.Width; x++)
    {
        var s = src.GetPixel(x, y);
        if (s.Alpha == 0) continue;
        var a = (byte)(s.Alpha * alphaScale / 255);
        if (a == 0) continue;
        var d = dst.GetPixel(x, y);
        var inv = 255 - a;
        dst.SetPixel(x, y, new SKColor(
            (byte)((s.Red * a + d.Red * inv) / 255),
            (byte)((s.Green * a + d.Green * inv) / 255),
            (byte)((s.Blue * a + d.Blue * inv) / 255),
            255));
    }
}

static SKBitmap Clone(SKBitmap src)
{
    var dst = new SKBitmap(src.Width, src.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
    using var c = new SKCanvas(dst);
    c.DrawBitmap(src, 0, 0);
    return dst;
}

static void Label(SKCanvas canvas, string text, float x, float y)
{
    using var bg = new SKPaint { Color = new SKColor(0, 0, 0, 180) };
    using var fg = new SKPaint { Color = SKColors.White, IsAntialias = true };
    using var font = new SKFont(SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold), 18);
    font.MeasureText(text, out var bounds);
    canvas.DrawRect(x - 4, y + bounds.Top - 4, bounds.Width + 8, bounds.Height + 8, bg);
    canvas.DrawText(text, x, y, SKTextAlign.Left, font, fg);
}

static void Save(SKBitmap bmp, string path)
{
    using var enc = SKImage.FromBitmap(bmp).Encode(SKEncodedImageFormat.Png, 92);
    File.WriteAllBytes(path, enc.ToArray());
    Console.WriteLine($"  → {path} ({bmp.Width}×{bmp.Height})");
}

Console.WriteLine("Building layer rasters…");
using var waterFull = MakeWaterOnly();
using var pdfFull = MakePdfRoadsOnly();
using var nvdbFull = MakeNvdbRoadsOnly();
using var elevFull = MakeElevTint();

// --- Full-map stacks ---
Console.WriteLine("Stacks…");
using (var stackPdf = Clone(waterFull))
{
    BlendOver(stackPdf, pdfFull);
    Save(ScaleTo(stackPdf, tileW, tileH), Path.Combine(outDir, "01-water-plus-pdf-roads.png"));
}
using (var stackNvdb = Clone(waterFull))
{
    BlendOver(stackNvdb, nvdbFull);
    Save(ScaleTo(stackNvdb, tileW, tileH), Path.Combine(outDir, "02-water-plus-nvdb.png"));
}
using (var stackBoth = Clone(waterFull))
{
    BlendOver(stackBoth, pdfFull, 200);
    BlendOver(stackBoth, nvdbFull, 220);
    Save(ScaleTo(stackBoth, tileW, tileH), Path.Combine(outDir, "03-water-pdf-green-nvdb-red.png"));
}
using (var stackElev = Clone(elevFull))
{
    BlendOver(stackElev, nvdbFull);
    Save(ScaleTo(stackElev, tileW, tileH), Path.Combine(outDir, "04-elevation-plus-nvdb.png"));
}

// Difference: where PDF primary and NVDB main disagree (magenta = NVDB only, green = PDF only)
Console.WriteLine("Difference map…");
using (var diff = new SKBitmap(w, h, SKColorType.Rgba8888, SKAlphaType.Unpremul))
{
    for (var y = 0; y < h; y++)
    for (var x = 0; x < w; x++)
    {
        var wet = water[y * w + x] != 0;
        var pdf = pdfFull.GetPixel(x, y).Alpha > 40;
        var nv = nvdbFull.GetPixel(x, y).Alpha > 40;
        SKColor c;
        if (pdf && nv) c = new SKColor(255, 0, 255, 255);      // overlap
        else if (nv && !pdf) c = new SKColor(255, 50, 50, 255); // NVDB only
        else if (pdf && !nv) c = new SKColor(0, 255, 100, 255); // PDF only
        else c = wet ? new SKColor(25, 50, 100, 255) : new SKColor(18, 18, 18, 255);
        diff.SetPixel(x, y, c);
    }
    Save(ScaleTo(diff, tileW, tileH), Path.Combine(outDir, "05-diff-pdf-green-nvdb-red-overlap-magenta.png"));
}

// --- Bridge crops (full-res) ---
// Kristiansand: downtown-ish + Varodd / fjord belt in pixel space.
var crops = new (string Id, int Cx, int Cy, int Half)[]
{
    ("crop-a-city-harbour", (int)(0.42 * w), (int)(0.48 * h), 320),
    ("crop-b-east-bridge", (int)(0.52 * w), (int)(0.55 * h), 320),
    ("crop-c-west-coast", (int)(0.30 * w), (int)(0.62 * h), 280),
};

SKBitmap Crop(SKBitmap src, int cx, int cy, int half)
{
    var x0 = Math.Clamp(cx - half, 0, w - 1);
    var y0 = Math.Clamp(cy - half, 0, h - 1);
    var cw = Math.Min(half * 2, w - x0);
    var ch = Math.Min(half * 2, h - y0);
    var dst = new SKBitmap(cw, ch, SKColorType.Rgba8888, SKAlphaType.Unpremul);
    using var c = new SKCanvas(dst);
    c.DrawBitmap(src, SKRect.Create(x0, y0, cw, ch), SKRect.Create(cw, ch));
    return dst;
}

Console.WriteLine("Crops…");
foreach (var (id, cx, cy, half) in crops)
{
    using var baseWater = Crop(waterFull, cx, cy, half);
    using var pdfC = Crop(pdfFull, cx, cy, half);
    using var nvC = Crop(nvdbFull, cx, cy, half);
    using var stack = Clone(baseWater);
    BlendOver(stack, pdfC, 210);
    BlendOver(stack, nvC, 230);
    using var canvas = new SKCanvas(stack);
    Label(canvas, "green=PDF  red=NVDB  blue=water", 10, 28);
    Label(canvas, id, 10, 52);
    Save(stack, Path.Combine(outDir, $"{id}.png"));
}

// --- Collage sheet ---
Console.WriteLine("Collage…");
var panels = new[]
{
    ("01 water+PDF", Path.Combine(outDir, "01-water-plus-pdf-roads.png")),
    ("02 water+NVDB", Path.Combine(outDir, "02-water-plus-nvdb.png")),
    ("03 PDF green / NVDB red", Path.Combine(outDir, "03-water-pdf-green-nvdb-red.png")),
    ("04 elev+NVDB", Path.Combine(outDir, "04-elevation-plus-nvdb.png")),
    ("05 diff", Path.Combine(outDir, "05-diff-pdf-green-nvdb-red-overlap-magenta.png")),
    ("crop A", Path.Combine(outDir, "crop-a-city-harbour.png")),
};

const int cols = 3;
var rows = (panels.Length + cols - 1) / cols;
const int pad = 12;
const int header = 36;
var cellW = tileW;
var cellH = tileH;
using var sheet = new SKBitmap(
    cols * cellW + (cols + 1) * pad,
    rows * (cellH + header) + (rows + 1) * pad + 40,
    SKColorType.Rgba8888,
    SKAlphaType.Unpremul);
using (var canvas = new SKCanvas(sheet))
{
    canvas.Clear(new SKColor(12, 12, 14));
    Label(canvas, "ArtillerySimulator layer align — green PDF roads, red NVDB, blue water, magenta overlap", 16, 28);
    for (var i = 0; i < panels.Length; i++)
    {
        var col = i % cols;
        var row = i / cols;
        var x = pad + col * (cellW + pad);
        var y = 44 + pad + row * (cellH + header + pad);
        using var tile = SKBitmap.Decode(panels[i].Item2);
        if (tile is null) continue;
        Label(canvas, panels[i].Item1, x, y + 20);
        canvas.DrawBitmap(tile, SKRect.Create(x, y + header, cellW, cellH));
    }
}
var collagePath = Path.Combine(outDir, "00-COLLAGE.png");
Save(sheet, collagePath);

// Stats: PDF vs NVDB nearest on primary samples
Console.WriteLine("Stats…");
var pdfPts = new List<(float X, float Z)>();
for (var y = 0; y < h; y += 3)
for (var x = 0; x < w; x += 3)
{
    var c = pdfFull.GetPixel(x, y);
    if (c.Alpha < 40) continue;
    if (c.Green < 200) continue; // primary lime
    pdfPts.Add((x / (float)(w - 1) * EastM, (1f - y / (float)(h - 1)) * NorthM));
}
var nvPts = new List<(float X, float Z)>();
foreach (var s in segs)
{
    if (!s.IsMain) continue;
    for (var i = 0; i < s.Points.Length; i += 2)
        nvPts.Add(s.Points[i]);
}

const float cell = 40f;
var grid = new Dictionary<(int, int), List<(float X, float Z)>>();
foreach (var p in nvPts)
{
    var k = ((int)MathF.Floor(p.X / cell), (int)MathF.Floor(p.Z / cell));
    if (!grid.TryGetValue(k, out var list)) grid[k] = list = new();
    list.Add(p);
}
double sd = 0, sx = 0, sz = 0;
var n = 0;
foreach (var p in pdfPts)
{
    var cx = (int)MathF.Floor(p.X / cell);
    var cz = (int)MathF.Floor(p.Z / cell);
    float best = 250f * 250f;
    float bx = 0, bz = 0;
    var hit = false;
    for (var dz = -2; dz <= 2; dz++)
    for (var dx = -2; dx <= 2; dx++)
    {
        if (!grid.TryGetValue((cx + dx, cz + dz), out var list)) continue;
        foreach (var q in list)
        {
            var ex = q.X - p.X;
            var ez = q.Z - p.Z;
            var d2 = ex * ex + ez * ez;
            if (d2 < best) { best = d2; bx = ex; bz = ez; hit = true; }
        }
    }
    if (!hit || best >= 250f * 250f) continue;
    sd += Math.Sqrt(best);
    sx += bx;
    sz += bz;
    n++;
}
var report = Path.Combine(outDir, "README.txt");
var meanDx = n == 0 ? 0 : sx / n;
var meanDz = n == 0 ? 0 : sz / n;
var meanD = n == 0 ? 0 : sd / n;
File.WriteAllText(report, string.Create(CultureInfo.InvariantCulture, $"""
ArtillerySimulator layer align diagnose
======================================
Water: PDF Norgeskart silhouette (pixel frame)
PDF roads: layers/roads.png (same pixel frame as water)
NVDB: nvdb-roads.bin placed with shared WGS84 equirectangular → meters

Legend
------
01: water + PDF roads (should lock if PDF georef is consistent)
02: water + NVDB (runtime roads)
03: water + PDF(green) + NVDB(red) — look for red/green separation
04: elevation preview + NVDB
05: magenta=overlap, red=NVDB-only, green=PDF-only

PDF↔NVDB primary match (n={n})
  mean |nvdb-pdf| = {meanD:F1} m
  mean (nvdb - pdf) = ({meanDx:F1}, {meanDz:F1}) m
  → positive X = NVDB east of PDF; positive Z = NVDB north of PDF

If 01 is tight but 02/03 show red west of green/coast → NVDB placement offset.
If 01 is also loose → water vs PDF roads disagree (unexpected).
If collage locks but Silk still drifts → runtime view / wrong bin loaded.
"""));
Console.WriteLine(File.ReadAllText(report));
Console.WriteLine($"Collage: {collagePath}");
return 0;
