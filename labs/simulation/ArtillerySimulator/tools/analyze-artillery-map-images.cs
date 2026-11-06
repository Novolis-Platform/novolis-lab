// Image analysis: reference Norgeskart PNG, hillshade relief, generated preview, ground-truth anchors.
//
//   dotnet run --file d:\novolis\novolis-lab\labs\simulation\ArtillerySimulator\tools\analyze-artillery-map-images.cs
//   dotnet run --file d:\novolis\novolis-lab\labs\simulation\ArtillerySimulator\tools\analyze-artillery-map-images.cs -- --write-alignment
//
#:package SkiaSharp@3.116.1
#:property TargetFramework=net10.0
#:property PublishAot=false
#:property PackAsTool=false
#:property ImplicitUsings=enable
#:property Nullable=enable
#:property ManagePackageVersionsCentrally=false

using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using SkiaSharp;

var writeAlignment = args.Contains("--write-alignment");
var root = ResolveRoot();
var mapDir = Path.Combine(root, "novolis-lab", "labs", "simulation", "ArtillerySimulator", "map");
var gameDir = Path.Combine(root, "novolis-lab", "labs", "simulation", "ArtillerySimulator", "Game");
var truthPath = Path.Combine(mapDir, "kristiansand-ground-truth.json");
var previewPath = Path.Combine(mapDir, "kristiansand-map-preview.png");
var referencePath = Path.Combine(mapDir, "kristiansand-reference.png");
var hillshadePath = Path.Combine(mapDir, "kristiansand-hillshade.jpg");
var cutoutPath = Path.Combine(mapDir, "kristiansand-norgeskart-cutout.json");
var reportPath = Path.Combine(mapDir, "kristiansand-map-analysis.txt");
var alignmentPath = Path.Combine(mapDir, "kristiansand-reference-alignment.json");

var cutout = LoadCutout(cutoutPath);
var truth = LoadGroundTruth(truthPath);
var sb = new StringBuilder();
sb.AppendLine($"Kristiansand map image analysis · {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC");
sb.AppendLine($"Scenario bbox: {cutout.South:F5}–{cutout.North:F5} N, {cutout.West:F5}–{cutout.East:F5} E");
sb.AppendLine();

ReportImageMeta(sb, "Reference (Norgeskart decorative)", referencePath);
ReportImageMeta(sb, "Hillshade (grey relief)", hillshadePath);
ReportImageMeta(sb, "Generated preview (NDH+OSM)", previewPath);
sb.AppendLine();

if (File.Exists(previewPath) && File.Exists(hillshadePath))
{
    var hillVsGen = CompareEdgeCorrelation(hillshadePath, previewPath, resizeSecondToFirst: false);
    sb.AppendLine($"Hillshade vs preview edge correlation (best scale search): {hillVsGen.BestScore:F4}");
    sb.AppendLine($"  Best scale={hillVsGen.BestScale:F3} offset=({hillVsGen.OffsetX:F3}, {hillVsGen.OffsetY:F3}) [normalized image coords]");
    sb.AppendLine();
}

if (File.Exists(previewPath) && File.Exists(referencePath))
{
    var refVsGen = CompareEdgeCorrelation(referencePath, previewPath, resizeSecondToFirst: true);
    sb.AppendLine($"Reference vs preview edge correlation: {refVsGen.BestScore:F4}");
    sb.AppendLine($"  Best scale={refVsGen.BestScale:F3} offset=({refVsGen.OffsetX:F3}, {refVsGen.OffsetY:F3})");
    sb.AppendLine("  (Low score expected until reference is re-exported to scenario WGS84 box.)");
    sb.AppendLine();

    if (writeAlignment)
    {
        var alignment = new ReferenceAlignmentJson
        {
            Citation = "Heuristic edge match: reference PNG → generated preview. Not a survey-grade georeference.",
            ReferenceFile = "kristiansand-reference.png",
            PreviewFile = "kristiansand-map-preview.png",
            EdgeCorrelation = refVsGen.BestScore,
            Scale = refVsGen.BestScale,
            OffsetX = refVsGen.OffsetX,
            OffsetY = refVsGen.OffsetY,
            ScenarioWgs84 = new Wgs84Box(cutout.South, cutout.North, cutout.West, cutout.East),
        };
        File.WriteAllText(
            alignmentPath,
            JsonSerializer.Serialize(alignment, new JsonSerializerOptions { WriteIndented = true }));
        sb.AppendLine($"Wrote alignment hint: {alignmentPath}");
        sb.AppendLine();
    }
}

