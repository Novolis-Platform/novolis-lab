using System.Numerics;

namespace ArtillerySimulator.Game;

/// <summary>
/// M777-class educational scale. Published HE ranges (open sources, not classified):
/// M107 ~21 km, M795 ~23.5 km, ERFB/base-bleed ~30 km, Excalibur ~40 km;
/// max zone muzzle velocity often quoted ~827 m/s (Charge 8S).
/// Training area extent is sized so standard charges can land on terrain before the map edge.
/// </summary>
internal static class SimulationUnits
{
  /// <summary>Square training area — slightly beyond M795 range, inside ERFB class (m).</summary>
  public const float ExtentMeters = 26_000f;

  public const float GunPivotX = 100f;
  public const float GunHeightOffset = 1.6f;

  /// <summary>Educational MACS-style low / mid / high muzzle velocity (m/s).</summary>
  public static readonly float[] ChargeSpeedsMps = [427f, 594f, 780f];

  public static readonly string[] ChargeLabels = ["reduced", "standard", "max"];

  /// <summary>Typical catalog max range for same charge tier (km, open-source HE).</summary>
  public static readonly float[] ReferenceMaxRangeKm = [21f, 23.5f, 30f];

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
