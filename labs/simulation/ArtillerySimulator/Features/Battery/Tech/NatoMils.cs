namespace ArtillerySimulator;

/// <summary>NATO mil helpers (6400 mil = 360°). Educational fire-control math, not a classified table.</summary>
internal static class NatoMils
{
    public const float PerCircle = 6400f;
    public const float DegreesPerMil = 360f / PerCircle;
    public const float MilsPerDegree = PerCircle / 360f;

    /// <summary>Probable error ↔ Gaussian σ. PE contains ~50% of samples; σ ≈ PE / 0.6745.</summary>
    public const float PeToSigma = 1f / 0.6745f;

    public static float DegreesToMils(float degrees) => degrees * MilsPerDegree;

    public static float MilsToDegrees(float mils) => mils * DegreesPerMil;

    public static float SigmaFromPe(float pe) => pe * PeToSigma;
}
