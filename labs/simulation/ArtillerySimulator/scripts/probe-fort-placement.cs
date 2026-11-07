#:property OutputType=Exe
#:property TargetFramework=net10.0
#:property ImplicitUsings=enable
#:property Nullable=enable

var mapDir = @"d:\novolis\novolis-lab\labs\simulation\ArtillerySimulator\map";
const double south = 58.06181603859646;
const double north = 58.20818396140355;
const double west = 7.825738542655645;
const double east = 8.234261457344356;
const float eastM = 24008.08f;
const float northM = 16266.16f;

float LonX(double lon) => (float)((lon - west) / (east - west) * eastM);
float LatZ(double lat) => (float)((lat - south) / (north - south) * northM);

using var br = new BinaryReader(File.OpenRead(Path.Combine(mapDir, "kristiansand-land-sea-mask.bin")));
_ = br.ReadUInt32();
_ = br.ReadUInt16();
var iw = br.ReadUInt16();
var ih = br.ReadUInt16();
var ppm = br.ReadSingle();
var ox = br.ReadSingle();
var oy = br.ReadSingle();
_ = br.ReadSingle();
var scenN = br.ReadSingle();
var water = br.ReadBytes(iw * ih);

var hb = File.ReadAllBytes(Path.Combine(mapDir, "layers", "heightfield-1025.bin"));
var gw = BitConverter.ToInt32(hb, 0);

bool IsWater(float x, float z, bool flip)
{
    var dataX = flip ? eastM - x : x;
    var ix = (int)MathF.Round(dataX * ppm + ox);
    var iy = (int)MathF.Round(oy + (scenN - z) * ppm);
    if (ix < 0 || iy < 0 || ix >= iw || iy >= ih)
        return false;
    return water[iy * iw + ix] != 0;
}

float HeightAt(float x, float z, bool flip)
{
    var cells = gw - 1;
    var u = Math.Clamp(flip ? 1f - x / eastM : x / eastM, 0f, 1f);
    var v = Math.Clamp(z / northM, 0f, 1f);
    var i = (int)MathF.Floor(u * cells);
    var j = (int)MathF.Floor(v * cells);
    return BitConverter.ToInt16(hb, 8 + (j * gw + i) * 2);
}

void Report(string name, float x, float z, bool flip)
{
    var land = !IsWater(x, z, flip);
    var h = HeightAt(x, z, flip);
    Console.WriteLine($"{name,-22} ({x:F0},{z:F0}) {(land ? "LAND" : "WATER")} h={h:F1}");
}

var fx = LonX(8.0085);
var fz = LatZ(58.1345);
Console.WriteLine($"fort lon/lat -> ({fx:F1},{fz:F1})");

foreach (var flip in new[] { false, true })
{
    Console.WriteLine($"\n=== RasterColumn0IsEast={flip} ===");
    Report("fort", fx, fz, flip);
    Report("S250", fx, fz - 250f, flip);
    Report("N250", fx, fz + 250f, flip);
    Report("E250", fx + 250f, fz, flip);
    Report("W250", fx - 250f, fz, flip);
    Report("downtown", LonX(7.9956), LatZ(58.1464), flip);

    float bestX = fx, bestZ = fz, bestScore = float.NegativeInfinity;
    var found = false;
    for (var z = fz - 120f; z <= fz + 120f; z += 12f)
    for (var x = fx - 120f; x <= fx + 120f; x += 12f)
    {
        if (IsWater(x, z, flip)) continue;
        var h = HeightAt(x, z, flip);
        if (h < 5f || h > 50f) continue;
        var waterNear = 0;
        if (IsWater(x - 140f, z, flip)) waterNear++;
        if (IsWater(x + 140f, z, flip)) waterNear++;
        if (IsWater(x, z - 160f, flip)) waterNear++;
        if (waterNear < 2) continue;
        var dist = MathF.Sqrt((x - fx) * (x - fx) + (z - fz) * (z - fz));
        var score = h + waterNear * 8f - dist * 0.4f;
        if (score <= bestScore) continue;
        bestScore = score;
        bestX = x;
        bestZ = z;
        found = true;
    }

    var d = MathF.Sqrt((bestX - fx) * (bestX - fx) + (bestZ - fz) * (bestZ - fz));
    Console.WriteLine($"snap found={found} -> ({bestX:F0},{bestZ:F0}) d={d:F1}m");
    Report("snapped", bestX, bestZ, flip);
}

// Southernmost land near fort (true tip)
Console.WriteLine("\n=== southernmost land within 1km (flip=false) ===");
float tipX = fx, tipZ = fz;
var tipFound = false;
for (var z = fz - 900f; z <= fz + 200f; z += 20f)
for (var x = fx - 500f; x <= fx + 500f; x += 20f)
{
    if (IsWater(x, z, false)) continue;
    var h = HeightAt(x, z, false);
    if (h < 2f || h > 45f) continue;
    var waterNear = 0;
    if (IsWater(x - 120f, z, false)) waterNear++;
    if (IsWater(x + 120f, z, false)) waterNear++;
    if (IsWater(x, z - 120f, false)) waterNear++;
    if (waterNear < 2) continue;
    if (!tipFound || z < tipZ)
    {
        tipX = x;
        tipZ = z;
        tipFound = true;
    }
}

if (tipFound)
{
    Report("south tip candidate", tipX, tipZ, false);
    var lat = south + (tipZ / northM) * (north - south);
    var lon = west + (tipX / eastM) * (east - west);
    Console.WriteLine($"  -> WGS84 approx {lat:F5},{lon:F5}");
}
