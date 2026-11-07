using System.Drawing;
using System.Numerics;
using Novolis.Physics.Ballistics;

namespace ArtillerySimulator;

/// <summary>
/// Educational 155 mm / 52-caliber self-propelled howitzer preset (K9 Thunder-class).
/// Turret traverse is continuous; constants are open-source approximations only.
/// </summary>
internal sealed class GunModel
{
  /// <summary>155 mm × 52 calibers ≈ 8.06 m tube.</summary>
  public const float BarrelLength = 8.06f;

  public const float MuzzleRadius = 0.0775f;

  /// <summary>Educational K9 quadrant: roughly −45 to +1244 mil.</summary>
  public const float MinElevationDegrees = -2.5f;

  public const float MaxElevationDegrees = 70f;

  /// <summary>Full turret (±180°) — not trailed carriage stops.</summary>
  public const float MaxTraverseDegrees = 180f;

  public const bool ContinuousTraverse = true;

  public float ElevationDegrees { get; private set; } = 45f;

  /// <summary>World bearing. Azimuth 0 fires east (+X). Turret covers ±<see cref="MaxTraverseDegrees"/> around hull <see cref="CenterAzimuthDegrees"/>.</summary>
  public float AzimuthDegrees { get; private set; }

  /// <summary>Hull / preferred-sector center (opening lay).</summary>
  public float CenterAzimuthDegrees { get; private set; }

  public int ChargeIndex { get; private set; } = 1;

  public float ElevationMils => ElevationDegrees * 6400f / 360f;

  public float AzimuthMils => AzimuthDegrees * 6400f / 360f;

  public float DeflectionDegrees => AzimuthDegrees - CenterAzimuthDegrees;

  public float DeflectionMils => DeflectionDegrees * 6400f / 360f;

  public bool AtTraverseStop =>
      !ContinuousTraverse && MathF.Abs(Wrap180(DeflectionDegrees)) >= MaxTraverseDegrees - 0.05f;

  public float MuzzleSpeedMps => SimulationUnits.ChargeSpeedsMps[ChargeIndex];

  public string ChargeLabel => SimulationUnits.ChargeLabels[ChargeIndex];

  public static ProjectileProfile Educational155Profile =>
      new(massKg: 47f, referenceAreaM2: Math.PI * 0.155f * 0.155f / 4.0, dragCoefficient: 0.42);

  public void NudgeElevation(float delta) =>
      ElevationDegrees = Math.Clamp(ElevationDegrees + delta, MinElevationDegrees, MaxElevationDegrees);

  public void SetElevation(float degrees) =>
      ElevationDegrees = Math.Clamp(degrees, MinElevationDegrees, MaxElevationDegrees);

  public void LayCenter(float worldAzimuthDegrees)
  {
    CenterAzimuthDegrees = worldAzimuthDegrees;
    AzimuthDegrees = worldAzimuthDegrees;
  }

  public void NudgeAzimuth(float delta)
  {
    var deflection = Math.Clamp(Wrap180(DeflectionDegrees + delta), -MaxTraverseDegrees, MaxTraverseDegrees);
    AzimuthDegrees = CenterAzimuthDegrees + deflection;
  }

  public void SetCharge(int index) =>
      ChargeIndex = Math.Clamp(index, 0, SimulationUnits.ChargeSpeedsMps.Length - 1);

  public Vector3 BarrelDirection() => DirectionOf(ElevationDegrees, AzimuthDegrees);

  public static Vector3 DirectionOf(float elevationDegrees, float azimuthDegrees)
  {
    var elev = elevationDegrees * (MathF.PI / 180f);
    var az = azimuthDegrees * (MathF.PI / 180f);
    var cosE = MathF.Cos(elev);
    return Vector3.Normalize(new Vector3(cosE * MathF.Cos(az), MathF.Sin(elev), cosE * MathF.Sin(az)));
  }

  public Vector3 MuzzlePosition(Vector3 gunPivot) =>
      gunPivot + BarrelDirection() * BarrelLength;

  public ProjectileState CreateProjectileState(Vector3 muzzle, Vector3 barrelDir) =>
      new(muzzle, barrelDir * MuzzleSpeedMps, Educational155Profile.MassKg, timeSeconds: 0);

  public ProjectileState PredictState(Vector3 gunPivot, float elevationDegrees, float azimuthDegrees, float muzzleSpeedMps)
  {
    var direction = DirectionOf(elevationDegrees, azimuthDegrees);
    return new ProjectileState(gunPivot + direction * BarrelLength, direction * muzzleSpeedMps, Educational155Profile.MassKg, timeSeconds: 0);
  }

