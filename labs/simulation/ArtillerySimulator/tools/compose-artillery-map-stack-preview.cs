// Layer stack QA: relief + roads overlay + land/sea mask (north-up).
//
//   dotnet run --file d:\novolis\novolis-lab\labs\simulation\ArtillerySimulator\tools\compose-artillery-map-stack-preview.cs
//
#:package SkiaSharp@3.116.1
#:property TargetFramework=net10.0
#:property PublishAot=false
#:property PackAsTool=false
#:property ImplicitUsings=enable
#:property Nullable=enable
#:property ManagePackageVersionsCentrally=false

using System.Text.Json;
using SkiaSharp;

var root = ResolveRoot();
var mapDir = Path.Combine(root, "novolis-lab", "labs", "simulation", "ArtillerySimulator", "map");
var stackPath = Path.Combine(mapDir, "kristiansand-map-stack.json");
var outPath = Path.Combine(mapDir, "kristiansand-map-stack-preview.png");

var reliefPath = Path.Combine(mapDir, "kristiansand-reference.png");
var roadsPath = Path.Combine(mapDir, "kristiansand-roads-reference.png");
var maskPath = Path.Combine(mapDir, "kristiansand-land-sea-mask.png");
var gamePreview = Path.Combine(mapDir, "kristiansand-map-preview.png");

using var relief = SKBitmap.Decode(reliefPath) ?? throw new FileNotFoundException(reliefPath);
using var roads = File.Exists(roadsPath) ? SKBitmap.Decode(roadsPath) : null;
using var mask = File.Exists(maskPath) ? SKBitmap.Decode(maskPath) : null;

if (roads is not null)
{
    var compositePath = Path.Combine(mapDir, "kristiansand-relief-roads-composite.png");
    using var roadsAligned = roads.Width == relief.Width && roads.Height == relief.Height
        ? roads.Copy()
        : Resize(roads, relief.Width, relief.Height);
    using var composite = new SKBitmap(relief.Width, relief.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
    using (var c = new SKCanvas(composite))
    {
        c.DrawBitmap(relief, 0, 0);
        using var paint = new SKPaint { Color = new SKColor(255, 60, 60, 110), BlendMode = SKBlendMode.SrcOver };
        c.DrawBitmap(roadsAligned, 0, 0, paint);
    }

    using var compImage = SKImage.FromBitmap(composite);
    using var compData = compImage.Encode(SKEncodedImageFormat.Png, 92);
    await File.WriteAllBytesAsync(compositePath, compData.ToArray());
    Console.WriteLine($"Wrote single-sheet composite: {compositePath} ({compData.Size} bytes)");
}

var panelW = relief.Width;
var panelH = relief.Height;
var legendH = 72;
var totalH = panelH * 2 + legendH * 3;

using var surface = SKSurface.Create(new SKImageInfo(panelW, totalH));
var canvas = surface.Canvas;
canvas.Clear(new SKColor(28, 32, 40));

var stackLine = File.Exists(stackPath)
    ? ReadStackSummary(stackPath)
    : "(no map stack json)";

DrawLabel(canvas, 12, 18, "Kristiansand map stack — north up, shared corner markers", 22);
DrawLabel(canvas, 12, 44, stackLine, 14);

var y0 = legendH;
DrawPanel(canvas, 0, y0, panelW, panelH, relief, null, "Relief (cropped PDF tiles)");

if (roads is not null)
{
    using var roadsScaled = roads.Width == relief.Width && roads.Height == relief.Height
        ? roads.Copy()
        : Resize(roads, relief.Width, relief.Height);
    var y1 = y0 + panelH + legendH;
    DrawPanel(canvas, 0, y1, panelW, panelH, relief, roadsScaled, "Relief + roads layer (50% red tint, stack-aligned resize)");
}

if (mask is not null)
{
    using var maskScaled = mask.Width == relief.Width && mask.Height == relief.Height
        ? mask.Copy()
        : Resize(mask, relief.Width, relief.Height);
    var y2 = y0 + (panelH + legendH) * (roads is not null ? 2 : 1);
    DrawPanel(canvas, 0, y2, panelW, panelH, maskScaled, null, "Land/sea mask (blue = water)");
}

using var image = surface.Snapshot();
using var data = image.Encode(SKEncodedImageFormat.Png, 92);
await File.WriteAllBytesAsync(outPath, data.ToArray());
Console.WriteLine($"Wrote {outPath} ({data.Size} bytes)");

if (File.Exists(gamePreview))
    Console.WriteLine($"Game DEM/OSM preview: {gamePreview}");

return 0;

static string ReadStackSummary(string path)
{
    using var doc = JsonDocument.Parse(File.ReadAllText(path));
    var w = doc.RootElement.GetProperty("world");
    var east = w.GetProperty("eastMeters").GetDouble();
    var north = w.GetProperty("northMeters").GetDouble();
    var layers = doc.RootElement.GetProperty("layers").GetArrayLength();
    return $"{layers} layers · {east:F0} m E × {north:F0} m N · extent: {w.GetProperty("extentSource").GetString()}";
}

static void DrawPanel(SKCanvas canvas, int x, int y, int w, int h, SKBitmap baseBmp, SKBitmap? overlay, string caption)
{
    canvas.DrawBitmap(baseBmp, x, y);
    if (overlay is not null)
    {
        using var paint = new SKPaint { Color = new SKColor(255, 60, 60, 110), BlendMode = SKBlendMode.SrcOver };
        canvas.DrawBitmap(overlay, x, y, paint);
    }

    using var border = new SKPaint { Color = new SKColor(120, 160, 220), Style = SKPaintStyle.Stroke, StrokeWidth = 2 };
    canvas.DrawRect(x, y, w, h, border);
    DrawLabel(canvas, x + 8, y + 22, caption, 16);
}

static void DrawLabel(SKCanvas canvas, float x, float y, string text, float size)
{
    using var paint = new SKPaint
    {
        Color = SKColors.White,
        TextSize = size,
        IsAntialias = true,
        Typeface = SKTypeface.FromFamilyName("Segoe UI") ?? SKTypeface.Default,
    };
    paint.Style = SKPaintStyle.Fill;
    canvas.DrawText(text, x, y, paint);
}

static SKBitmap Resize(SKBitmap src, int w, int h)
{
    var dest = new SKBitmap(w, h);
    using var canvas = new SKCanvas(dest);
    canvas.DrawBitmap(src, new SKRect(0, 0, w, h));
    return dest;
}

static string ResolveRoot()
{
    var env = Environment.GetEnvironmentVariable("NOVOLIS_ROOT");
    if (!string.IsNullOrWhiteSpace(env))
        return env;
    return @"d:\novolis";
}
