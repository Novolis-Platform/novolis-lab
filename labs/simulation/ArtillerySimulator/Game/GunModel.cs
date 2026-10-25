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
    var carriage = Color.FromArgb(255, 78, 86, 64);
    var tube = Color.FromArgb(255, 186, 176, 154);
    var copper = Color.FromArgb(255, 210, 132, 72);
    var right = Lateral(dir);
    var horizontal = Vector3.Normalize(new Vector3(dir.X, 0f, dir.Z));
    var trunnion = gunPivot + new Vector3(0f, 0.95f, 0f);
    var axle = gunPivot - new Vector3(0f, 0.85f, 0f);

    DrawBundle(ctx, trunnion, muzzle, dir, 0.09f, tube);
    DrawRing(ctx, muzzle, dir, 0.16f, copper);
    DrawRing(ctx, trunnion, dir, 0.12f, carriage);

    var trailEnd = gunPivot - horizontal * 3.6f - new Vector3(0f, 0.55f, 0f);
    DrawBundle(ctx, axle + right * 0.35f, trailEnd + right * 0.7f, horizontal, 0.05f, carriage);
    DrawBundle(ctx, axle - right * 0.35f, trailEnd - right * 0.7f, horizontal, 0.05f, carriage);
    DrawRing(ctx, axle + right * 1.15f, right, 0.72f, carriage);
    DrawRing(ctx, axle - right * 1.15f, right, 0.72f, carriage);

    var shield = trunnion + horizontal * 0.45f;
    ctx.DrawBolt(shield + right * 0.85f + Vector3.UnitY * 0.55f, shield - right * 0.85f + Vector3.UnitY * 0.55f, copper);
    ctx.DrawBolt(shield + right * 0.85f - Vector3.UnitY * 0.15f, shield - right * 0.85f - Vector3.UnitY * 0.15f, carriage);
    ctx.DrawBolt(shield + right * 0.85f + Vector3.UnitY * 0.55f, shield + right * 0.85f - Vector3.UnitY * 0.15f, carriage);
    ctx.DrawBolt(shield - right * 0.85f + Vector3.UnitY * 0.55f, shield - right * 0.85f - Vector3.UnitY * 0.15f, carriage);

    if (showPivotGlow)
      ctx.DrawGlowSphere(muzzle, 1.1f, copper);
  }

  private static Vector3 Lateral(Vector3 dir)
  {
    var right = Vector3.Cross(dir, Vector3.UnitY);
    return right.LengthSquared() < 1e-6f ? Vector3.UnitZ : Vector3.Normalize(right);
  }

  private static void DrawBundle(RayGameContext ctx, Vector3 from, Vector3 to, Vector3 axis, float radius, Color color)
  {
    var side = Lateral(axis);
    var up = Vector3.Normalize(Vector3.Cross(axis, side));
    ctx.DrawBolt(from, to, color);
    ctx.DrawBolt(from + side * radius, to + side * radius, color);
    ctx.DrawBolt(from - side * radius, to - side * radius, color);
    ctx.DrawBolt(from + up * radius, to + up * radius, color);
  }

  private static void DrawRing(RayGameContext ctx, Vector3 center, Vector3 axis, float radius, Color color)
  {
    var side = Lateral(axis);
    var up = Vector3.Normalize(Vector3.Cross(axis, side));
    const int segments = 12;
    var previous = center + side * radius;
    for (var i = 1; i <= segments; i++)
    {
      var angle = i / (float)segments * MathF.Tau;
      var next = center + side * (MathF.Cos(angle) * radius) + up * (MathF.Sin(angle) * radius);
      ctx.DrawBolt(previous, next, color);
      previous = next;
    }
  }
}