  public void Draw(SilkWireBatch wires, Vector3 gunPivot, bool showPivotGlow, bool loading)
  {
    var dir = BarrelDirection();
    var muzzle = MuzzlePosition(gunPivot);
    var hullPaint = Color.FromArgb(255, 62, 78, 58);
    var turretPaint = Color.FromArgb(255, 88, 102, 72);
    var tube = Color.FromArgb(255, 186, 176, 154);
    var copper = Color.FromArgb(255, 210, 132, 72);

    var hullForward = DirectionOf(0f, CenterAzimuthDegrees);
    var hullRight = Lateral(hullForward);
    var tubeRight = Lateral(dir);
    var deck = gunPivot + new Vector3(0f, 0.55f, 0f);
    var turret = deck + new Vector3(0f, 0.85f, 0f);
    var trunnion = turret + hullForward * 0.35f + new Vector3(0f, 0.35f, 0f);

    // Tracked hull (wire box aligned to hull heading).
    DrawBox(wires, deck, hullForward, hullRight, 3.6f, 1.55f, 1.05f, hullPaint);
    // Side skirts / track lines.
    wires.Line(deck - hullRight * 1.7f - hullForward * 3.2f, deck - hullRight * 1.7f + hullForward * 3.2f, hullPaint);
    wires.Line(deck + hullRight * 1.7f - hullForward * 3.2f, deck + hullRight * 1.7f + hullForward * 3.2f, hullPaint);

    // Turret + bustle.
    DrawBox(wires, turret, hullForward, hullRight, 1.9f, 1.35f, 0.75f, turretPaint);
    DrawBox(wires, turret - hullForward * 1.5f, hullForward, hullRight, 1.1f, 1.15f, 0.55f, turretPaint);
    wires.AxisRing(turret, Vector3.UnitY, 1.05f, turretPaint);

    // L/52 tube from trunnion.
    DrawBundle(wires, trunnion, muzzle, dir, 0.095f, tube);
    wires.AxisRing(muzzle, dir, 0.17f, copper);
    wires.AxisRing(trunnion, dir, 0.14f, turretPaint);
    wires.Line(trunnion + tubeRight * 0.55f, trunnion - tubeRight * 0.55f, turretPaint);

    // Vertical fort marker so the piece is visible from altitude.
    wires.Line(gunPivot, gunPivot + Vector3.UnitY * 18f, copper);

    if (loading)
      wires.WireSphere(trunnion, 3.2f, copper);
    else if (showPivotGlow)
      wires.WireSphere(muzzle, 2.4f, copper);
  }

  private static float Wrap180(float degrees)
  {
    while (degrees > 180f)
      degrees -= 360f;
    while (degrees < -180f)
      degrees += 360f;
    return degrees;
  }

  private static Vector3 Lateral(Vector3 dir)
  {
    var right = Vector3.Cross(dir, Vector3.UnitY);
    return right.LengthSquared() < 1e-6f ? Vector3.UnitZ : Vector3.Normalize(right);
  }

  private static void DrawBox(
      SilkWireBatch wires,
      Vector3 center,
      Vector3 forward,
      Vector3 right,
      float halfLength,
      float halfWidth,
      float halfHeight,
      Color color)
  {
    var up = Vector3.UnitY;
    var f = forward * halfLength;
    var r = right * halfWidth;
    var u = up * halfHeight;
    var c000 = center - f - r - u;
    var c001 = center - f - r + u;
    var c010 = center - f + r - u;
    var c011 = center - f + r + u;
    var c100 = center + f - r - u;
    var c101 = center + f - r + u;
    var c110 = center + f + r - u;
    var c111 = center + f + r + u;
    wires.Line(c000, c100, color);
    wires.Line(c010, c110, color);
    wires.Line(c001, c101, color);
    wires.Line(c011, c111, color);
    wires.Line(c000, c010, color);
    wires.Line(c100, c110, color);
    wires.Line(c001, c011, color);
    wires.Line(c101, c111, color);
    wires.Line(c000, c001, color);
    wires.Line(c010, c011, color);
    wires.Line(c100, c101, color);
    wires.Line(c110, c111, color);
  }

  private static void DrawBundle(SilkWireBatch wires, Vector3 from, Vector3 to, Vector3 axis, float radius, Color color)
  {
    var side = Lateral(axis);
    var up = Vector3.Normalize(Vector3.Cross(axis, side));
    wires.Line(from, to, color);
    wires.Line(from + side * radius, to + side * radius, color);
    wires.Line(from - side * radius, to - side * radius, color);
    wires.Line(from + up * radius, to + up * radius, color);
  }
}
