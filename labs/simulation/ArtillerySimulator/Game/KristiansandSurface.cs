namespace ArtillerySimulator.Game;

/// <summary>
/// Sea vs inland lake vs land. Draw Y is true NDH meters (no vertical exaggeration).
/// Sea is forced to Y=0. Lakes keep their orthometric height.
/// </summary>
internal static class KristiansandSurface
{
    public const float SeaMaxMeters = 2f;
    public const float MaxLakeMeters = 70f;

    private const float ClusterStepMeters = 45f;
    private const int ClusterRadius = 2;

    public static KristiansandSurfaceKind KindAt(float xMeters, float zMeters)
    {
        var meters = KristiansandHeightSampler.SampleMetersStatic(xMeters, zMeters);
        return KindAt(xMeters, zMeters, meters);
    }

    public static KristiansandSurfaceKind KindAt(float xMeters, float zMeters, float meters)
    {
        if (!KristiansandLandSeaMask.IsWater(xMeters, zMeters))
            return KristiansandSurfaceKind.Land;

        if (meters <= SeaMaxMeters)
            return KristiansandSurfaceKind.Sea;

        return IsElevatedWaterBody(xMeters, zMeters, meters)
            ? KristiansandSurfaceKind.Lake
            : KristiansandSurfaceKind.Land;
    }

    private static bool IsElevatedWaterBody(float xMeters, float zMeters, float centerMeters)
    {
        if (centerMeters > MaxLakeMeters)
            return false;

        var water = 0;
        var total = 0;
        var waterMin = centerMeters;
        var waterMax = centerMeters;
        var landSum = 0f;
        var landCount = 0;
        for (var jz = -ClusterRadius; jz <= ClusterRadius; jz++)
        {
            for (var jx = -ClusterRadius; jx <= ClusterRadius; jx++)
            {
                total++;
                var x = xMeters + jx * ClusterStepMeters;
                var z = zMeters + jz * ClusterStepMeters;
                var h = KristiansandHeightSampler.SampleMetersStatic(x, z);
                if (KristiansandLandSeaMask.IsWater(x, z))
                {
                    water++;
                    if (h < waterMin) waterMin = h;
                    if (h > waterMax) waterMax = h;
                }
                else
                {
                    landSum += h;
                    landCount++;
                }
            }
        }

        if (water < total * 0.78f)
            return false;
        if (waterMax - waterMin > 5f)
            return false;
        if (landCount > 0 && landSum / landCount < centerMeters + 2f)
            return false;

        return true;
    }

    /// <summary>True orthometric meters — never magnify beyond NDH.</summary>
    public static float ToDisplayHeight(float meters) => meters <= 0f ? 0f : meters;

    public static float SampleDrawHeight(float xMeters, float zMeters)
    {
        var meters = KristiansandHeightSampler.SampleMetersStatic(xMeters, zMeters);
        if (KindAt(xMeters, zMeters, meters) == KristiansandSurfaceKind.Sea)
            return 0f;

        return ToDisplayHeight(meters);
    }
}
