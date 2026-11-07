#:property OutputType=Exe
#:property TargetFramework=net10.0
#:property ImplicitUsings=enable
#:property Nullable=enable
#:property ManagePackageVersionsCentrally=false
#:package SkiaSharp@4.153.1

using SkiaSharp;

var pngPath = @"d:\novolis\novolis-lab\labs\simulation\ArtillerySimulator\map\layers\water-mask.png";
const float east = 24008.08f;
const float north = 16266.16f;
const double southLat = 58.06181603859646;
const double northLat = 58.20818396140355;
const double westLon = 7.825738542655645;
const double eastLon = 8.234261457344356;

using var bitmap = SKBitmap.Decode(pngPath) ?? throw new FileNotFoundException(pngPath);
var w = bitmap.Width;
var h = bitmap.Height;
var ppm = (w - 1) / east;

bool IsWater(float x, float z)
{
    var px = (int)MathF.Round(x * ppm);
    var py = (int)MathF.Round((north - z) * ppm);
    if (px < 0 || py < 0 || px >= w || py >= h) return false;
    return bitmap.GetPixel(px, py).Alpha > 0;
}

float LonX(double lon) => (float)((lon - westLon) / (eastLon - westLon) * east);
float LatZ(double lat) => (float)((lat - southLat) / (northLat - southLat) * north);

void Check(string name, double lat, double lon, bool expectWater)
{
    var x = LonX(lon);
    var z = LatZ(lat);
    var water = IsWater(x, z);
    var c = bitmap.GetPixel((int)MathF.Round(x * ppm), (int)MathF.Round((north - z) * ppm));
    var ok = water == expectWater ? "OK" : "FAIL";
    Console.WriteLine($"{ok} {name}: ({x:F0},{z:F0}) px=({x*ppm:F0},{(north-z)*ppm:F0}) rgba=({c.Red},{c.Green},{c.Blue},{c.Alpha}) water={water}");
}

Console.WriteLine($"mask {w}x{h}");
Check("fort tip LAND", 58.13216, 8.01088, false);
Check("sea south WATER", 58.12000, 8.01088, true);
Check("topdalsfjord WATER", 58.16000, 8.09000, true);
Check("skagerrak SE WATER", 58.09000, 8.15000, true);
Check("city LAND", 58.14670, 7.99560, false);
Check("otra WATER", 58.17000, 7.97500, true);
