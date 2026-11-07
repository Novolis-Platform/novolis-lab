// Measure NVDB road samples vs DTM / water-mask.
#:project d:\novolis\novolis-lab\labs\simulation\ArtillerySimulator\scripts\MapPipeline.Support\MapPipeline.Support.csproj
#:property TargetFramework=net10.0
#:property ImplicitUsings=enable
#:property Nullable=enable

using System.Text.Json;
using ArtillerySimulator.MapPipeline;

var layersDir = @"d:\novolis\novolis-lab\labs\simulation\ArtillerySimulator\map\layers";
var mapDir = Path.GetDirectoryName(layersDir)!;
var world = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(mapDir, "kristiansand-map-stack.json"))).RootElement.GetProperty("world");
var eastM = world.GetProperty("eastMeters").GetDouble();
var northM = world.GetProperty("northMeters").GetDouble();
var e0 = world.GetProperty("utmOriginEasting").GetDouble();
var n0 = world.GetProperty("utmOriginNorthing").GetDouble();

var dtm = TiledFloatTiff.Decode(await File.ReadAllBytesAsync(Path.Combine(layersDir, "ndh-dtm.tif")), out var dw, out var dh);
var water = await File.ReadAllBytesAsync(Path.Combine(layersDir, "water-mask.bin"));
using var geo = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(layersDir, "georef.json")));
var ww = geo.RootElement.GetProperty("PixelWidth").GetInt32();
var wh = geo.RootElement.GetProperty("PixelHeight").GetInt32();

float SampleDtm(double u, double vNorthFromSouth) =>
    TiledFloatTiff.SampleBilinear(dtm, dw, dh, u * (dw - 1), (1.0 - vNorthFromSouth) * (dh - 1));

bool IsWater(double u, double vNorthFromSouth)
{
    var ix = (int)Math.Clamp(Math.Round(u * (ww - 1)), 0, ww - 1);
    var iy = (int)Math.Clamp(Math.Round((1.0 - vNorthFromSouth) * (wh - 1)), 0, wh - 1);
    return water[iy * ww + ix] != 0;
}

void Mark(string name, double lat, double lon)
{
    var (e, n) = Etrs89Utm33.ToEastingNorthing(lat, lon);
    var x = e - e0;
    var z = n - n0;
    var u = x / eastM;
    var v = z / northM;
    var h = SampleDtm(u, v);
    Console.WriteLine($"{name} world=({x:F1},{z:F1}) u={u:F4} v={v:F4} dtm={h:F2} waterMask={IsWater(u, v)}");
}

Mark("A", 58.1433529, 7.9939879);
Mark("B", 58.1487535, 8.0038317);

var segs = NvdbRoadsBinary.Read(Path.Combine(layersDir, "nvdb-roads.bin"));
var main = 0;
var mainWet = 0;
var local = 0;
var localWet = 0;
var hist = new int[12];
foreach (var seg in segs)
{
    foreach (var (x, z) in seg.Points)
    {
        if (x < 0 || z < 0 || x > eastM || z > northM)
            continue;
        var u = x / eastM;
        var v = z / northM;
        var h = SampleDtm(u, v);
        var wet = IsWater(u, v);
        var bucket = float.IsNaN(h) ? 0 : (int)Math.Clamp(Math.Floor(h) + 1, 0, 11);
        hist[bucket]++;
        if (seg.IsMain)
        {
            main++;
            if (wet) mainWet++;
        }
        else
        {
            local++;
            if (wet) localWet++;
        }
    }
}

Console.WriteLine($"main on water: {mainWet}/{main} ({100.0 * mainWet / Math.Max(1, main):F1}%)");
Console.WriteLine($"local on water: {localWet}/{local} ({100.0 * localWet / Math.Max(1, local):F1}%)");
Console.WriteLine("DTM height hist at road pts (0=NaN,1=<0,2=0-1m,...): " + string.Join(",", hist));

var wetH = new List<float>();
var dryH = new List<float>();
for (var y = 0; y < wh; y += 8)
for (var x = 0; x < ww; x += 8)
{
    var u = x / (double)(ww - 1);
    var v = 1.0 - y / (double)(wh - 1);
    var h = SampleDtm(u, v);
    if (float.IsNaN(h))
        continue;
    if (water[y * ww + x] != 0) wetH.Add(h);
    else dryH.Add(h);
}

wetH.Sort();
dryH.Sort();
float Pct(List<float> a, double p) => a[(int)((a.Count - 1) * p)];
Console.WriteLine($"water-mask DTM p10/p50/p90 = {Pct(wetH, 0.1):F2}/{Pct(wetH, 0.5):F2}/{Pct(wetH, 0.9):F2} n={wetH.Count}");
Console.WriteLine($"land-mask  DTM p10/p50/p90 = {Pct(dryH, 0.1):F2}/{Pct(dryH, 0.5):F2}/{Pct(dryH, 0.9):F2} n={dryH.Count}");
