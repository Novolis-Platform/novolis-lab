#:property OutputType=Exe
#:property TargetFramework=net10.0
#:property ImplicitUsings=enable
#:property Nullable=enable
#:property ManagePackageVersionsCentrally=false
#:package SkiaSharp@4.153.1

using SkiaSharp;

// Rebuild kristiansand-land-sea-mask.bin from PDF water-mask.png (authoritative).
var mapDir = @"d:\novolis\novolis-lab\labs\simulation\ArtillerySimulator\map";
var pngPath = Path.Combine(mapDir, "layers", "water-mask.png");
var binPath = Path.Combine(mapDir, "kristiansand-land-sea-mask.bin");
const float east = 24008.08f;
const float north = 16266.16f;

using var bitmap = SKBitmap.Decode(pngPath)
    ?? throw new FileNotFoundException(pngPath);

var w = bitmap.Width;
var h = bitmap.Height;
var water = new byte[w * h];
var count = 0;
for (var y = 0; y < h; y++)
{
    for (var x = 0; x < w; x++)
    {
        // Same rule as build-kristiansand-map: opaque = water, transparent = land.
        if (bitmap.GetPixel(x, y).Alpha <= 0)
            continue;
        water[y * w + x] = 1;
        count++;
    }
}

var ppm = (w - 1) / east;
using (var stream = File.Create(binPath))
using (var writer = new BinaryWriter(stream))
{
    writer.Write(0x4D534C4Bu);
    writer.Write((ushort)1);
    writer.Write((ushort)w);
    writer.Write((ushort)h);
    writer.Write(ppm);
    writer.Write(0f);
    writer.Write(0f);
    writer.Write(east);
    writer.Write(north);
    writer.Write(water);
}

Console.WriteLine($"Wrote {binPath}");
Console.WriteLine($"{w}x{h}, water pixels={count} ({100.0 * count / (w * h):F1}%), ppm={ppm:F4}");
