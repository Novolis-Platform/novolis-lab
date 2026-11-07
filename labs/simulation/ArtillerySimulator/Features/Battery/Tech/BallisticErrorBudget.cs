namespace ArtillerySimulator;

/// <summary>
/// Educational delivery error budget for a piece (probable error / PE terms).
/// Replace or subclass via a new <see cref="IArtilleryPieceSpec"/> — do not sprinkle magic mils in the host.
/// Values are open-source order-of-magnitude for a modern SP 155 with digital laying; not a classified table.
/// </summary>
internal sealed class BallisticErrorBudget
{
    public BallisticErrorBudget(
        float elevationPeMils,
        float deflectionPeMils,
        float muzzleVelocityPeFraction,
        float metWindResidualPeFraction,
        float envelopePeScale = 2f)
    {
        ElevationPeMils = elevationPeMils;
        DeflectionPeMils = deflectionPeMils;
        MuzzleVelocityPeFraction = muzzleVelocityPeFraction;
        MetWindResidualPeFraction = metWindResidualPeFraction;
        EnvelopePeScale = envelopePeScale;
    }

    /// <summary>Elevation laying PE (NATO mils).</summary>
    public float ElevationPeMils { get; }

    /// <summary>Deflection / azimuth laying PE (NATO mils).</summary>
    public float DeflectionPeMils { get; }

    /// <summary>Muzzle-velocity PE as a fraction of nominal MV (e.g. 0.0025 = 0.25%).</summary>
    public float MuzzleVelocityPeFraction { get; }

    /// <summary>
    /// Residual met-wind PE as a fraction of the live surface-wind magnitude
    /// (envelope / uncertainty only — live wind stays on the atmosphere clock).
    /// </summary>
    public float MetWindResidualPeFraction { get; }

    /// <summary>How many PE widths the drawn probability envelope spans (≈2 ≈ educational 95% band).</summary>
    public float EnvelopePeScale { get; }

    public float EnvelopeElevationDegrees => NatoMils.MilsToDegrees(ElevationPeMils * EnvelopePeScale);

    public float EnvelopeDeflectionDegrees => NatoMils.MilsToDegrees(DeflectionPeMils * EnvelopePeScale);

    public float EnvelopeMuzzleFraction => MuzzleVelocityPeFraction * EnvelopePeScale;

    public float EnvelopeWindFraction => MetWindResidualPeFraction * EnvelopePeScale;
}
