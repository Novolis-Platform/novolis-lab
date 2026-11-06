#:property OutputType=Exe
#:property TargetFramework=net10.0
#:property ImplicitUsings=enable
#:property Nullable=enable

using System.Globalization;

// Standalone orientation probe — reads mask + heightfield next to the lab map/.
var mapDir = @"d:\novolis\novolis-lab\labs\simulation\ArtillerySimulator\map";

const double south = 58.06181603859646;
const double north = 58.20818396140355;
const double west = 7.825738542655645;
const double east = 8.234261457344356;
const float eastM = 24008.08f;
const float northM = 16266.16f;

float LonX(double lon) => (float)((lon - west) / (east - west) * eastM);
float LatZ(double lat) => (float)((lat - south) / (north - south) * northM);

var maskPath = Path.Combine(mapDir, "kristiansand-land-sea-mask.bin");
var elevPath = Path.Combine(mapDir, "layers", "heightfield-1025.bin");
Console.WriteLine($"mapDir={mapDir}");
Console.WriteLine($"mask exists={File.Exists(maskPath)} elev exists={File.Exists(elevPath)}");

using var ms = File.OpenRead(maskPath);
using var br = new BinaryReader(ms);
var magic = br.ReadUInt32();
var ver = br.ReadUInt16();
var iw = br.ReadUInt16();
var ih = br.ReadUInt16();
var ppm = br.ReadSingle();
var ox = br.ReadSingle();
var oy = br.ReadSingle();
_ = br.ReadSingle();
var scenN = br.ReadSingle();
var water = br.ReadBytes(iw * ih);
Console.WriteLine(
    $"KLSM magic=0x{magic:X8} ver={ver} {iw}x{ih} ppm={ppm.ToString("F4", CultureInfo.InvariantCulture)} ox={ox.ToString("F1", CultureInfo.InvariantCulture)} oy={oy.ToString("F1", CultureInfo.InvariantCulture)} northM={scenN.ToString("F1", CultureInfo.InvariantCulture)}");

bool IsWater(float x, float z, bool flipEast)
{
    var dataX = flipEast ? eastM - x : x;
    var px = dataX * ppm + ox;
    var py = oy + (scenN - z) * ppm;
    var ix = (int)MathF.Round(px);
    var iy = (int)MathF.Round(py);
    if (ix < 0 || iy < 0 || ix >= iw || iy >= ih) return false;
    return water[iy * iw + ix] != 0;
}

var fortX = LonX(8.0085);
var fortZ = LatZ(58.1345);
var dtX = LonX(7.9956);
var dtZ = LatZ(58.1464);

Console.WriteLine(
    $"fort lon→x={fortX:F0} lat→z={fortZ:F0}; downtown x={dtX:F0} (Δx={(dtX - fortX):F0}, expect west <0)");

foreach (var flip in new[] { false, true })
{
    var westWater = 0;
    var eastWater = 0;
    var n = 0;
    for (var dz = -200f; dz <= 200f; dz += 40f)
    {
        for (var dx = 40f; dx <= 400f; dx += 40f)
        {
            n++;
            if (IsWater(fortX - dx, fortZ + dz, flip)) westWater++;
            if (IsWater(fortX + dx, fortZ + dz, flip)) eastWater++;
        }
    }

    var fortLand = !IsWater(fortX, fortZ, flip);
    var southSea = IsWater(fortX, fortZ - 500f, flip);
    var dtLand = !IsWater(dtX, dtZ, flip);
    Console.WriteLine(
        $"flipEast={flip}: fortLand={fortLand} downtownLand={dtLand} south500water={southSea} westW={westWater}/{n} eastW={eastWater}/{n}");
}

Console.WriteLine("Real Odderøya: harbor/channel more to the WEST; open Skagerrak more EAST/SE.");
Console.WriteLine("Pick flipEast that yields eastW > westW and fortLand + downtownLand + south water.");
