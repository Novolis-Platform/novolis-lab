// Fetch Kartverket/OSM (or Open-Meteo) DEM + vectors for Kristiansand; emit C# + map rasters beside this app.
//
//   dotnet run --file d:\novolis\novolis-lab\labs\simulation\ArtillerySimulator\tools\extract-artillery-map.cs -- --scenario kristiansand
//   dotnet run --file d:\novolis\novolis-lab\labs\simulation\ArtillerySimulator\tools\extract-artillery-map.cs -- --hillshade d:\novolis\novolis-lab\labs\simulation\ArtillerySimulator\map\kristiansand-hillshade.jpg
//   dotnet run --file d:\novolis\novolis-lab\labs\simulation\ArtillerySimulator\tools\extract-artillery-map.cs -- --open-meteo
//
#:package BitMiracle.LibTiff.NET@2.4.649
#:package ProjNet@2.0.0
#:package SkiaSharp@3.116.1
#:property TargetFramework=net10.0
#:property PublishAot=false
#:property PackAsTool=false
#:property ImplicitUsings=enable
#:property Nullable=enable
#:property ManagePackageVersionsCentrally=false

using System.Globalization;
using System.Numerics;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using BitMiracle.LibTiff.Classic;
using ProjNet.CoordinateSystems;
using ProjNet.CoordinateSystems.Transformations;
using SkiaSharp;

const int GridCount = 81;
const string Scenario = "kristiansand";

var argsList = args.ToList();
var scenario = Scenario;
var cutoutPath = Path.Combine(
    ResolveRoot(),
    "novolis-lab",
    "labs",
    "simulation",
    "ArtillerySimulator",
    "map",
    "kristiansand-norgeskart-cutout.json");
string? hillshadePath = null;
string? fuseHillshadePath = null;
string? groundTruthPath = null;
var useOpenMeteo = argsList.Contains("--open-meteo");
var fuseHillshade = argsList.Contains("--fuse-hillshade");
for (var i = 0; i < argsList.Count - 1; i++)
{
    if (argsList[i] == "--scenario")
        scenario = argsList[i + 1];
    if (argsList[i] == "--cutout")
        cutoutPath = Path.GetFullPath(argsList[i + 1]);
    if (argsList[i] == "--hillshade")
        hillshadePath = Path.GetFullPath(argsList[i + 1]);
    if (argsList[i] == "--fuse-hillshade" && i + 1 < argsList.Count && !argsList[i + 1].StartsWith('-'))
        fuseHillshadePath = Path.GetFullPath(argsList[i + 1]);
    if (argsList[i] == "--ground-truth")
        groundTruthPath = Path.GetFullPath(argsList[i + 1]);
}

var cutout = LoadCutout(cutoutPath);
var south = cutout.South;
var north = cutout.North;
var west = cutout.West;
var east = cutout.East;
Console.WriteLine($"BBox from {cutoutPath}");
Console.WriteLine($"  {south:F5}–{north:F5} N, {west:F5}–{east:F5} E ({cutout.Citation})");

if (!string.Equals(scenario, "kristiansand", StringComparison.OrdinalIgnoreCase))
{
    Console.Error.WriteLine("Only --scenario kristiansand is implemented.");
    return 1;
}

var root = ResolveRoot();
var outDir = Path.Combine(root, "novolis-lab", "labs", "simulation", "ArtillerySimulator", "Game");
Directory.CreateDirectory(outDir);

using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(3) };
http.DefaultRequestHeaders.UserAgent.ParseAdd("Novolis-ArtilleryMapExtract/1.0 (contact: maps@novolis.platform)");

string elevationSource;
short[] meters;
if (!string.IsNullOrWhiteSpace(hillshadePath))
{
    Console.WriteLine($"Sampling hillshade DEM from {hillshadePath}...");
    meters = HillshadeDemExtract.Sample(hillshadePath, GridCount);
    elevationSource = $"Norgeskart hillshade raster {Path.GetFileName(hillshadePath)}, luminance + water mask, aligned to cutout WGS84";
    Console.WriteLine($"  {meters.Length} samples (min {meters.Min()} m, max {meters.Max()} m)");
}
else if (useOpenMeteo)
{
    Console.WriteLine("Fetching elevation grid (Open-Meteo)...");
    meters = await FetchElevationAsync(http, GridCount, south, north, west, east);
    elevationSource = "Open-Meteo elevation API (7×7 coarse grid bilinear upsample)";
    Console.WriteLine($"  {meters.Length} samples");
}
else
{
    Console.WriteLine("Fetching Kartverket NDH DTM (NHM_DTM_25833, 1 m)...");
    meters = await KartverketNdhExtract.SampleAsync(http, cutout, GridCount);
    elevationSource = "Kartverket Nasjonal detaljert høydemodell (NDH) DTM 1 m, EPSG:25833, hoydedata.no ImageServer export";
    Console.WriteLine($"  {meters.Length} samples (min {meters.Min()} m, max {meters.Max()} m)");
}

Console.WriteLine("Fetching OSM ways...");
var osm = await FetchOverpassAsync(http, south, north, west, east);
Console.WriteLine($"  {osm.Elements.Count} elements");

var meanLat = (south + north) / 2.0;
var mPerDegLat = 111_132.0;
var mPerDegLon = 111_320.0 * Math.Cos(meanLat * Math.PI / 180.0);
var eastMeters = (east - west) * mPerDegLon;
var northMeters = (north - south) * mPerDegLat;
var extent = Math.Max(eastMeters, northMeters);

var mapDirEarly = Path.Combine(root, "novolis-lab", "labs", "simulation", "ArtillerySimulator", "map");
if (TryLoadMapStack(mapDirEarly, ref south, ref north, ref west, ref east, ref eastMeters, ref northMeters))
{
    extent = Math.Max(eastMeters, northMeters);
    Console.WriteLine($"Map stack: scenario {northMeters:F1} m N × {eastMeters:F1} m E (north-up, marker corners)");
}
groundTruthPath ??= Path.Combine(mapDirEarly, "kristiansand-ground-truth.json");
if (fuseHillshade || fuseHillshadePath is not null)
{
    fuseHillshadePath ??= Path.Combine(mapDirEarly, "kristiansand-hillshade.jpg");
    if (File.Exists(fuseHillshadePath))
    {
        Console.WriteLine($"Fusing NDH with hillshade relief {fuseHillshadePath}...");
        meters = DemGroundTruthFusion.FuseHillshade(meters, GridCount, fuseHillshadePath);
        elevationSource += $"; hillshade fusion ({Path.GetFileName(fuseHillshadePath)})";
    }
    else
        Console.WriteLine($"  Hillshade fusion skipped (missing {fuseHillshadePath})");
}

if (File.Exists(groundTruthPath))
{
    Console.WriteLine($"Applying ground-truth anchors from {groundTruthPath}...");
    var truth = DemGroundTruthFusion.LoadGroundTruth(groundTruthPath);
    var report = DemGroundTruthFusion.ApplyAnchorCorrections(
        meters,
        GridCount,
        (float)eastMeters,
        (float)northMeters,
        west,
        east,
        south,
        north,
        truth);
    elevationSource += "; ground-truth IDW anchor correction";
    foreach (var line in report)
        Console.WriteLine($"  {line}");
}
else
    Console.WriteLine($"  No ground-truth file at {groundTruthPath} (skipped anchor correction)");

