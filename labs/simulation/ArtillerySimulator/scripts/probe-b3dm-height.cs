#:property OutputType=Exe
#:property TargetFramework=net10.0
#:property ImplicitUsings=enable
#:property Nullable=enable

using System.Numerics;
using System.Text;
using System.Text.Json;

var path = @"d:\novolis\novolis-lab\labs\simulation\ArtillerySimulator\map\layers\_probe2.b3dm";
var bytes = File.ReadAllBytes(path);
var ftJsonLen = BitConverter.ToUInt32(bytes, 12);
var ftBinLen = BitConverter.ToUInt32(bytes, 16);
var btJsonLen = BitConverter.ToUInt32(bytes, 20);
var btBinLen = BitConverter.ToUInt32(bytes, 24);
var ft = Encoding.UTF8.GetString(bytes, 28, (int)ftJsonLen).TrimEnd('\0');
using var ftDoc = JsonDocument.Parse(ft);
var rtcEl = ftDoc.RootElement.GetProperty("RTC_CENTER");
var rtc = new Vector3((float)rtcEl[0].GetDouble(), (float)rtcEl[1].GetDouble(), (float)rtcEl[2].GetDouble());

var glb = 28 + (int)ftJsonLen + (int)ftBinLen + (int)btJsonLen + (int)btBinLen;
while (Encoding.ASCII.GetString(bytes, glb, 4) != "glTF") glb++;
var jsonLen = BitConverter.ToInt32(bytes, glb + 12);
var root = JsonDocument.Parse(Encoding.UTF8.GetString(bytes, glb + 20, jsonLen)).RootElement;
var binStart = glb + 20 + jsonLen + 8;

// Y-up → Z-up matrix from node 0
var m = new Matrix4x4(1, 0, 0, 0, 0, 0, 1, 0, 0, -1, 0, 0, 0, 0, 0, 1); // after correct colmajor unpack of [1,0,0,0, 0,0,-1,0, 0,1,0,0, 0,0,0,1]
// verify unpack:
// glTF [1,0,0,0, 0,0,-1,0, 0,1,0,0, 0,0,0,1]
// rows: (1,0,0,0), (0,0,1,0), (0,-1,0,0), (0,0,0,1) wait
// M11=1 M12=0 M13=0 M14=0
// M21=0 M22=0 M23=1 M24=0  → from glTF[1], glTF[5]=0, glTF[9]=1, glTF[13]=0
// Actually correct unpack:
m = new Matrix4x4(
    1, 0, 0, 0,
    0, 0, 1, 0,
    0, -1, 0, 0,
    0, 0, 0, 1);
// Vector (0,10,0) Y-up → transform: 
var test = Vector3.Transform(new Vector3(0, 10, 0), m);
Console.WriteLine($"Y-up 10 → {test} (expect Z-up like (0,0,-10) or (0,10,0) depending)");

var posIdx = root.GetProperty("meshes")[0].GetProperty("primitives")[0].GetProperty("attributes").GetProperty("POSITION").GetInt32();
var acc = root.GetProperty("accessors")[posIdx];
var bv = root.GetProperty("bufferViews")[acc.GetProperty("bufferView").GetInt32()];
var start = binStart + bv.GetProperty("byteOffset").GetInt32() + (acc.TryGetProperty("byteOffset", out var bo) ? bo.GetInt32() : 0);
var stride = bv.GetProperty("byteStride").GetInt32();
var count = Math.Min(12, acc.GetProperty("count").GetInt32());

double MinH = double.MaxValue, MaxH = double.MinValue;
for (var i = 0; i < count; i++)
{
    var o = start + i * stride;
    var lx = BitConverter.ToSingle(bytes, o);
    var ly = BitConverter.ToSingle(bytes, o + 4);
    var lz = BitConverter.ToSingle(bytes, o + 8);
    var local = new Vector3(lx, ly, lz);
    var zup = Vector3.Transform(local, m);
    var ecef = rtc + zup;
    EcefToGeodetic(ecef.X, ecef.Y, ecef.Z, out _, out _, out var h);
    if (h < MinH) MinH = h;
    if (h > MaxH) MaxH = h;
    if (i < 6)
        Console.WriteLine($"  raw=({lx:F1},{ly:F1},{lz:F1}) zup={zup} hEll={h:F1}");
}

Console.WriteLine($"hEll range sample {MinH:F1}..{MaxH:F1}");
EcefToGeodetic(rtc.X, rtc.Y, rtc.Z, out var rlat, out var rlon, out var rh);
Console.WriteLine($"RTC lat={rlat:F6} lon={rlon:F6} h={rh:F3}");

static void EcefToGeodetic(double x, double y, double z, out double latDeg, out double lonDeg, out double h)
{
    const double A = 6378137.0;
    const double E2 = 6.69437999014e-3;
    var lon = Math.Atan2(y, x);
    var p = Math.Sqrt(x * x + y * y);
    var lat = Math.Atan2(z, p * (1.0 - E2));
    for (var i = 0; i < 12; i++)
    {
        var sinLat = Math.Sin(lat);
        var n = A / Math.Sqrt(1.0 - E2 * sinLat * sinLat);
        h = p / Math.Cos(lat) - n;
        lat = Math.Atan2(z, p * (1.0 - E2 * n / (n + h)));
    }

    var sinL = Math.Sin(lat);
    var nFinal = A / Math.Sqrt(1.0 - E2 * sinL * sinL);
    h = p / Math.Cos(lat) - nFinal;
    latDeg = lat * (180.0 / Math.PI);
    lonDeg = lon * (180.0 / Math.PI);
}
