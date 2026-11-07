using System.Numerics;

namespace ArtillerySimulator;

/// <summary>
/// K9 Thunder-class educational scale for the Kristiansand heritage-site defense:
/// one SPG on Odderøya covering inbound road approaches. Kvadraturen itself (~1.2 km)
/// sits inside the HE minimum; the piece fights columns on the approaches, not downtown.
/// Catalog figures are open-source approximations only.
/// </summary>
internal static class SimulationUnits
{
  /// <summary>Map east/north come from georef; this is camera/chase fallback scale.</summary>
  public const float ExtentMeters = 26_000f;

  public const float GunPivotX = 100f;
  public const float GunHeightOffset = 2.4f;

  /// <summary>
  /// Educational HE minimum for standard 155 mm (open-source M107-class band ~3.9–4.7 km).
  /// Closest legal MPI — not a classified table.
  /// </summary>
  public const float MinEffectiveRangeMeters = 4_000f;

  /// <summary>
  /// Column spawn ring from the fort: ~2.25× HE minimum so the battery has time to observe,
  /// lay, and fire before the lead vehicle closes the HE floor.
  /// </summary>
  public const float MinOpeningEngagementMeters = MinEffectiveRangeMeters * 2.25f;

  /// <summary>Fixed pool of road-network spawn mouths around the HE ring.</summary>
  public const int ApproachSpawnCount = 18;

  /// <summary>Outer city-approach band the Odderøya battery is expected to cover (m).</summary>
  public const float MaxCityApproachMeters = 14_000f;

  /// <summary>
  /// Educational modular charges (m/s), ordered reduced / standard / max / low.
  /// Reduced covers just above min range; standard/max cover outer approach waves.
  /// </summary>
  public static readonly float[] ChargeSpeedsMps = [420f, 560f, 720f, 360f];

  public static readonly string[] ChargeLabels = ["reduced", "standard", "max", "low"];

  /// <summary>Typical educational max range for reduced / standard / max (km).</summary>
  public static readonly float[] ReferenceMaxRangeKm = [10f, 16f, 24f];

  public const float FixedCamLookAhead = 2500f;
  public static readonly Vector3 FixedCamEyeOffset = new(-1200f, 700f, 900f);

  public const float ChaseCamBack = 280f;
  public const float ChaseCamUp = 120f;
  public const float ChaseCamLeadMax = 200f;

  public static string FormatRange(float meters) =>
      meters >= 1000f ? $"{meters / 1000f:F2} km" : $"{meters:F0} m";

  public static float ReferenceMaxRangeKmForCharge(int chargeIndex) =>
      ReferenceMaxRangeKm[Math.Clamp(chargeIndex, 0, ReferenceMaxRangeKm.Length - 1)];
}