var polylines = BuildPolylines(osm, eastMeters, northMeters, west, east, south, north);
var waterPolys = BuildWaterPolygons(osm, eastMeters, northMeters, west, east, south, north, eastMeters * northMeters);
Console.WriteLine($"  {polylines.Count} polylines, {waterPolys.Count} water polygons");

var date = DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
var referencePath = Path.Combine(mapDirEarly, "kristiansand-reference.png");
ReferenceWaterMaskFrame? landSeaFrame = null;
byte[]? landSeaMask = null;
if (File.Exists(referencePath))
{
    Console.WriteLine($"Building pixel land/sea mask (#5182FF) from {referencePath}...");
    var stackCornerGeoref = File.Exists(Path.Combine(mapDirEarly, "kristiansand-map-stack.json"));
    (landSeaMask, landSeaFrame) = ReferenceWaterMaskExtract.Build(
        referencePath,
        (float)eastMeters,
        (float)northMeters,
        stackCornerGeoref);
    var waterCells = landSeaMask.Count(b => b != 0);
    var frame = landSeaFrame.Value;
    Console.WriteLine(
        $"  {frame.ImageWidth}×{frame.ImageHeight} px, ppm={frame.PixelsPerMeter:F4}, letterbox=({frame.OffsetPxX:F1},{frame.OffsetPxY:F1}): {waterCells} water px, {landSeaMask.Length - waterCells} land");
    var maskBin = Path.Combine(mapDirEarly, "kristiansand-land-sea-mask.bin");
    WriteLandSeaMaskBinary(maskBin, frame, landSeaMask);
    WriteLandSeaMaskGeorefJson(
        Path.Combine(mapDirEarly, "kristiansand-reference-georef.json"),
        frame,
        "kristiansand-reference.png");
    WriteLandSeaMaskPreviewPng(
        Path.Combine(mapDirEarly, "kristiansand-land-sea-mask.png"),
        landSeaMask,
        frame.ImageWidth,
        frame.ImageHeight);
}
else
    Console.WriteLine($"  Land/sea mask skipped (missing {referencePath})");

WriteElevation(outDir, meters, date, south, north, west, east, elevationSource);
WriteHeightSampler(outDir, eastMeters, northMeters, extent, south, north, west, east);
WriteMapFeatures(outDir, polylines, waterPolys, date, eastMeters, northMeters, south, north, west, east, cutout.Citation);

var mapDir = Path.Combine(root, "novolis-lab", "labs", "simulation", "ArtillerySimulator", "map");
var previewPath = Path.Combine(mapDir, "kristiansand-map-preview.png");
WriteMapPreviewPng(
    previewPath,
    meters,
    GridCount,
    polylines,
    waterPolys,
    (float)eastMeters,
    (float)northMeters,
    landSeaMask,
    landSeaFrame);
var comparePath = Path.Combine(mapDir, "kristiansand-map-compare.png");
WriteMapComparePng(comparePath, previewPath, mapDir);
Console.WriteLine($"  Map preview PNG: {previewPath}");
Console.WriteLine($"  Map compare PNG: {comparePath}");

Console.WriteLine($"Wrote Kristiansand map sources to {outDir}");
Console.WriteLine(
    "Image / anchor QA: dotnet run --file d:\\novolis\\novolis-lab\\labs\\simulation\\ArtillerySimulator\\tools\\analyze-artillery-map-images.cs -- --write-alignment");
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

static bool TryLoadMapStack(
    string mapDir,
    ref double south,
    ref double north,
    ref double west,
    ref double east,
    ref double eastMeters,
    ref double northMeters)
{
    var path = Path.Combine(mapDir, "kristiansand-map-stack.json");
    if (!File.Exists(path))
        return false;

    try
    {
        using var stream = File.OpenRead(path);
        using var doc = JsonDocument.Parse(stream);
        if (!doc.RootElement.TryGetProperty("world", out var world))
            return false;

        eastMeters = world.GetProperty("eastMeters").GetDouble();
        northMeters = world.GetProperty("northMeters").GetDouble();
        if (world.TryGetProperty("wgs84", out var wgs))
        {
            south = wgs.GetProperty("south").GetDouble();
            north = wgs.GetProperty("north").GetDouble();
            west = wgs.GetProperty("west").GetDouble();
            east = wgs.GetProperty("east").GetDouble();
        }

        return eastMeters > 0 && northMeters > 0;
    }
    catch
    {
        return false;
    }
}

static async Task<ElevationResponse> GetElevationWithRetryAsync(HttpClient http, string url)
{
    for (var attempt = 0; attempt < 8; attempt++)
    {
        using var resp = await http.GetAsync(url);
        if ((int)resp.StatusCode == 429)
        {
            await Task.Delay(1500 * (attempt + 1));
            continue;
        }

        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<ElevationResponse>())!;
    }

    throw new InvalidOperationException("Elevation API rate limited after retries.");
}

static async Task<short[]> FetchElevationAsync(HttpClient http, int count, double south, double north, double west, double east)
{
    const int coarse = 7;
    var coarseHeights = new double[coarse * coarse];
    var lats = new List<string>();
    var lons = new List<string>();
    for (var j = 0; j < coarse; j++)
    {
        var lat = south + (north - south) * j / (coarse - 1);
        for (var i = 0; i < coarse; i++)
        {
            var lon = west + (east - west) * i / (coarse - 1);
            lats.Add(lat.ToString("F6", CultureInfo.InvariantCulture));
            lons.Add(lon.ToString("F6", CultureInfo.InvariantCulture));
        }
    }

    var url = $"https://api.open-meteo.com/v1/elevation?latitude={string.Join(',', lats)}&longitude={string.Join(',', lons)}";
    var doc = await GetElevationWithRetryAsync(http, url);
    if (doc.Elevation is null || doc.Elevation.Length != coarse * coarse)
        throw new InvalidOperationException("Coarse elevation fetch failed.");

    for (var k = 0; k < coarseHeights.Length; k++)
        coarseHeights[k] = doc.Elevation[k];

    var result = new short[count * count];
    for (var j = 0; j < count; j++)
    {
        var v = j / (double)(count - 1) * (coarse - 1);
        var j0 = (int)Math.Floor(v);
        var j1 = Math.Min(j0 + 1, coarse - 1);
        var tz = v - j0;
        for (var i = 0; i < count; i++)
        {
            var u = i / (double)(count - 1) * (coarse - 1);
            var i0 = (int)Math.Floor(u);
            var i1 = Math.Min(i0 + 1, coarse - 1);
            var tx = u - i0;
            var h00 = coarseHeights[j0 * coarse + i0];
            var h10 = coarseHeights[j0 * coarse + i1];
            var h01 = coarseHeights[j1 * coarse + i0];
            var h11 = coarseHeights[j1 * coarse + i1];
            var hSouth = h00 + (h10 - h00) * tx;
            var hNorth = h01 + (h11 - h01) * tx;
            var h = hSouth + (hNorth - hSouth) * tz;
            result[j * count + i] = (short)Math.Round(h);
        }
    }

    return result;
}

