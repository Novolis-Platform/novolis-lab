using System.Drawing;
using System.Numerics;
using Novolis.Physics.Ballistics;
using Novolis.Raylib.Game;

namespace ArtillerySimulator.Game;

/// <summary>Simplified 155 mm towed gun preset (M777-class, educational constants only).</summary>
internal sealed class GunModel
{
  public const float BarrelLength = 5.2f;
  public const float MuzzleRadius = 0.0775f;

  public float ElevationDegrees { get; private set; } = 45f;
  public float AzimuthDegrees { get; private set; } = 0f;
  public int ChargeIndex { get; private set; } = 1;
  public bool DragEnabled { get; private set; } = true;

  public float MuzzleSpeedMps => SimulationUnits.ChargeSpeedsMps[ChargeIndex];

  public string ChargeLabel => SimulationUnits.ChargeLabels[ChargeIndex];

  public static ProjectileProfile Educational155Profile =>
      new(massKg: 47f, referenceAreaM2: Math.PI * 0.155f * 0.155f / 4.0, dragCoefficient: 0.42);

  public void NudgeElevation(float delta) => ElevationDegrees = Math.Clamp(ElevationDegrees + delta, -3f, 75f);

  public void NudgeAzimuth(float delta) => AzimuthDegrees = (AzimuthDegrees + delta + 360f) % 360f;

  public void SetCharge(int index) =>
      ChargeIndex = Math.Clamp(index, 0, SimulationUnits.ChargeSpeedsMps.Length - 1);

  public void ToggleDrag() => DragEnabled = !DragEnabled;

  public Vector3 BarrelDirection()
  {
    var elev = ElevationDegrees * (MathF.PI / 180f);
    var az = AzimuthDegrees * (MathF.PI / 180f);
    var cosE = MathF.Cos(elev);
    return Vector3.Normalize(new Vector3(cosE * MathF.Cos(az), MathF.Sin(elev), cosE * MathF.Sin(az)));
  }

  public Vector3 MuzzlePosition(Vector3 gunPivot) =>
      gunPivot + BarrelDirection() * BarrelLength;

  public ProjectileState CreateProjectileState(Vector3 muzzle, Vector3 barrelDir) =>
      new(muzzle, barrelDir * MuzzleSpeedMps, Educational155Profile.MassKg, timeSeconds: 0);

  public void Draw(RayGameContext ctx, Vector3 gunPivot, bool showPivotGlow)
  {
    var dir = BarrelDirection();
    var muzzle = MuzzlePosition(gunPivot);
    var carriage = Color.FromArgb(255, 90, 95, 100);
    var tube = Color.FromArgb(255, 150, 155, 165);
    var plate = Color.FromArgb(255, 70, 75, 80);

    var right = Vector3.Normalize(Vector3.Cross(dir, Vector3.UnitY));
    if (right.LengthSquared() < 1e-6f)
      right = Vector3.UnitX;

    var plateHalf = 2.2f;
    ctx.DrawBolt(gunPivot - right * plateHalf, gunPivot + right * plateHalf, plate);
    ctx.DrawBolt(gunPivot - Vector3.UnitZ * plateHalf, gunPivot + Vector3.UnitZ * plateHalf, plate);
    ctx.DrawBolt(gunPivot, gunPivot + new Vector3(0f, 1.1f, 0f), carriage);
    ctx.DrawBolt(gunPivot + new Vector3(0f, 0.75f, 0f), muzzle, tube);
    ctx.DrawGlowSphereWires(muzzle, MuzzleRadius * 1.35f, Color.FromArgb(255, 180, 190, 200));

    if (showPivotGlow)
      ctx.DrawGlowSphereWires(gunPivot, 0.45f, Color.FromArgb(255, 100, 140, 120));
  }
}