var elevPath = Path.Combine(gameDir, "KristiansandElevation.cs");
if (File.Exists(elevPath) && truth is not null)
{
    var meters = TryLoadEmbeddedElevation(elevPath);
    if (meters is not null)
    {
        var meanLat = (cutout.South + cutout.North) / 2.0;
        var mPerDegLat = 111_132.0;
        var mPerDegLon = 111_320.0 * Math.Cos(meanLat * Math.PI / 180.0);
        var eastM = (cutout.East - cutout.West) * mPerDegLon;
        var northM = (cutout.North - cutout.South) * mPerDegLat;
        var count = (int)Math.Sqrt(meters.Length);
        sb.AppendLine("Embedded DEM vs ground-truth anchors:");
        foreach (var a in truth.Anchors)
        {
            var x = (float)((a.Longitude - cutout.West) / (cutout.East - cutout.West) * eastM);
            var z = (float)((a.Latitude - cutout.South) / (cutout.North - cutout.South) * northM);
            var sampled = SampleBilinear(meters, count, x, z, (float)eastM, (float)northM);
            var delta = sampled - a.ElevationMeters;
            sb.AppendLine(
                $"  {a.Id,-16} expected {a.ElevationMeters,4} m  sampled {sampled,7:F1} m  Δ {delta,6:F1} m  ({a.Name})");
        }

        sb.AppendLine();
        sb.AppendLine("Road elevation samples:");
        foreach (var r in truth.RoadSamples)
        {
            var x = (float)((r.Longitude - cutout.West) / (cutout.East - cutout.West) * eastM);
            var z = (float)((r.Latitude - cutout.South) / (cutout.North - cutout.South) * northM);
            var sampled = SampleBilinear(meters, count, x, z, (float)eastM, (float)northM);
            sb.AppendLine(
                $"  {r.RoadName,-6} expected {r.ElevationMeters,4} m  sampled {sampled,7:F1} m  Δ {sampled - r.ElevationMeters,6:F1} m  {r.Note}");
        }
    }
}

Directory.CreateDirectory(mapDir);
File.WriteAllText(reportPath, sb.ToString());
Console.Write(sb);
return 0;

static string ResolveRoot()
{
    var env = Environment.GetEnvironmentVariable("NOVOLIS_ROOT");
    if (!string.IsNullOrWhiteSpace(env))
        return env;
    var scripts = Path.GetDirectoryName(Environment.ProcessPath)!;
    if (scripts.EndsWith("scripts", StringComparison.OrdinalIgnoreCase))
        return Directory.GetParent(scripts)!.Parent!.FullName;
    return @"d:\novolis";
}

static CutoutBounds LoadCutout(string path)
{
    var json = JsonSerializer.Deserialize<CutoutJson>(File.ReadAllText(path))
        ?? throw new InvalidDataException($"Missing cutout: {path}");
    return new CutoutBounds(
        json.Wgs84!.South,
        json.Wgs84.North,
        json.Wgs84.West,
        json.Wgs84.East,
        json.Citation ?? "");
}

static GroundTruthFile? LoadGroundTruth(string path) =>
    File.Exists(path)
        ? JsonSerializer.Deserialize<GroundTruthFile>(File.ReadAllText(path))
        : null;

static void ReportImageMeta(StringBuilder sb, string label, string path)
{
    if (!File.Exists(path))
    {
        sb.AppendLine($"{label}: (missing) {path}");
        return;
    }

    using var bmp = SKBitmap.Decode(path);
    if (bmp is null)
    {
        sb.AppendLine($"{label}: decode failed");
        return;
    }

    var lum = SampleLuminanceGrid(bmp, 64, 64);
    var mean = lum.Average();
    var std = Math.Sqrt(lum.Select(v => (v - mean) * (v - mean)).Average());
    sb.AppendLine($"{label}: {bmp.Width}×{bmp.Height} px, mean lum={mean:F1} σ={std:F1}");
}