static async Task<OverpassResult> FetchOverpassAsync(HttpClient http, double south, double north, double west, double east)
{
    var bbox = FormattableString.Invariant($"{south},{west},{north},{east}");
    var query = FormattableString.Invariant($"""
        [out:json][timeout:120];
        (
          way["highway"~"^(motorway|trunk|primary|secondary|tertiary)"]({bbox});
          way["waterway"]({bbox});
          way["natural"="water"]({bbox});
          way["natural"="coastline"]({bbox});
        );
        out body;
        >;
        out skel qt;
        """);

    string[] endpoints =
    [
        "https://overpass-api.de/api/interpreter",
        "https://overpass.kumi.systems/api/interpreter",
    ];

    foreach (var endpoint in endpoints)
    {
        for (var attempt = 0; attempt < 4; attempt++)
        {
            using var content = new FormUrlEncodedContent(new Dictionary<string, string> { ["data"] = query });
            using var resp = await http.PostAsync(endpoint, content);
            if ((int)resp.StatusCode is 502 or 503 or 429)
            {
                await Task.Delay(2000 * (attempt + 1));
                continue;
            }

            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<OverpassResult>(json) ?? new OverpassResult();
        }
    }

    throw new InvalidOperationException("Overpass API unavailable after retries.");
}

static Vector2 Project(double lat, double lon, double eastM, double northM, double west, double east, double south, double north)
{
    var x = (float)((lon - west) / (east - west) * eastM);
    var z = (float)((lat - south) / (north - south) * northM);
    return new Vector2(x, z);
}

static List<PolylineRecord> BuildPolylines(
    OverpassResult osm,
    double eastM,
    double northM,
    double west,
    double east,
    double south,
    double north)
{
    var nodes = osm.Elements.Where(e => e.Type == "node" && e.Lat is not null && e.Lon is not null)
        .ToDictionary(e => e.Id, e => Project(e.Lat!.Value, e.Lon!.Value, eastM, northM, west, east, south, north));

    var list = new List<PolylineRecord>();
    foreach (var way in osm.Elements.Where(e => e.Type == "way" && e.Nodes is { Count: >= 2 }))
    {
        var tags = way.Tags ?? new Dictionary<string, string>();
        string kind;
        string name;
        if (tags.TryGetValue("natural", out var nat) && nat == "coastline")
        {
            kind = "coastline";
            name = tags.GetValueOrDefault("name") ?? "coastline";
        }
        else if (tags.TryGetValue("waterway", out var ww))
        {
            kind = "river";
            name = tags.GetValueOrDefault("name") ?? ww;
        }
        else if (tags.ContainsKey("highway"))
        {
            kind = "road";
            name = tags.GetValueOrDefault("name") ?? tags.GetValueOrDefault("ref") ?? tags["highway"];
        }
        else
            continue;

        var pts = new List<Vector2>();
        foreach (var nid in way.Nodes!)
        {
            if (nodes.TryGetValue(nid, out var p))
                pts.Add(p);
        }

        if (pts.Count < 2)
            continue;

        var length = 0f;
        for (var i = 1; i < pts.Count; i++)
            length += Vector2.Distance(pts[i - 1], pts[i]);

        if (length < 400f && kind == "road")
            continue;
        if (length < 200f && kind == "coastline")
            continue;

        list.Add(new PolylineRecord(name, kind, pts.ToArray(), length));
    }

    return list.OrderByDescending(p => p.Length).Take(96).ToList();
}

static List<PolygonRecord> BuildWaterPolygons(OverpassResult osm, double eastM, double northM, double west, double east, double south, double north, double mapArea)
{
    var nodes = osm.Elements.Where(e => e.Type == "node" && e.Lat is not null && e.Lon is not null)
        .ToDictionary(e => e.Id, e => Project(e.Lat!.Value, e.Lon!.Value, eastM, northM, west, east, south, north));

    var list = new List<PolygonRecord>();
    foreach (var way in osm.Elements.Where(e => e.Type == "way" && e.Nodes is { Count: >= 3 }))
    {
        var tags = way.Tags ?? new Dictionary<string, string>();
        if (!tags.TryGetValue("natural", out var natural) || natural != "water")
            continue;

        var pts = new List<Vector2>();
        foreach (var nid in way.Nodes!)
        {
            if (nodes.TryGetValue(nid, out var p))
                pts.Add(p);
        }

        if (pts.Count < 3)
            continue;

        var area = PolygonArea(pts);
        if (area < 15_000f)
            continue;
        if (area > mapArea * 0.35f)
            continue;

        var name = tags.GetValueOrDefault("name") ?? "water";
        list.Add(new PolygonRecord(name, pts.ToArray(), area));
    }

    return list.OrderByDescending(p => p.Area).Take(32).ToList();
}

