namespace ArtillerySimulator.Game;

/// <summary>
/// Land vs water. Binary <c>water-mask.png</c> is unbreakable for where water is.
/// Heightfield has sea-connected water flattened to 0 m; inland lakes keep NDH.
/// </summary>
internal static class KristiansandSurface
{
    public static KristiansandSurfaceKind KindAt(float xMeters, float zMeters)
    {
        return KristiansandLandSeaMask.IsWater(xMeters, zMeters)
            ? KristiansandSurfaceKind.Water
            : KristiansandSurfaceKind.Land;
    }

    public static KristiansandSurfaceKind KindAt(float xMeters, float zMeters, float meters)
    {
        _ = meters;
        return KindAt(xMeters, zMeters);
    }

    /// <summary>True orthometric meters — never magnify beyond NDH.</summary>
    public static float ToDisplayHeight(float meters) => meters <= 0f ? 0f : meters;

    public static float SampleDrawHeight(float xMeters, float zMeters)
    {
        var meters = KristiansandHeightSampler.SampleMetersStatic(xMeters, zMeters);
        // Mask wins: only mask-water samples use under-water NDH; land uses land NDH.
        return ToDisplayHeight(meters);
    }
}