static CorrelationResult CompareEdgeCorrelation(string pathA, string pathB, bool resizeSecondToFirst)
{
    using var a = SKBitmap.Decode(pathA) ?? throw new InvalidDataException(pathA);
    using var bOrig = SKBitmap.Decode(pathB) ?? throw new InvalidDataException(pathB);
    using var b = resizeSecondToFirst ? ResizeTo(a.Width, a.Height, bOrig) : bOrig;
    var edgeA = SobelEdges(a, 256, 256);
    var edgeB = SobelEdges(b, 256, 256);
    var best = new CorrelationResult { BestScore = -1f };
    for (var scale = 0.75f; scale <= 1.35f; scale += 0.05f)
    {
        for (var ox = -0.15f; ox <= 0.15f; ox += 0.05f)
        {
            for (var oy = -0.15f; oy <= 0.15f; oy += 0.05f)
            {
                var score = Correlation(edgeA, edgeB, scale, ox, oy);
                if (score > best.BestScore)
                {
                    best.BestScore = score;
                    best.BestScale = scale;
                    best.OffsetX = ox;
                    best.OffsetY = oy;
                }
            }
        }
    }

    return best;
}

static SKBitmap ResizeTo(int w, int h, SKBitmap src)
{
    var info = new SKImageInfo(w, h);
    var dest = new SKBitmap(info);
    using var canvas = new SKCanvas(dest);
    canvas.DrawBitmap(src, new SKRect(0, 0, w, h));
    return dest;
}

static float[] SampleLuminanceGrid(SKBitmap bmp, int gw, int gh)
{
    var arr = new float[gw * gh];
    for (var j = 0; j < gh; j++)
    {
        var py = (j + 0.5f) / gh * bmp.Height;
        for (var i = 0; i < gw; i++)
        {
            var px = (i + 0.5f) / gw * bmp.Width;
            var c = bmp.GetPixel(
                Math.Clamp((int)px, 0, bmp.Width - 1),
                Math.Clamp((int)py, 0, bmp.Height - 1));
            arr[j * gw + i] = (c.Red + c.Green + c.Blue) / 3f;
        }
    }

    return arr;
}

static float[] SobelEdges(SKBitmap bmp, int gw, int gh)
{
    var lum = SampleLuminanceGrid(bmp, gw, gh);
    var edge = new float[gw * gh];
    for (var j = 1; j < gh - 1; j++)
    {
        for (var i = 1; i < gw - 1; i++)
        {
            float Gx =
                -lum[(j - 1) * gw + (i - 1)] + lum[(j - 1) * gw + (i + 1)]
                - 2 * lum[j * gw + (i - 1)] + 2 * lum[j * gw + (i + 1)]
                - lum[(j + 1) * gw + (i - 1)] + lum[(j + 1) * gw + (i + 1)];
            float Gz =
                -lum[(j - 1) * gw + (i - 1)] - 2 * lum[(j - 1) * gw + i] - lum[(j - 1) * gw + (i + 1)]
                + lum[(j + 1) * gw + (i - 1)] + 2 * lum[(j + 1) * gw + i] + lum[(j + 1) * gw + (i + 1)];
            edge[j * gw + i] = MathF.Sqrt(Gx * Gx + Gz * Gz);
        }
    }

    var max = edge.Max();
    if (max > 1e-6f)
    {
        for (var k = 0; k < edge.Length; k++)
            edge[k] /= max;
    }

    return edge;
}

static float Correlation(float[] a, float[] b, float scale, float offsetX, float offsetY)
{
    var gw = (int)Math.Sqrt(a.Length);
    var gh = a.Length / gw;
    var sumA = 0f;
    var sumB = 0f;
    var sumAB = 0f;
    var sumA2 = 0f;
    var sumB2 = 0f;
    var n = 0;
    for (var j = 0; j < gh; j++)
    {
        for (var i = 0; i < gw; i++)
        {
            var u = i / (float)(gw - 1);
            var v = j / (float)(gh - 1);
            var srcU = (u - 0.5f) / scale + 0.5f + offsetX;
            var srcV = (v - 0.5f) / scale + 0.5f + offsetY;
            if (srcU < 0f || srcU > 1f || srcV < 0f || srcV > 1f)
                continue;
            var bi = (int)Math.Clamp(srcU * (gw - 1), 0, gw - 1);
            var bj = (int)Math.Clamp(srcV * (gh - 1), 0, gh - 1);
            var av = a[j * gw + i];
            var bv = b[bj * gw + bi];
            sumA += av;
            sumB += bv;
            sumAB += av * bv;
            sumA2 += av * av;
            sumB2 += bv * bv;
            n++;
        }
    }

    if (n < 64)
        return -1f;
    var meanA = sumA / n;
    var meanB = sumB / n;
    var cov = sumAB / n - meanA * meanB;
    var varA = sumA2 / n - meanA * meanA;
    var varB = sumB2 / n - meanB * meanB;
    var denom = MathF.Sqrt(varA * varB);
    return denom < 1e-6f ? 0f : cov / denom;
}