static CutoutBounds LoadCutout(string path)
{
    if (!File.Exists(path))
        throw new FileNotFoundException($"Norgeskart cutout JSON not found: {path}");

    var json = JsonSerializer.Deserialize<CutoutJson>(
        File.ReadAllText(path),
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
        ?? throw new InvalidDataException($"Invalid cutout JSON: {path}");
    if (json.Wgs84.North <= json.Wgs84.South || json.Wgs84.East <= json.Wgs84.West)
        throw new InvalidDataException($"Cutout WGS84 box is invalid in {path}");
    var south = json.Wgs84.South;
    var north = json.Wgs84.North;
    var west = json.Wgs84.West;
    var east = json.Wgs84.East;
    var citation = json.Citation ?? path;

    var (utmWest, utmSouth, utmEast, utmNorth) = Wgs84BoundsToUtm25833(south, west, north, east);
    return new CutoutBounds(south, north, west, east, citation, utmWest, utmSouth, utmEast, utmNorth);
}

static (double West, double South, double East, double North) Wgs84BoundsToUtm25833(
    double south,
    double west,
    double north,
    double east)
{
    var wgs84 = GeographicCoordinateSystem.WGS84;
    var utm = ProjectedCoordinateSystem.WGS84_UTM(33, true);
    var toUtm = new CoordinateTransformationFactory().CreateFromCoordinateSystems(wgs84, utm);
    double minE = double.PositiveInfinity;
    double minN = double.PositiveInfinity;
    double maxE = double.NegativeInfinity;
    double maxN = double.NegativeInfinity;
    foreach (var (lon, lat) in new (double, double)[]
             {
                 (west, south),
                 (east, south),
                 (west, north),
                 (east, north),
             })
    {
        var p = toUtm.MathTransform.Transform(new[] { lon, lat });
        minE = Math.Min(minE, p[0]);
        maxE = Math.Max(maxE, p[0]);
        minN = Math.Min(minN, p[1]);
        maxN = Math.Max(maxN, p[1]);
    }

    return (minE, minN, maxE, maxN);
}

static float PolygonArea(List<Vector2> pts)
{
    double a = 0;
    for (var i = 0; i < pts.Count; i++)
    {
        var j = (i + 1) % pts.Count;
        a += pts[i].X * pts[j].Y - pts[j].X * pts[i].Y;
    }

    return (float)Math.Abs(a * 0.5);
}

static void WriteElevation(string dir, short[] meters, string date, double south, double north, double west, double east, string elevationSource)
{
    var sb = new StringBuilder();
    sb.AppendLine("namespace ArtillerySimulator.Game;");
    sb.AppendLine();
    sb.AppendLine("/// <summary>");
    sb.AppendLine($"/// Copernicus DEM elevations for {south:F2}–{north:F2} N, {west:F2}–{east:F2} E (Kristiansand).");
    sb.AppendLine($"/// {elevationSource}. Generated {date}. {GridCount} by {GridCount}, row-major, south to north,");
    sb.AppendLine("/// west to east within each row. Values are meters from that source.");
    sb.AppendLine("/// </summary>");
    sb.AppendLine("internal static class KristiansandElevation");
    sb.AppendLine("{");
    sb.AppendLine($"    public const int Count = {GridCount};");
    sb.AppendLine();
    sb.AppendLine("    public static readonly short[] Meters =");
    sb.AppendLine("    [");
    for (var j = 0; j < GridCount; j++)
    {
        sb.Append("        ");
        for (var i = 0; i < GridCount; i++)
        {
            sb.Append(meters[j * GridCount + i].ToString(CultureInfo.InvariantCulture));
            sb.Append(i == GridCount - 1 ? ',' : ", ");
        }

        sb.AppendLine();
    }

    sb.AppendLine("    ];");
    sb.AppendLine("}");
    File.WriteAllText(Path.Combine(dir, "KristiansandElevation.cs"), sb.ToString());
}

static void WriteHeightSampler(string dir, double eastM, double northM, double extent, double south, double north, double west, double east)
{
    var sb = new StringBuilder();
    sb.AppendLine("using System.Numerics;");
    sb.AppendLine("using Novolis.Physics.Abstractions;");
    sb.AppendLine();
    sb.AppendLine("namespace ArtillerySimulator.Game;");
    sb.AppendLine();
    sb.AppendLine("/// <summary>");
    sb.AppendLine("/// Bilinear sampler over <see cref=\"KristiansandElevation\"/>. World X is east, world Z is north, origin at the southwest corner.");
    sb.AppendLine("/// </summary>");
    sb.AppendLine("internal sealed class KristiansandHeightSampler : IHeightSampler");
    sb.AppendLine("{");
    sb.AppendLine($"    public const double SouthLatitude = {south.ToString(CultureInfo.InvariantCulture)};");
    sb.AppendLine($"    public const double NorthLatitude = {north.ToString(CultureInfo.InvariantCulture)};");
    sb.AppendLine($"    public const double WestLongitude = {west.ToString(CultureInfo.InvariantCulture)};");
    sb.AppendLine($"    public const double EastLongitude = {east.ToString(CultureInfo.InvariantCulture)};");
    sb.AppendLine("    public const double MeanLatitudeDegrees = (SouthLatitude + NorthLatitude) / 2.0;");
    sb.AppendLine("    public const double MetersPerDegreeLatitude = 111_132.0;");
    sb.AppendLine("    public const double MetersPerDegreeLongitudeAtEquator = 111_320.0;");
    sb.AppendLine();
    sb.AppendLine("    public static float EastMeters { get; } = (float)(");
    sb.AppendLine("        (EastLongitude - WestLongitude)");
    sb.AppendLine("        * MetersPerDegreeLongitudeAtEquator");
    sb.AppendLine("        * Math.Cos(MeanLatitudeDegrees * Math.PI / 180.0));");
    sb.AppendLine();
    sb.AppendLine("    public static float NorthMeters { get; } = (float)((NorthLatitude - SouthLatitude) * MetersPerDegreeLatitude);");
    sb.AppendLine();
    sb.AppendLine("    public static float MapExtentMeters { get; } = MathF.Max(EastMeters, NorthMeters);");
    sb.AppendLine();
    sb.AppendLine("    public float SampleHeight(float x, float z)");
    sb.AppendLine("    {");
    sb.AppendLine("        if (KristiansandLandSeaMask.IsWater(x, z))");
    sb.AppendLine("            return 0f;");
    sb.AppendLine();
    sb.AppendLine("        var cells = KristiansandElevation.Count - 1;");
    sb.AppendLine("        var u = Math.Clamp(x / EastMeters, 0f, 1f) * cells;");
    sb.AppendLine("        var v = Math.Clamp(z / NorthMeters, 0f, 1f) * cells;");
    sb.AppendLine("        var i0 = (int)MathF.Floor(u);");
    sb.AppendLine("        var j0 = (int)MathF.Floor(v);");
    sb.AppendLine("        var i1 = Math.Min(i0 + 1, cells);");
    sb.AppendLine("        var j1 = Math.Min(j0 + 1, cells);");
    sb.AppendLine("        var tx = u - i0;");
    sb.AppendLine("        var tz = v - j0;");
    sb.AppendLine("        var h00 = At(i0, j0);");
    sb.AppendLine("        var h10 = At(i1, j0);");
    sb.AppendLine("        var h01 = At(i0, j1);");
    sb.AppendLine("        var h11 = At(i1, j1);");
    sb.AppendLine("        var south = h00 + (h10 - h00) * tx;");
    sb.AppendLine("        var north = h01 + (h11 - h01) * tx;");
    sb.AppendLine("        return south + (north - south) * tz;");
    sb.AppendLine("    }");
    sb.AppendLine();
    sb.AppendLine("    private static float At(int i, int j) => KristiansandElevation.Meters[j * KristiansandElevation.Count + i];");
    sb.AppendLine();
    sb.AppendLine("    public static Vector2 ToWorldMeters(double latitudeDegrees, double longitudeDegrees)");
    sb.AppendLine("    {");
    sb.AppendLine("        var x = (float)((longitudeDegrees - WestLongitude) / (EastLongitude - WestLongitude) * EastMeters);");
    sb.AppendLine("        var z = (float)((latitudeDegrees - SouthLatitude) / (NorthLatitude - SouthLatitude) * NorthMeters);");
    sb.AppendLine("        return new Vector2(x, z);");
    sb.AppendLine("    }");
    sb.AppendLine("}");
    File.WriteAllText(Path.Combine(dir, "KristiansandHeightSampler.cs"), sb.ToString());
}

static void WriteMapFeatures(
    string dir,
    List<PolylineRecord> lines,
    List<PolygonRecord> water,
    string date,
    double eastM,
    double northM,
    double south,
    double north,
    double west,
    double east,
    string citation)
{
    var sb = new StringBuilder();
    sb.AppendLine("using System.Numerics;");
    sb.AppendLine();
    sb.AppendLine("namespace ArtillerySimulator.Game;");
    sb.AppendLine();
    sb.AppendLine("/// <summary>");
    sb.AppendLine($"/// OpenStreetMap for {south:F2}–{north:F2} N, {west:F2}–{east:F2} E, retrieved {date}.");
    sb.AppendLine($"/// {citation}");
    sb.AppendLine("/// © OpenStreetMap contributors. Points are world meters, X east and Z north, on the Kristiansand elevation grid.");
    sb.AppendLine("/// </summary>");
    sb.AppendLine("internal static partial class KristiansandMapFeatures");
    sb.AppendLine("{");
    sb.AppendLine("    public static readonly GroundPolyline[] Lines =");
    sb.AppendLine("    [");
    foreach (var line in lines)
    {
        var safeName = EscapeString(line.Name);
        var pts = string.Join(", ", line.Points.Select(p =>
            $"new({p.X.ToString("F1", CultureInfo.InvariantCulture)}f, {p.Y.ToString("F1", CultureInfo.InvariantCulture)}f)"));
        sb.AppendLine($"        new(\"{safeName}\", \"{line.Kind}\", [{pts}]),");
    }

    sb.AppendLine("    ];");
    sb.AppendLine();
    sb.AppendLine("    public static readonly GroundPolygon[] Water =");
    sb.AppendLine("    [");
    foreach (var poly in water)
    {
        var safeName = EscapeString(poly.Name);
        var pts = string.Join(", ", poly.Points.Select(p =>
            $"new({p.X.ToString("F1", CultureInfo.InvariantCulture)}f, {p.Y.ToString("F1", CultureInfo.InvariantCulture)}f)"));
        sb.AppendLine($"        new(\"{safeName}\", [{pts}]),");
    }

    sb.AppendLine("    ];");
    sb.AppendLine("}");
    File.WriteAllText(Path.Combine(dir, "KristiansandMapFeatures.cs"), sb.ToString());
}

static string EscapeString(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");

static float SampleBilinearMeters(
    short[] meters,
    int count,
    float x,
    float z,
    float eastMeters,
    float northMeters)
{
    var cells = count - 1;
    var u = Math.Clamp(x / eastMeters, 0f, 1f) * cells;
    var v = Math.Clamp(z / northMeters, 0f, 1f) * cells;
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
    var north = h01 + (h11 - h01) * tx;
    return south + (north - south) * tz;
}

static void WriteMapComparePng(string path, string previewPath, string mapDir)
{
    var referencePath = Path.Combine(mapDir, "kristiansand-reference.png");
    if (!File.Exists(referencePath) || !File.Exists(previewPath))
        return;

    using var reference = SKBitmap.Decode(referencePath);
    using var generated = SKBitmap.Decode(previewPath);
    if (reference is null || generated is null)
        return;

    const int pad = 12;
    const int labelH = 28;
    var width = reference.Width + generated.Width + pad * 3;
    var height = Math.Max(reference.Height, generated.Height) + labelH + pad * 2;
    using var surface = SKSurface.Create(new SKImageInfo(width, height));
    var canvas = surface.Canvas;
    canvas.Clear(new SKColor(18, 22, 30));
    using var label = new SKPaint { Color = SKColors.White, TextSize = 20, IsAntialias = true };
    canvas.DrawText("Reference (not georeferenced to scenario bbox)", pad, pad + 18, label);
    canvas.DrawText("Generated (NDH + OSM, scenario WGS84)", reference.Width + pad * 2, pad + 18, label);
    canvas.DrawBitmap(reference, pad, labelH + pad);
    canvas.DrawBitmap(generated, reference.Width + pad * 2, labelH + pad);
    using var image = surface.Snapshot();
    using var data = image.Encode(SKEncodedImageFormat.Png, 92);
    using var fs = File.OpenWrite(path);
    data.SaveTo(fs);
}

static void WriteLandSeaMaskBinary(string path, ReferenceWaterMaskFrame frame, byte[] water)
{
    using var stream = File.OpenWrite(path);
    using var writer = new BinaryWriter(stream);
    writer.Write(0x4D534C4Bu);
    writer.Write((ushort)1);
    writer.Write((ushort)frame.ImageWidth);
    writer.Write((ushort)frame.ImageHeight);
    writer.Write(frame.PixelsPerMeter);
    writer.Write(frame.OffsetPxX);
    writer.Write(frame.OffsetPxY);
    writer.Write(frame.ScenarioEastMeters);
    writer.Write(frame.ScenarioNorthMeters);
    writer.Write(water);
}

static void WriteLandSeaMaskGeorefJson(string path, ReferenceWaterMaskFrame frame, string imageFile)
{
    var doc = new
    {
        citation = "Norgeskart reference image → scenario meters. Uniform pixels/meter; letterbox preserves image aspect.",
        waterColorHex = "5182FF",
        imageFile,
        maskBinary = "kristiansand-land-sea-mask.bin",
        imageWidth = frame.ImageWidth,
        imageHeight = frame.ImageHeight,
        pixelsPerMeter = frame.PixelsPerMeter,
        offsetPxX = frame.OffsetPxX,
        offsetPxY = frame.OffsetPxY,
        scenarioEastMeters = frame.ScenarioEastMeters,
        scenarioNorthMeters = frame.ScenarioNorthMeters,
        worldToImage = new
        {
            px = "xMeters * pixelsPerMeter + offsetPxX",
            py = "offsetPxY + (scenarioNorthMeters - zMeters) * pixelsPerMeter",
        },
    };
    File.WriteAllText(
        path,
        JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = true }));
}

