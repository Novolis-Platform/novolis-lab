#:property TargetFramework=net10.0
#:property ManagePackageVersionsCentrally=false
#:package SkiaSharp@3.119.1

// Bake PNG + ICO from the generated splash/icon JPGs for ApplicationIcon and overlays.
//   dotnet run --file d:\novolis\novolis-lab\labs\simulation\ArtillerySimulator\scripts\bake-k9-dog-icons.cs

using SkiaSharp;

var assets = @"d:\novolis\novolis-lab\labs\simulation\ArtillerySimulator\assets";
BakePng(Path.Combine(assets, "k9-dog-icon.jpg"), Path.Combine(assets, "k9-dog-icon.png"), 256);
BakePng(Path.Combine(assets, "k9-dog-splash.jpg"), Path.Combine(assets, "k9-dog-splash.png"), maxEdge: 1920);
BakeIco(Path.Combine(assets, "k9-dog-icon.png"), Path.Combine(assets, "k9-dog-icon.ico"), [256, 48, 32, 16]);
Console.WriteLine("Baked PNG + ICO into assets/");

static void BakePng(string src, string dst, int? size = null, int? maxEdge = null)
{
    using var bmp = SKBitmap.Decode(src) ?? throw new FileNotFoundException(src);
    SKBitmap work = bmp;
    SKBitmap? scaled = null;
    if (size is int s)
    {
        scaled = bmp.Resize(new SKImageInfo(s, s), SKSamplingOptions.Default)
            ?? throw new InvalidOperationException("resize failed");
        work = scaled;
    }
    else if (maxEdge is int edge && Math.Max(bmp.Width, bmp.Height) > edge)
    {
        var scale = edge / (float)Math.Max(bmp.Width, bmp.Height);
        scaled = bmp.Resize(
            new SKImageInfo((int)(bmp.Width * scale), (int)(bmp.Height * scale)),
            SKSamplingOptions.Default) ?? throw new InvalidOperationException("resize failed");
        work = scaled;
    }

    using var image = SKImage.FromBitmap(work);
    using var data = image.Encode(SKEncodedImageFormat.Png, 90);
    File.WriteAllBytes(dst, data.ToArray());
    scaled?.Dispose();
    Console.WriteLine($"PNG {dst}");
}

static void BakeIco(string pngPath, string icoPath, int[] sizes)
{
    using var src = SKBitmap.Decode(pngPath) ?? throw new FileNotFoundException(pngPath);
    var images = new List<byte[]>();
    foreach (var size in sizes)
    {
        using var resized = src.Resize(new SKImageInfo(size, size), SKSamplingOptions.Default)
            ?? throw new InvalidOperationException("ico resize");
        using var image = SKImage.FromBitmap(resized);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        images.Add(data.ToArray());
    }

    using var ms = new MemoryStream();
    using var bw = new BinaryWriter(ms);
    bw.Write((ushort)0);
    bw.Write((ushort)1);
    bw.Write((ushort)images.Count);
    var offset = 6 + 16 * images.Count;
    for (var i = 0; i < images.Count; i++)
    {
        var size = sizes[i];
        bw.Write((byte)(size >= 256 ? 0 : size));
        bw.Write((byte)(size >= 256 ? 0 : size));
        bw.Write((byte)0);
        bw.Write((byte)0);
        bw.Write((ushort)1);
        bw.Write((ushort)32);
        bw.Write(images[i].Length);
        bw.Write(offset);
        offset += images[i].Length;
    }

    foreach (var png in images)
        bw.Write(png);

    File.WriteAllBytes(icoPath, ms.ToArray());
    Console.WriteLine($"ICO {icoPath}");
}
