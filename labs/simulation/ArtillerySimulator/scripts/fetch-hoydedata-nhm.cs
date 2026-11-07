// Fetch NHM DTM (required) + optional DOM/TOPOBATHY float TIFFs into map/layers/.
// Uses the shared EPSG:25833 UTM frame (same origin as NVDB / scenario meters).
//
//   dotnet run --file d:\novolis\novolis-lab\labs\simulation\ArtillerySimulator\scripts\fetch-hoydedata-nhm.cs
//   dotnet run --file …\fetch-hoydedata-nhm.cs -- --dtm-only
//
#:project d:\novolis\novolis-lab\labs\simulation\ArtillerySimulator\scripts\MapPipeline.Support\MapPipeline.Support.csproj
#:property TargetFramework=net10.0
#:property ImplicitUsings=enable
#:property Nullable=enable
#:property ManagePackageVersionsCentrally=false

using System.Text.Json;
using ArtillerySimulator.MapPipeline;

var dtmOnly = args.Contains("--dtm-only", StringComparer.OrdinalIgnoreCase);
var skipDom = dtmOnly || args.Contains("--skip-dom", StringComparer.OrdinalIgnoreCase);
var skipTopo = dtmOnly || args.Contains("--skip-topobathy", StringComparer.OrdinalIgnoreCase);

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
var eastMeters = world.GetProperty("eastMeters").GetDouble();
var northMeters = world.GetProperty("northMeters").GetDouble();

double e0, n0;
if (world.TryGetProperty("utmOriginEasting", out var e0El)
    && world.TryGetProperty("utmOriginNorthing", out var n0El))
{
    e0 = e0El.GetDouble();
    n0 = n0El.GetDouble();
}
else
{
    (e0, n0) = Etrs89Utm33.FrameOriginFromWgs84Box(south, west, north, east, eastMeters, northMeters);
}

var w = 3466;
var h = 2348;
var georefPath = Path.Combine(layersDir, "georef.json");
if (File.Exists(georefPath))
{
    using var geo = JsonDocument.Parse(await File.ReadAllTextAsync(georefPath));
    if (geo.RootElement.TryGetProperty("PixelWidth", out var pw) || geo.RootElement.TryGetProperty("pixelWidth", out pw))
        w = pw.GetInt32();
    if (geo.RootElement.TryGetProperty("PixelHeight", out var ph) || geo.RootElement.TryGetProperty("pixelHeight", out ph))
        h = ph.GetInt32();
}

w = Math.Min(w, 4096);
h = Math.Min(h, 4096);
var maxE = e0 + eastMeters;
var maxN = n0 + northMeters;

Console.WriteLine(
    FormattableString.Invariant(
        $"EPSG:25833 frame E0={e0:F1} N0={n0:F1} → E1={maxE:F1} N1={maxN:F1}  {w}×{h}"));

using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };

async Task Fetch(string service, string fileName)
{
    var bytes = await NhmImageServer.FetchFloatTiffUtmAsync(http, service, e0, n0, maxE, maxN, w, h);
    var path = Path.Combine(layersDir, fileName);
    await File.WriteAllBytesAsync(path, bytes);
    _ = TiledFloatTiff.Decode(bytes, out var dw, out var dh);
    Console.WriteLine($"  decoded {dw}×{dh} → {path}");
}

await Fetch(NhmImageServer.Dtm25833, "ndh-dtm.tif");
if (!skipDom)
    await Fetch(NhmImageServer.Dom25833, "ndh-dom.tif");
if (!skipTopo)
    await Fetch(NhmImageServer.Topobathy25833, "ndh-topobathy.tif");

Console.WriteLine("Done. Rebuild heightfield with build-kristiansand-map (UTM-locked sampling).");
return 0;