static void WriteLandSeaMaskPreviewPng(string path, byte[] water, int width, int height)
{
    using var bitmap = new SKBitmap(width, height);
    for (var y = 0; y < height; y++)
    {
        for (var x = 0; x < width; x++)
        {
            var isWater = water[y * width + x] != 0;
            bitmap.SetPixel(
                x,
                y,
                isWater ? new SKColor(81, 130, 255) : new SKColor(48, 92, 52));
        }
    }

    using var image = SKImage.FromBitmap(bitmap);
    using var data = image.Encode(SKEncodedImageFormat.Png, 92);
    using var fs = File.OpenWrite(path);
    data.SaveTo(fs);
}

static bool LandSeaMaskIsWater(byte[]? mask, ReferenceWaterMaskFrame? frame, float x, float z)
{
    if (mask is null || frame is null)
        return false;
    var f = frame.Value;
    var px = x * f.PixelsPerMeter + f.OffsetPxX;
    var py = f.OffsetPxY + (f.ScenarioNorthMeters - z) * f.PixelsPerMeter;
    var ix = (int)MathF.Round(px);
    var iy = (int)MathF.Round(py);
    if (ix < 0 || iy < 0 || ix >= f.ImageWidth || iy >= f.ImageHeight)
        return false;
    return mask[iy * f.ImageWidth + ix] != 0;
}

