using System.Numerics;

namespace ArtillerySimulator;

/// <summary>
/// Mission / map scale for the Kristiansand heritage-site defense.
/// Piece MV, PE, and tube limits live on <see cref="IArtilleryPieceSpec"/> via <see cref="ArtilleryPieceCatalog"/>.
/// Charge ordinal arrays here stay as thin shims so existing UI/agent code keeps working.
/// </summary>
internal static class SimulationUnits
{
    public const float ExtentMeters = 26_000f;

    public const float GunPivotX = 100f;

    public static float GunHeightOffset => ArtilleryPieceCatalog.Default.GunHeightOffsetMeters;

    public static float MinEffectiveRangeMeters => ArtilleryPieceCatalog.Default.MinEffectiveRangeMeters;

    public static float LaunchClearanceMeters => ArtilleryPieceCatalog.Default.LaunchClearanceMeters;

    public static float MinOpeningEngagementMeters => MinEffectiveRangeMeters * 2.25f;

    public const int ApproachSpawnCount = 18;

    public const float MaxCityApproachMeters = 14_000f;

    public static float[] ChargeSpeedsMps { get; } =
        ArtilleryPieceCatalog.Default.Charges.Select(static c => c.MuzzleSpeedMps).ToArray();

    public static string[] ChargeLabels { get; } =
        ArtilleryPieceCatalog.Default.Charges.Select(static c => c.Label).ToArray();

    public static float[] ReferenceMaxRangeKm { get; } =
        ArtilleryPieceCatalog.Default.Charges
            .Where(static c => c.Id is not "low")
            .Select(static c => c.ReferenceMaxRangeKm)
            .ToArray();

    public const float FixedCamLookAhead = 2500f;
    public static readonly Vector3 FixedCamEyeOffset = new(-1200f, 700f, 900f);

    public const float ChaseCamBack = 280f;
    public const float ChaseCamUp = 120f;
    public const float ChaseCamLeadMax = 200f;

    public static string FormatRange(float meters) =>
        meters >= 1000f ? $"{meters / 1000f:F2} km" : $"{meters:F0} m";

    public static float ReferenceMaxRangeKmForCharge(int chargeIndex) =>
        ArtilleryPieceCatalog.Default.ChargeAt(chargeIndex).ReferenceMaxRangeKm;
}
