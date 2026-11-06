namespace ArtillerySimulator.Game;

/// <summary>
/// Pixel-perfect land/sea from Norgeskart reference (#5182FF). Mask matches reference image pixels;
/// scenario meters map with uniform scale and letterboxing (aspect preserved).
/// </summary>
internal static class KristiansandLandSeaMask
{
    public const byte WaterR = 0x51;
    public const byte WaterG = 0x82;
    public const byte WaterB = 0xFF;

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

        // Mask bytes and georef come from extract-artillery-map (same letterboxing as kristiansand-reference.png).
        // Do not use ArtilleryMapStackGeoref.TryWorldToReliefPixel here: stack JSON pixel sizes can differ from
        // the exported PNG, and full-bleed u/v mapping ignores offsetPx — both yield wrong rows (visible seam).
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
        var path = Path.Combine(AppContext.BaseDirectory, "map", "kristiansand-land-sea-mask.bin");
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
            _ = reader.ReadSingle(); // scenario east (m); mask georef uses ppm + offsets only
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