static void WriteMapPreviewPng(
    string path,
    short[] meters,
    int gridCount,
    List<PolylineRecord> lines,
    List<PolygonRecord> water,
    float eastMeters,
    float northMeters,
    byte[]? landSeaMask,
    ReferenceWaterMaskFrame? landSeaFrame)
{
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);

    var minH = meters.Min();
    var maxH = meters.Max();
    var span = Math.Max(1, maxH - minH);
    var width = 1280;
    var height = Math.Max(480, (int)Math.Round(width * (northMeters / eastMeters)));

    using var bitmap = new SKBitmap(width, height);
    for (var py = 0; py < height; py++)
    {
        var v = 1f - py / (height - 1f);
        var z = v * northMeters;
        for (var px = 0; px < width; px++)
        {
            var u = px / (width - 1f);
            var x = u * eastMeters;
            SKColor c;
            if (LandSeaMaskIsWater(landSeaMask, landSeaFrame, x, z))
                c = new SKColor(81, 130, 255);
            else
            {
                var h = SampleBilinearMeters(meters, gridCount, x, z, eastMeters, northMeters);
                var t = (h - minH) / (float)span;
                c = SKColor.FromHsv(95f - t * 55f, 42, (byte)(38 + t * 58));
            }

            bitmap.SetPixel(px, py, c);
        }
    }

    using var surface = SKSurface.Create(new SKImageInfo(width, height));
    var canvas = surface.Canvas;
    canvas.Clear(new SKColor(24, 28, 36));
    canvas.DrawBitmap(bitmap, 0, 0);

    using var waterPaint = new SKPaint { Color = new SKColor(48, 118, 188, 150), Style = SKPaintStyle.Fill, IsAntialias = true };
    foreach (var poly in water)
    {
        using var pathWater = new SKPath();
        var first = true;
        foreach (var p in poly.Points)
        {
            var sx = p.X / eastMeters * width;
            var sy = height - p.Y / northMeters * height;
            if (first)
            {
                pathWater.MoveTo(sx, sy);
                first = false;
            }
            else
                pathWater.LineTo(sx, sy);
        }

        pathWater.Close();
        canvas.DrawPath(pathWater, waterPaint);
    }

    using var coastPaint = new SKPaint { Color = new SKColor(120, 210, 255), StrokeWidth = 2.5f, Style = SKPaintStyle.Stroke, IsAntialias = true };
    using var riverPaint = new SKPaint { Color = new SKColor(90, 170, 230), StrokeWidth = 2f, Style = SKPaintStyle.Stroke, IsAntialias = true };
    using var roadPaint = new SKPaint { Color = new SKColor(210, 188, 140), StrokeWidth = 1.6f, Style = SKPaintStyle.Stroke, IsAntialias = true };
    foreach (var line in lines)
    {
        using var pathLine = new SKPath();
        var first = true;
        foreach (var p in line.Points)
        {
            var sx = p.X / eastMeters * width;
            var sy = height - p.Y / northMeters * height;
            if (first)
            {
                pathLine.MoveTo(sx, sy);
                first = false;
            }
            else
                pathLine.LineTo(sx, sy);
        }

        var paint = line.Kind switch
        {
            "coastline" => coastPaint,
            "river" => riverPaint,
            _ => roadPaint,
        };
        canvas.DrawPath(pathLine, paint);
    }

    using var image = surface.Snapshot();
    using var data = image.Encode(SKEncodedImageFormat.Png, 92);
    using var fs = File.OpenWrite(path);
    data.SaveTo(fs);
}

readonly record struct PolylineRecord(string Name, string Kind, Vector2[] Points, float Length);

readonly record struct PolygonRecord(string Name, Vector2[] Points, float Area);

sealed record CutoutBounds(
    double South,
    double North,
    double West,
    double East,
    string Citation,
    double UtmWest,
    double UtmSouth,
    double UtmEast,
    double UtmNorth);

sealed class CutoutJson
{
    public string? Citation { get; set; }

    public Wgs84Box Wgs84 { get; set; } = new();

    public Epsg25833Center? CenterEpsg25833 { get; set; }

    public double HalfExtentEastMeters { get; set; }

    public double HalfExtentNorthMeters { get; set; }
}

sealed class Epsg25833Center
{
    public double Easting { get; set; }

    public double Northing { get; set; }
}

sealed class Wgs84Box
{
    public double South { get; set; }
    public double North { get; set; }
    public double West { get; set; }
    public double East { get; set; }
}

sealed class ElevationResponse
{
    [JsonPropertyName("elevation")]
    public double[]? Elevation { get; set; }
}

sealed class OverpassResult
{
    [JsonPropertyName("elements")]
    public List<OverpassElement> Elements { get; set; } = [];
}

sealed class OverpassElement
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("lat")]
    public double? Lat { get; set; }

    [JsonPropertyName("lon")]
    public double? Lon { get; set; }

    [JsonPropertyName("nodes")]
    public List<long>? Nodes { get; set; }

    [JsonPropertyName("tags")]
    public Dictionary<string, string>? Tags { get; set; }
}

