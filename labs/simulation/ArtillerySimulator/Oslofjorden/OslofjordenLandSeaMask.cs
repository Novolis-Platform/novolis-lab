namespace ArtillerySimulator;

/// <summary>DTM-derived land/sea mask for the Oslofjorden cutout (KLSM binary).</summary>
internal static class OslofjordenLandSeaMask
{
    private const uint Magic = 0x4D534C4Bu; // KLSM

    private static byte[]? _water;
    private static int _width;
    private static int _height;
    private static float _pixelsPerMeter;
    private static float _east;
    private static float _north;
    private static bool _attempted;

    public static bool IsAvailable => EnsureLoaded() && _water is { Length: > 0 };

    public static bool IsWater(float xMeters, float zMeters)
    {
        if (!EnsureLoaded() || _water is null)
            return OslofjordenSurface.IsWaterProcedural(xMeters, zMeters);

        var ix = (int)MathF.Round(xMeters * _pixelsPerMeter);
        var iy = (int)MathF.Round((_north - zMeters) * _pixelsPerMeter);
        if (ix < 0 || iy < 0 || ix >= _width || iy >= _height)
            return true;

        return _water[iy * _width + ix] != 0;
    }

    private static bool EnsureLoaded()
    {
        if (_attempted)
            return _water is not null;
        _attempted = true;

        foreach (var path in CandidatePaths())
        {
            if (!File.Exists(path))
                continue;
            if (TryLoad(path))
                return true;
        }

        return false;
    }

    private static IEnumerable<string> CandidatePaths()
    {
        yield return Path.Combine(AppContext.BaseDirectory, "map", "oslofjorden", "oslofjorden-land-sea-mask.bin");
        yield return Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "map", "oslofjorden", "oslofjorden-land-sea-mask.bin"));
        yield return @"d:\novolis\novolis-lab\labs\simulation\ArtillerySimulator\map\oslofjorden\oslofjorden-land-sea-mask.bin";
    }

    private static bool TryLoad(string path)
    {
        try
        {
            using var fs = File.OpenRead(path);
            using var br = new BinaryReader(fs);
            if (br.ReadUInt32() != Magic)
                return false;
            _ = br.ReadUInt16(); // version
            _width = br.ReadUInt16();
            _height = br.ReadUInt16();
            _pixelsPerMeter = br.ReadSingle();
            _ = br.ReadSingle();
            _ = br.ReadSingle();
            _east = br.ReadSingle();
            _north = br.ReadSingle();
            var need = _width * _height;
            _water = br.ReadBytes(need);
            return _water.Length == need;
        }
        catch
        {
            _water = null;
            return false;
        }
    }
}
