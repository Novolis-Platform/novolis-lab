namespace ArtillerySimulator;

/// <summary>One corner of the educational probability envelope (degrees / fractions, not PE units).</summary>
internal readonly struct EnvelopeOffset
{
    public EnvelopeOffset(float elevationDegrees, float azimuthDegrees, float muzzleFraction, float windFraction)
    {
        ElevationDegrees = elevationDegrees;
        AzimuthDegrees = azimuthDegrees;
        MuzzleFraction = muzzleFraction;
        WindFraction = windFraction;
    }

    public float ElevationDegrees { get; }
    public float AzimuthDegrees { get; }
    public float MuzzleFraction { get; }
    public float WindFraction { get; }
}
