using SkiaSharp;

namespace ArtillerySimulator.Game;

/// <summary>
/// PDF Norgeskart water silhouette — authoritative land/sea/lake/pond delineation.
/// Prefers binary <c>map/layers/water-mask.png</c> (Alpha&gt;0 = water) — unbreakable where-water.
/// Falls back to packed <c>kristiansand-land-sea-mask.bin</c>.
/// Height shades: <c>water-heightmap.png</c>; mask-gated composite: <c>water-combined.png</c>.
/// </summary>
internal static class KristiansandLandSeaMask
{
    private const uint Magic = 0x4D534C4Bu; // KLSM

    private static byte[]? _water;
    private static int _imageWidth;
    private static int _imageHeight;
    private static float _pixelsPerMeter;
    private static float _offsetPxX;
    private static float _offsetPxY;
    private static float _scenarioNorthMeters;
    private static bool _loadAttempted;

    public static bool IsAvailable => EnsureLoaded() && _water is { Length: > 0 };

    public static bool IsWater(float xMeters, float zMeters)
    {
        if (!EnsureLoaded() || _water is null)
            return false;

        var px = KristiansandGeoref.WorldToDataX(xMeters, KristiansandHeightSampler.EastMeters) * _pixelsPerMeter + _offsetPxX;
        var py = _offsetPxY + (_scenarioNorthMeters - zMeters) * _pixelsPerMeter;
        var ix = (int)MathF.Round(px);
        var iy = (int)MathF.Round(py);
        if (ix < 0 || iy < 0 || ix >= _imageWidth || iy >= _imageHeight)
            return false;

        return _water[iy * _imageWidth + ix] != 0;
    }

    private static bool EnsureLoaded()
    {
        if (_loadAttempted)
            return _water is not null;

        _loadAttempted = true;
        foreach (var path in CandidatePngPaths())
        {
            if (TryLoadPng(path))
                return true;
        }

        foreach (var path in CandidateBinPaths())
        {
            if (TryLoadBin(path))
                return true;
        }

        return false;
    }

    private static IEnumerable<string> CandidatePngPaths()
    {
        yield return Path.Combine(AppContext.BaseDirectory, "map", "layers", "water-mask.png");
        yield return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "map", "layers", "water-mask.png"));
        yield return @"d:\novolis\novolis-lab\labs\simulation\ArtillerySimulator\map\layers\water-mask.png";
    }

    private static IEnumerable<string> CandidateBinPaths()
    {
        yield return Path.Combine(AppContext.BaseDirectory, "map", "kristiansand-land-sea-mask.bin");
        yield return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "map", "kristiansand-land-sea-mask.bin"));
        yield return @"d:\novolis\novolis-lab\labs\simulation\ArtillerySimulator\map\kristiansand-land-sea-mask.bin";
    }

    private static bool TryLoadPng(string path)
    {
        if (!File.Exists(path))
            return false;

        try
        {
            using var bitmap = SKBitmap.Decode(path);
            if (bitmap is null || bitmap.Width < 8 || bitmap.Height < 8)
                return false;

            var w = bitmap.Width;
            var h = bitmap.Height;
            var water = new byte[w * h];
            var waterCount = 0;
            for (var y = 0; y < h; y++)
            {
                for (var x = 0; x < w; x++)
                {
                    var c = bitmap.GetPixel(x, y);
                    // Same rule as build-kristiansand-map: water-mask.png stores opaque water, transparent land.
                    if (c.Alpha <= 0)
                        continue;
                    water[y * w + x] = 1;
                    waterCount++;
                }
            }

            if (waterCount < w * h / 50)
                return false;

            var east = KristiansandHeightSampler.EastMeters;
            var north = KristiansandHeightSampler.NorthMeters;
            _imageWidth = w;
            _imageHeight = h;
            _pixelsPerMeter = (w - 1) / east;
            _offsetPxX = 0f;
            _offsetPxY = 0f;
            _scenarioNorthMeters = north;
            _water = water;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryLoadBin(string path)
    {
        if (!File.Exists(path))
            return false;

        try
        {
            using var stream = File.OpenRead(path);
            using var reader = new BinaryReader(stream);
            if (reader.ReadUInt32() != Magic || reader.ReadUInt16() != 1)
                return false;

            _imageWidth = reader.ReadUInt16();
            _imageHeight = reader.ReadUInt16();
            _pixelsPerMeter = reader.ReadSingle();
            _offsetPxX = reader.ReadSingle();
            _offsetPxY = reader.ReadSingle();
            _ = reader.ReadSingle();
            _scenarioNorthMeters = reader.ReadSingle();
            var count = _imageWidth * _imageHeight;
            _water = reader.ReadBytes(count);
            return _water.Length == count;
        }
        catch
        {
            _water = null;
            return false;
        }
    }
}