static short[]? TryLoadEmbeddedElevation(string csPath)
{
    var text = File.ReadAllText(csPath);
    var start = text.IndexOf("Meters =", StringComparison.Ordinal);
    if (start < 0)
        return null;
    start = text.IndexOf('[', start);
    var end = text.IndexOf(']', start);
    if (start < 0 || end < 0)
        return null;
    var slice = text[(start + 1)..end];
    var parts = slice.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    var list = new List<short>(parts.Length);
    foreach (var p in parts)
    {
        if (short.TryParse(p, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v))
            list.Add(v);
    }

    return list.Count > 0 ? list.ToArray() : null;
}

static float SampleBilinear(short[] meters, int count, float x, float z, float eastM, float northM)
{
    var cells = count - 1;
    var u = Math.Clamp(x / eastM, 0f, 1f) * cells;
    var v = Math.Clamp(z / northM, 0f, 1f) * cells;
    var i0 = (int)MathF.Floor(u);
    var j0 = (int)MathF.Floor(v);
    var i1 = Math.Min(i0 + 1, cells);
    var j1 = Math.Min(j0 + 1, cells);
    var tx = u - i0;
    var tz = v - j0;
    float At(int i, int j) => meters[j * count + i];
    var h00 = At(i0, j0);
    var h10 = At(i1, j0);
    var h01 = At(i0, j1);
    var h11 = At(i1, j1);
    var south = h00 + (h10 - h00) * tx;
    var northH = h01 + (h11 - h01) * tx;
    return south + (northH - south) * tz;
}

readonly record struct CutoutBounds(double South, double North, double West, double East, string Citation);

sealed class CutoutJson
{
    [JsonPropertyName("citation")]
    public string? Citation { get; set; }

    [JsonPropertyName("wgs84")]
    public Wgs84Json? Wgs84 { get; set; }
}

sealed class Wgs84Json
{
    [JsonPropertyName("south")]
    public double South { get; set; }

    [JsonPropertyName("north")]
    public double North { get; set; }

    [JsonPropertyName("west")]
    public double West { get; set; }

    [JsonPropertyName("east")]
    public double East { get; set; }
}

sealed class GroundTruthFile
{
    [JsonPropertyName("anchors")]
    public List<GroundTruthAnchor> Anchors { get; set; } = [];

    [JsonPropertyName("roadSamples")]
    public List<GroundTruthRoad> RoadSamples { get; set; } = [];
}

sealed class GroundTruthAnchor
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("latitude")]
    public double Latitude { get; set; }

    [JsonPropertyName("longitude")]
    public double Longitude { get; set; }

    [JsonPropertyName("elevationMeters")]
    public float ElevationMeters { get; set; }
}

sealed class GroundTruthRoad
{
    [JsonPropertyName("roadName")]
    public string RoadName { get; set; } = "";

    [JsonPropertyName("latitude")]
    public double Latitude { get; set; }

    [JsonPropertyName("longitude")]
    public double Longitude { get; set; }

    [JsonPropertyName("elevationMeters")]
    public float ElevationMeters { get; set; }

    [JsonPropertyName("note")]
    public string? Note { get; set; }
}

sealed class ReferenceAlignmentJson
{
    [JsonPropertyName("citation")]
    public string Citation { get; set; } = "";

    [JsonPropertyName("referenceFile")]
    public string ReferenceFile { get; set; } = "";

    [JsonPropertyName("previewFile")]
    public string PreviewFile { get; set; } = "";

    [JsonPropertyName("edgeCorrelation")]
    public float EdgeCorrelation { get; set; }

    [JsonPropertyName("scale")]
    public float Scale { get; set; }

    [JsonPropertyName("offsetX")]
    public float OffsetX { get; set; }

    [JsonPropertyName("offsetY")]
    public float OffsetY { get; set; }

    [JsonPropertyName("scenarioWgs84")]
    public Wgs84Box ScenarioWgs84 { get; set; }
}

readonly record struct Wgs84Box(double South, double North, double West, double East);

struct CorrelationResult
{
    public float BestScore;
    public float BestScale;
    public float OffsetX;
    public float OffsetY;
}