file static class KartverketNdhExtract
{
    const string ExportUrl = "https://hoydedata.no/arcgis/rest/services/NHM_DTM_25833/ImageServer/exportImage";

    internal static async Task<short[]> SampleAsync(HttpClient http, CutoutBounds cutout, int count) =>
        await SampleSquareGridAsync(http, cutout, count);

    internal static async Task<float[]> SampleRasterAsync(HttpClient http, CutoutBounds cutout, int width, int height) =>
        await DownloadFloatRasterAsync(http, cutout, width, height);

    static async Task<short[]> SampleSquareGridAsync(HttpClient http, CutoutBounds cutout, int count)
    {
        var floats = await DownloadFloatRasterAsync(http, cutout, count, count);
        var meters = new short[floats.Length];
        for (var i = 0; i < floats.Length; i++)
        {
            var v = floats[i];
            if (float.IsNaN(v) || v < -0.5f)
                v = 0f;
            meters[i] = (short)Math.Clamp(Math.Round(v), 0, 2000);
        }

        return meters;
    }

    static async Task<float[]> DownloadFloatRasterAsync(HttpClient http, CutoutBounds cutout, int width, int height)
    {
        var inv = CultureInfo.InvariantCulture;
        var bbox = string.Join(
            ',',
            cutout.UtmWest.ToString(inv),
            cutout.UtmSouth.ToString(inv),
            cutout.UtmEast.ToString(inv),
            cutout.UtmNorth.ToString(inv));
        var url =
            $"{ExportUrl}?bbox={bbox}&bboxSR=25833&size={width},{height}&imageSR=25833&format=tiff&pixelType=F32&f=image";
        var bytes = await http.GetByteArrayAsync(url);
        var tmp = Path.Combine(Path.GetTempPath(), $"novolis-ndh-{Guid.NewGuid():N}.tif");
        try
        {
            await File.WriteAllBytesAsync(tmp, bytes);
            return DecodeFloatGeoTiff(tmp, width, height);
        }
        finally
        {
            try
            {
                File.Delete(tmp);
            }
            catch
            {
                // ignore
            }
        }
    }

    static float[] DecodeFloatGeoTiff(string path, int width, int height)
    {
        using var tiff = Tiff.Open(path, "r")
            ?? throw new InvalidDataException($"Could not open Kartverket GeoTIFF: {path}");

        var imageWidth = tiff.GetField(TiffTag.IMAGEWIDTH)?[0].ToInt()
            ?? throw new InvalidDataException("GeoTIFF missing IMAGEWIDTH");
        var imageHeight = tiff.GetField(TiffTag.IMAGELENGTH)?[0].ToInt()
            ?? throw new InvalidDataException("GeoTIFF missing IMAGELENGTH");
        if (imageWidth != width || imageHeight != height)
            throw new InvalidDataException($"Expected {width}×{height} raster, got {imageWidth}×{imageHeight}");

        var floats = new float[width * height];
        if (tiff.IsTiled() && !TryReadTiledFloats(tiff, imageWidth, imageHeight, width, height, floats))
            ReadScanlineFloats(tiff, width, height, floats);
        else if (!tiff.IsTiled())
            ReadScanlineFloats(tiff, width, height, floats);

        return floats;

        static bool TryReadTiledFloats(
            Tiff tiff,
            int imageWidth,
            int imageHeight,
            int width,
            int height,
            float[] floats)
        {
            var tileWidth = tiff.GetField(TiffTag.TILEWIDTH)?[0].ToInt() ?? 256;
            var tileHeight = tiff.GetField(TiffTag.TILELENGTH)?[0].ToInt() ?? 256;
            var tilesAcross = (imageWidth + tileWidth - 1) / tileWidth;
            var tileBuffer = new byte[tiff.TileSize()];
            var tileCount = tiff.NumberOfTiles();
            for (var tileIndex = 0; tileIndex < tileCount; tileIndex++)
            {
                if (tiff.ReadEncodedTile(tileIndex, tileBuffer, 0, tileBuffer.Length) < 0)
                    return false;

                var tileCol = tileIndex % tilesAcross;
                var tileRow = tileIndex / tilesAcross;
                var tileLeft = tileCol * tileWidth;
                var tileTop = tileRow * tileHeight;
                for (var y = 0; y < tileHeight; y++)
                {
                    var imageY = tileTop + y;
                    if (imageY >= imageHeight)
                        continue;
                    for (var x = 0; x < tileWidth; x++)
                    {
                        var imageX = tileLeft + x;
                        if (imageX >= imageWidth)
                            continue;
                        var src = (y * tileWidth + x) * sizeof(float);
                        if (src + sizeof(float) > tileBuffer.Length)
                            continue;
                        var value = BitConverter.ToSingle(tileBuffer, src);
                        var southRow = imageHeight - 1 - imageY;
                        floats[southRow * width + imageX] = value;
                    }
                }
            }

            return true;
        }

        static void ReadScanlineFloats(Tiff tiff, int width, int height, float[] floats)
        {
            var scanline = new byte[width * sizeof(float)];
            for (var imageY = 0; imageY < height; imageY++)
            {
                if (!tiff.ReadScanline(scanline, imageY))
                    throw new InvalidDataException($"Failed reading GeoTIFF row {imageY}");

                var southRow = height - 1 - imageY;
                for (var x = 0; x < width; x++)
                {
                    var value = BitConverter.ToSingle(scanline, x * sizeof(float));
                    floats[southRow * width + x] = value;
                }
            }
        }
    }
}

file static class DemGroundTruthFusion
{
    internal static GroundTruthBundle LoadGroundTruth(string path)
    {
        var json = File.ReadAllText(path);
        var doc = JsonSerializer.Deserialize<GroundTruthJson>(json)
            ?? throw new InvalidDataException($"Could not parse {path}");
        var points = new List<GroundTruthPoint>();
        foreach (var a in doc.Anchors ?? [])
            points.Add(new GroundTruthPoint(a.Latitude, a.Longitude, a.ElevationMeters, a.Id ?? a.Name ?? "anchor"));
        foreach (var r in doc.RoadSamples ?? [])
            points.Add(new GroundTruthPoint(r.Latitude, r.Longitude, r.ElevationMeters, r.RoadName ?? "road"));
        return new GroundTruthBundle(points);
    }

    internal static List<string> ApplyAnchorCorrections(
        short[] meters,
        int count,
        float eastMeters,
        float northMeters,
        double west,
        double east,
        double south,
        double north,
        GroundTruthBundle truth)
    {
        var report = new List<string>();
        if (truth.Points.Count == 0)
            return report;

        var residuals = new List<(float x, float z, float delta)>();
        foreach (var p in truth.Points)
        {
            var xz = Project(p.Latitude, p.Longitude, eastMeters, northMeters, west, east, south, north);
            var sampled = BilinearMeters(meters, count, xz.X, xz.Y, eastMeters, northMeters);
            var delta = p.ElevationMeters - sampled;
            residuals.Add((xz.X, xz.Y, delta));
            report.Add($"{p.Label}: expected {p.ElevationMeters:F0} m, before {sampled:F1} m, Δ {delta:F1} m");
        }

        const float influenceMeters = 2800f;
        for (var j = 0; j < count; j++)
        {
            for (var i = 0; i < count; i++)
            {
                var x = i / (float)(count - 1) * eastMeters;
                var z = j / (float)(count - 1) * northMeters;
                var weightSum = 0f;
                var deltaSum = 0f;
                foreach (var (rx, rz, delta) in residuals)
                {
                    var d = Vector2.Distance(new Vector2(x, z), new Vector2(rx, rz));
                    var w = MathF.Exp(-d * d / (influenceMeters * influenceMeters));
                    weightSum += w;
                    deltaSum += w * delta;
                }

                if (weightSum < 1e-6f)
                    continue;
                var idx = j * count + i;
                var corrected = meters[idx] + deltaSum / weightSum;
                meters[idx] = (short)Math.Clamp(Math.Round(corrected), -20, 2000);
            }
        }

        foreach (var p in truth.Points)
        {
            var xz = Project(p.Latitude, p.Longitude, eastMeters, northMeters, west, east, south, north);
            var after = BilinearMeters(meters, count, xz.X, xz.Y, eastMeters, northMeters);
            report.Add($"{p.Label} after correction: {after:F1} m");
        }

        return report;
    }

    static float BilinearMeters(short[] meters, int count, float x, float z, float eastMeters, float northMeters)
    {
        var cells = count - 1;
        var u = Math.Clamp(x / eastMeters, 0f, 1f) * cells;
        var v = Math.Clamp(z / northMeters, 0f, 1f) * cells;
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

    internal static short[] FuseHillshade(short[] ndhMeters, int count, string hillshadePath)
    {
        var hill = HillshadeDemExtract.Sample(hillshadePath, count);
        var fused = new short[ndhMeters.Length];
        for (var k = 0; k < fused.Length; k++)
        {
            var ndh = ndhMeters[k];
            var hs = hill[k];
            if (ndh <= 0 && hs >= 4)
                fused[k] = (short)Math.Clamp(Math.Round(hs * 0.35f), 0, 80);
            else if (ndh <= 2 && hs <= 1)
                fused[k] = 0;
            else
                fused[k] = ndh;
        }

        return fused;
    }

    static Vector2 Project(
        double lat,
        double lon,
        float eastM,
        float northM,
        double west,
        double east,
        double south,
        double north)
    {
        var x = (float)((lon - west) / (east - west) * eastM);
        var z = (float)((lat - south) / (north - south) * northM);
        return new Vector2(x, z);
    }

    internal readonly record struct GroundTruthPoint(double Latitude, double Longitude, float ElevationMeters, string Label);

    internal sealed record GroundTruthBundle(List<GroundTruthPoint> Points);
}

sealed class GroundTruthJson
{
    [JsonPropertyName("anchors")]
    public List<GroundTruthAnchorJson>? Anchors { get; set; }

    [JsonPropertyName("roadSamples")]
    public List<GroundTruthRoadJson>? RoadSamples { get; set; }
}

sealed class GroundTruthAnchorJson
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("latitude")]
    public double Latitude { get; set; }

    [JsonPropertyName("longitude")]
    public double Longitude { get; set; }

    [JsonPropertyName("elevationMeters")]
    public float ElevationMeters { get; set; }
}

