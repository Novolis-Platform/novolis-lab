using System.Drawing;
using System.Numerics;
using Novolis.Physics.Ballistics;

namespace ArtillerySimulator;

/// <summary>Simplified 155 mm towed gun preset (M777-class, educational constants only).</summary>
internal sealed class GunModel
{
  public const float BarrelLength = 5.2f;
  public const float MuzzleRadius = 0.0775f;

  /// <summary>Educational M777 quadrant: about −43 to +1275 mil.</summary>
  public const float MinElevationDegrees = -2.4f;

  public const float MaxElevationDegrees = 71.7f;

  /// <summary>±400 mil on the trails. The piece does not slew like a turret.</summary>
  public const float MaxTraverseDegrees = 22.5f;

  public float ElevationDegrees { get; private set; } = 45f;

  /// <summary>World bearing. Azimuth 0 fires east (+X). The trails only cover ±<see cref="MaxTraverseDegrees"/> around <see cref="CenterAzimuthDegrees"/>.</summary>
  public float AzimuthDegrees { get; private set; }

  public float CenterAzimuthDegrees { get; private set; }
  public int ChargeIndex { get; private set; } = 1;

  public float ElevationMils => ElevationDegrees * 6400f / 360f;

  public float AzimuthMils => AzimuthDegrees * 6400f / 360f;

  public float DeflectionDegrees => AzimuthDegrees - CenterAzimuthDegrees;

  public float DeflectionMils => DeflectionDegrees * 6400f / 360f;

  public bool AtTraverseStop => MathF.Abs(DeflectionDegrees) >= MaxTraverseDegrees - 0.05f;

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
    var deflection = Math.Clamp(DeflectionDegrees + delta, -MaxTraverseDegrees, MaxTraverseDegrees);
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
    var carriage = Color.FromArgb(255, 78, 86, 64);
    var tube = Color.FromArgb(255, 186, 176, 154);
    var copper = Color.FromArgb(255, 210, 132, 72);
    var right = Lateral(dir);
    var horizontal = Vector3.Normalize(new Vector3(dir.X, 0f, dir.Z));
    var trunnion = gunPivot + new Vector3(0f, 0.95f, 0f);
    var axle = gunPivot - new Vector3(0f, 0.85f, 0f);

    DrawBundle(wires, trunnion, muzzle, dir, 0.09f, tube);
    wires.AxisRing(muzzle, dir, 0.16f, copper);
    wires.AxisRing(trunnion, dir, 0.12f, carriage);

    var trailEnd = gunPivot - horizontal * 3.6f - new Vector3(0f, 0.55f, 0f);
    DrawBundle(wires, axle + right * 0.35f, trailEnd + right * 0.7f, horizontal, 0.05f, carriage);
    DrawBundle(wires, axle - right * 0.35f, trailEnd - right * 0.7f, horizontal, 0.05f, carriage);
    wires.AxisRing(axle + right * 1.15f, right, 0.72f, carriage);
    wires.AxisRing(axle - right * 1.15f, right, 0.72f, carriage);

    var shield = trunnion + horizontal * 0.45f;
    wires.Line(shield + right * 0.85f + Vector3.UnitY * 0.55f, shield - right * 0.85f + Vector3.UnitY * 0.55f, copper);
    wires.Line(shield + right * 0.85f - Vector3.UnitY * 0.15f, shield - right * 0.85f - Vector3.UnitY * 0.15f, carriage);
    wires.Line(shield + right * 0.85f + Vector3.UnitY * 0.55f, shield + right * 0.85f - Vector3.UnitY * 0.15f, carriage);
    wires.Line(shield - right * 0.85f + Vector3.UnitY * 0.55f, shield - right * 0.85f - Vector3.UnitY * 0.15f, carriage);

    // Vertical fort marker so the piece is visible from altitude.
    wires.Line(gunPivot, gunPivot + Vector3.UnitY * 18f, copper);

    if (loading)
      wires.WireSphere(trunnion, 3.2f, copper);
    else if (showPivotGlow)
      wires.WireSphere(muzzle, 2.4f, copper);
  }

  private static Vector3 Lateral(Vector3 dir)
  {
    var right = Vector3.Cross(dir, Vector3.UnitY);
    return right.LengthSquared() < 1e-6f ? Vector3.UnitZ : Vector3.Normalize(right);
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
