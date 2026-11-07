namespace ArtillerySimulator;

/// <summary>One sampled delivery — ideal aim plus PE residuals applied to the fired state.</summary>
internal readonly struct FiredDelivery
{
    public FiredDelivery(
        float elevationDegrees,
        float azimuthDegrees,
        float muzzleSpeedMps,
        float elevationResidualMils,
        float deflectionResidualMils,
        float muzzleResidualFraction)
    {
        ElevationDegrees = elevationDegrees;
        AzimuthDegrees = azimuthDegrees;
        MuzzleSpeedMps = muzzleSpeedMps;
        ElevationResidualMils = elevationResidualMils;
        DeflectionResidualMils = deflectionResidualMils;
        MuzzleResidualFraction = muzzleResidualFraction;
    }

    public float ElevationDegrees { get; }
    public float AzimuthDegrees { get; }
    public float MuzzleSpeedMps { get; }
    public float ElevationResidualMils { get; }
    public float DeflectionResidualMils { get; }
    public float MuzzleResidualFraction { get; }
}
