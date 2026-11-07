// Fetch NVDB car roads → map/layers/nvdb-roads.bin (sole road authority). Optional --write-debug-png.
// Placement: EPSG:25833 meters on the shared hoydedata / NHM frame (UTM SW origin), not PDF equirectangular.
//
//   dotnet run --file d:\novolis\novolis-lab\labs\simulation\ArtillerySimulator\scripts\fetch-nvdb-roads.cs
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

var writeDebugPng = args.Contains("--write-debug-png", StringComparer.OrdinalIgnoreCase);
var mapDir = @"d:\novolis\novolis-lab\labs\simulation\ArtillerySimulator\map";
var layersDir = Path.Combine(mapDir, "layers");
Directory.CreateDirectory(layersDir);

var stack = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(mapDir, "kristiansand-map-stack.json")));
var world = stack.RootElement.GetProperty("world");
var wgs = world.GetProperty("wgs84");
var west = wgs.GetProperty("west").GetDouble();
var south = wgs.GetProperty("south").GetDouble();
var east = wgs.GetProperty("east").GetDouble();
var north = wgs.GetProperty("north").GetDouble();
var eastMeters = (float)world.GetProperty("eastMeters").GetDouble();
var northMeters = (float)world.GetProperty("northMeters").GetDouble();

var (utmOriginEasting, utmOriginNorthing) = Etrs89Utm33.FrameOriginFromWgs84Box(
    south, west, north, east, eastMeters, northMeters);

Console.WriteLine(
    FormattableString.Invariant(
        $"EPSG:25833 frame E0={utmOriginEasting:F1} N0={utmOriginNorthing:F1}  {eastMeters:F0}×{northMeters:F0} m"));
Console.WriteLine(
    FormattableString.Invariant($"Fetch bbox WGS84 [{west:F5},{south:F5}]–[{east:F5},{north:F5}]"));

using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
var raw = await NvdbVegnett.FetchCarSegmentsAsync(http, west, south, east, north);
var segments = NvdbVegnett.ToScenarioSegments(raw, utmOriginEasting, utmOriginNorthing);
var outBin = Path.Combine(layersDir, "nvdb-roads.bin");
NvdbRoadsBinary.Write(outBin, segments);

var main = segments.Count(static s => s.IsMain);
var local = segments.Count - main;
Console.WriteLine($"Wrote {outBin}  main={main:N0} local={local:N0} total={segments.Count:N0}");

foreach (var g in segments.GroupBy(s => NvdbRoadHierarchy.DecodeKategori(s.Vegkategori)))
    Console.WriteLine($"  vegkategori {g.Key}: {g.Count():N0}");

var georefPath = Path.Combine(layersDir, "georef.json");
if (File.Exists(georefPath))
{
    using var geoDoc = JsonDocument.Parse(await File.ReadAllTextAsync(georefPath));
    var root = geoDoc.RootElement.Clone();
    await using var ms = new MemoryStream();
    await using (var writer = new Utf8JsonWriter(ms, new JsonWriterOptions { Indented = true }))
    {
        writer.WriteStartObject();
        foreach (var prop in root.EnumerateObject())
        {
            if (prop.Name is "UtmOriginEasting" or "UtmOriginNorthing" or "Placement")
                continue;
            prop.WriteTo(writer);
        }
        writer.WriteNumber("UtmOriginEasting", utmOriginEasting);
        writer.WriteNumber("UtmOriginNorthing", utmOriginNorthing);
        writer.WriteString("Placement", "epsg25833_utm_frame_hoydedata");
        writer.WriteEndObject();
    }
    await File.WriteAllBytesAsync(georefPath, ms.ToArray());
    Console.WriteLine($"Updated {georefPath} with UTM origin + placement");
}

if (writeDebugPng)
{
    const int w = 1200;
    var h = Math.Max(1, (int)Math.Round(w * (northMeters / eastMeters)));
    using var bmp = new SKBitmap(w, h, SKColorType.Rgba8888, SKAlphaType.Unpremul);
    using var canvas = new SKCanvas(bmp);
    canvas.Clear(SKColors.Transparent);
    using var localPaint = new SKPaint
    {
        Color = new SKColor(168, 168, 168, 210),
        IsAntialias = true,
        Style = SKPaintStyle.Stroke,
        StrokeWidth = 1.15f,
        StrokeCap = SKStrokeCap.Round,
    };
    using var mainPaint = new SKPaint
    {
        Color = new SKColor(210, 35, 42, 255),
        IsAntialias = true,
        Style = SKPaintStyle.Stroke,
        StrokeWidth = 2.6f,
        StrokeCap = SKStrokeCap.Round,
    };
    void Draw(NvdbRoadsBinary.Segment seg, SKPaint paint)
    {
        using var path = new SKPath();
        for (var i = 0; i < seg.Points.Length; i++)
        {
            var px = seg.Points[i].X / eastMeters * (w - 1);
            var py = (1f - seg.Points[i].Z / northMeters) * (h - 1);
            if (i == 0) path.MoveTo(px, py);
            else path.LineTo(px, py);
        }

        canvas.DrawPath(path, paint);
    }

    foreach (var s in segments.Where(static s => !s.IsMain))
        Draw(s, localPaint);
    foreach (var s in segments.Where(static s => s.IsMain))
        Draw(s, mainPaint);

    var pngPath = Path.Combine(layersDir, "roads-debug.png");
    using var encoded = SKImage.FromBitmap(bmp).Encode(SKEncodedImageFormat.Png, 90);
    await File.WriteAllBytesAsync(pngPath, encoded.ToArray());
    Console.WriteLine($"Debug PNG → {pngPath}");
}

return 0;