sealed class GroundTruthRoadJson
{
    [JsonPropertyName("roadName")]
    public string? RoadName { get; set; }

    [JsonPropertyName("latitude")]
    public double Latitude { get; set; }

    [JsonPropertyName("longitude")]
    public double Longitude { get; set; }

    [JsonPropertyName("elevationMeters")]
    public float ElevationMeters { get; set; }
}

readonly record struct ReferenceWaterMaskFrame(
    int ImageWidth,
    int ImageHeight,
    float PixelsPerMeter,
    float OffsetPxX,
    float OffsetPxY,
    float ScenarioEastMeters,
    float ScenarioNorthMeters);

file static class ReferenceWaterMaskExtract
{
    const byte TargetR = 0x51;
    const byte TargetG = 0x82;
    const byte TargetB = 0xFF;
    const int MaxColorDistanceSq = 40 * 40;

    internal static (byte[] Water, ReferenceWaterMaskFrame Frame) Build(
        string referencePath,
        float eastMeters,
        float northMeters,
        bool cornerGeoref = false)
    {
        using var bitmap = SKBitmap.Decode(referencePath)
            ?? throw new InvalidDataException($"Could not decode reference: {referencePath}");

        var w = bitmap.Width;
        var h = bitmap.Height;
        var water = new byte[w * h];
        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                var c = bitmap.GetPixel(x, y);
                water[y * w + x] = IsNorgeskartWater(c.Red, c.Green, c.Blue) ? (byte)1 : (byte)0;
            }
        }

        float ppm;
        float offX;
        float offY;
        if (cornerGeoref)
        {
            ppm = (w - 1) / eastMeters;
            offX = 0f;
            offY = 0f;
        }
        else
        {
            ppm = MathF.Min(w / eastMeters, h / northMeters);
            var usedW = eastMeters * ppm;
            var usedH = northMeters * ppm;
            offX = (w - usedW) * 0.5f;
            offY = (h - usedH) * 0.5f;
        }

        var frame = new ReferenceWaterMaskFrame(w, h, ppm, offX, offY, eastMeters, northMeters);
        return (water, frame);
    }

    internal static bool IsNorgeskartWater(byte r, byte g, byte b)
    {
        if (r + g + b < 36)
            return true;

        var dr = r - TargetR;
        var dg = g - TargetG;
        var db = b - TargetB;
        if (dr * dr + dg * dg + db * db <= MaxColorDistanceSq)
            return true;

        if (b >= 248 && r <= 95 && g >= 110 && g <= 155)
            return true;

        // Terrengskygge / relief print: open water is flat light blue or uniform gray sea.
        if (b >= 155 && b > r + 18 && b > g + 4 && g >= 120)
            return true;

        if (r > 248 && g > 248 && b > 248)
            return false;

        var spread = Math.Max(r, Math.Max(g, b)) - Math.Min(r, Math.Min(g, b));
        if (spread >= 5)
            return false;

        var avg = (r + g + b) / 3;
        return avg >= 148 && avg <= 178;
    }
}

file static class HillshadeDemExtract
{
    internal static short[] Sample(string imagePath, int count)
    {
        using var bitmap = SKBitmap.Decode(imagePath)
            ?? throw new InvalidDataException($"Could not decode hillshade image: {imagePath}");

        var marginX = bitmap.Width * 0.02f;
        var marginY = bitmap.Height * 0.02f;
        var x0 = marginX;
        var x1 = bitmap.Width - 1 - marginX;
        var y0 = marginY;
        var y1 = bitmap.Height - 1 - marginY;

        var land = new List<byte>();
        for (var j = 0; j < count; j++)
        {
            var v = j / (double)(count - 1);
            var py = y1 - (float)(v * (y1 - y0));
            for (var i = 0; i < count; i++)
            {
                var u = i / (double)(count - 1);
                var px = x0 + (float)(u * (x1 - x0));
                var lum = SampleLuminance(bitmap, px, py);
                if (!IsWater(bitmap, px, py, lum))
                    land.Add(lum);
            }
        }

        land.Sort();
        var p08 = land.Count > 0 ? land[(int)(land.Count * 0.08)] : (byte)80;
        var p92 = land.Count > 0 ? land[(int)(land.Count * 0.92)] : (byte)220;
        var span = Math.Max(8, p92 - p08);

        var meters = new short[count * count];
        for (var j = 0; j < count; j++)
        {
            var v = j / (double)(count - 1);
            var py = y1 - (float)(v * (y1 - y0));
            for (var i = 0; i < count; i++)
            {
                var u = i / (double)(count - 1);
                var px = x0 + (float)(u * (x1 - x0));
                var lum = SampleLuminance(bitmap, px, py);
                float metersF;
                if (IsWater(bitmap, px, py, lum))
                    metersF = 0f;
                else
                {
                    var t = Math.Clamp((lum - p08) / (float)span, 0f, 1f);
                    metersF = MathF.Pow(t, 0.92f) * 420f;
                }

                meters[j * count + i] = (short)Math.Clamp(Math.Round(metersF), 0, 2000);
            }
        }

        return meters;
    }

    static byte SampleLuminance(SKBitmap bitmap, float x, float y)
    {
        var c = bitmap.GetPixel(
            Math.Clamp((int)MathF.Round(x), 0, bitmap.Width - 1),
            Math.Clamp((int)MathF.Round(y), 0, bitmap.Height - 1));
        return (byte)((c.Red + c.Green + c.Blue) / 3);
    }

    static bool IsWater(SKBitmap bitmap, float x, float y, byte lum)
    {
        if (lum < 108 || lum > 178)
            return false;

        var sum = 0;
        var sumSq = 0;
        var n = 0;
        for (var dy = -2; dy <= 2; dy++)
        {
            for (var dx = -2; dx <= 2; dx++)
            {
                var px = Math.Clamp((int)MathF.Round(x) + dx, 0, bitmap.Width - 1);
                var py = Math.Clamp((int)MathF.Round(y) + dy, 0, bitmap.Height - 1);
                var l = SampleLuminance(bitmap, px, py);
                sum += l;
                sumSq += l * l;
                n++;
            }
        }

        var mean = sum / (double)n;
        var variance = sumSq / n - mean * mean;
        return variance < 90;
    }
}
