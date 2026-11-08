namespace ArtillerySimulator;

/// <summary>
/// Land/sea + draw heights for Oslofjorden.
/// Authority: Kartverket NHM DTM + DTM-derived water mask when present; procedural fallback otherwise.
/// </summary>
internal static class OslofjordenSurface
{
    public static bool IsWater(float x, float z) => OslofjordenLandSeaMask.IsWater(x, z);

    /// <summary>Procedural channel (fallback when NDH mask is missing).</summary>
    public static bool IsWaterProcedural(float x, float z)
    {
        var east = OslofjordenGeodesy.EastMeters;
        var north = OslofjordenGeodesy.NorthMeters;
        if (x < 0f || z < 0f || x > east || z > north)
            return true;

        if (InsideOscarsborgIsland(x, z))
            return false;

        var centerX = FjordCenterX(z);
        var halfWidth = FjordHalfWidth(z);
        if (MathF.Abs(x - centerX) < halfWidth)
            return true;

        return NearCoastalInlet(x, z, centerX, halfWidth);
    }

    public static float SampleMeters(float x, float z)
    {
        try
        {
            return OslofjordenHeightSampler.SampleMetersStatic(x, z);
        }
        catch (FileNotFoundException)
        {
            return SampleMetersProcedural(x, z);
        }
    }

    public static float SampleDrawHeight(float x, float z)
    {
        var meters = SampleMeters(x, z);
        if (IsWater(x, z))
            return 0f;
        return meters <= 0f ? 0f : meters;
    }

    private static float SampleMetersProcedural(float x, float z)
    {
        if (IsWaterProcedural(x, z))
            return 0f;

        var east = OslofjordenGeodesy.EastMeters;
        var north = OslofjordenGeodesy.NorthMeters;
        var nx = x / east;
        var nz = z / north;
        var centerX = FjordCenterX(z);
        var shoreDist = MathF.Abs(x - centerX) - FjordHalfWidth(z);
        var rise = Math.Clamp(shoreDist / 1800f, 0f, 1f);
        var hills = 18f
            + 55f * rise
            + 22f * MathF.Sin(nx * 9.1f + nz * 3.3f)
            + 14f * MathF.Sin(nx * 4.2f - nz * 7.7f);
        if (InsideOscarsborgIsland(x, z))
            hills = MathF.Max(hills, 28f + 8f * MathF.Sin(x * 0.01f));
        return Math.Clamp(hills, 2f, 140f);
    }

    private static bool InsideOscarsborgIsland(float x, float z)
    {
        var island = OslofjordenGeodesy.ToWorldMeters(
            OslofjordenGeodesy.OscarsborgLatitude,
            OslofjordenGeodesy.OscarsborgLongitude);
        var dx = x - island.X;
        var dz = z - island.Y;
        return dx * dx + dz * dz < 380f * 380f
            || (dx - 420f) * (dx - 420f) + (dz + 180f) * (dz + 180f) < 160f * 160f;
    }

    private static float FjordCenterX(float z)
    {
        var north = OslofjordenGeodesy.NorthMeters;
        var t = Math.Clamp(z / north, 0f, 1f);
        return OslofjordenGeodesy.EastMeters * (0.46f + 0.04f * MathF.Sin(t * MathF.PI * 1.6f));
    }

    private static float FjordHalfWidth(float z)
    {
        var north = OslofjordenGeodesy.NorthMeters;
        var t = Math.Clamp(z / north, 0f, 1f);
        var pinchAt = OslofjordenGeodesy.OscarsborgNorthFraction;
        var pinch = MathF.Exp(-MathF.Pow((t - pinchAt) / 0.08f, 2f));
        return 3200f - 2100f * pinch;
    }

    private static bool NearCoastalInlet(float x, float z, float centerX, float halfWidth)
    {
        var shore = halfWidth + 80f;
        var dx = MathF.Abs(x - centerX);
        if (dx < shore || dx > shore + 900f)
            return false;
        var pockets = MathF.Sin(z * 0.0011f) * MathF.Sin(x * 0.0007f);
        return pockets > 0.72f;
    }
}
