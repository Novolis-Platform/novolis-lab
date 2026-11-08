using System.Numerics;

namespace ArtillerySimulator;

/// <summary>
/// Mission / map scale constants. Piece MV, PE, tube limits, and warhead radii live on
/// <see cref="IArtilleryPieceSpec"/> — pass the active piece; do not read the catalog default.
/// </summary>
internal static class SimulationUnits
{
    public const float ExtentMeters = 26_000f;

    public const float GunPivotX = 100f;

    public const int ApproachSpawnCount = 18;

    public const float MaxCityApproachMeters = 14_000f;

    public const float FixedCamLookAhead = 2500f;
    public static readonly Vector3 FixedCamEyeOffset = new(-1200f, 700f, 900f);

    public const float ChaseCamBack = 280f;
    public const float ChaseCamUp = 120f;
    public const float ChaseCamLeadMax = 200f;

    public static float MinOpeningEngagementMeters(IArtilleryPieceSpec piece) =>
        piece.MinEffectiveRangeMeters * 2.25f;

    public static string FormatRange(float meters) =>
        meters >= 1000f ? $"{meters / 1000f:F2} km" : $"{meters:F0} m";
}
