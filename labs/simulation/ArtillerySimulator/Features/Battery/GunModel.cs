using System.Drawing;
using System.Numerics;
using Novolis.Physics.Ballistics;

namespace ArtillerySimulator;

/// <summary>
/// Live tube / charge state for one piece. Geometry and PE come from <see cref="IArtilleryPieceSpec"/>
/// so new vehicles expand via the catalog, not host forks.
/// </summary>
internal sealed class GunModel
{
    public GunModel(IArtilleryPieceSpec? spec = null)
    {
        Spec = spec ?? ArtilleryPieceCatalog.Default;
        ElevationDegrees = Math.Clamp(45f, Spec.MinElevationDegrees, Spec.MaxElevationDegrees);
        ChargeIndex = Math.Clamp(1, 0, Spec.Charges.Count - 1);
    }

    public IArtilleryPieceSpec Spec { get; }

    // Compatibility shims for call sites that still use GunModel.* statics.
    public static float BarrelLength => ArtilleryPieceCatalog.Default.BarrelLengthMeters;
    public static float MuzzleRadius => ArtilleryPieceCatalog.Default.MuzzleRadiusMeters;
    public static float MinElevationDegrees => ArtilleryPieceCatalog.Default.MinElevationDegrees;
    public static float MaxElevationDegrees => ArtilleryPieceCatalog.Default.MaxElevationDegrees;
    public static float MaxTraverseDegrees => ArtilleryPieceCatalog.Default.MaxTraverseDegrees;
    public static bool ContinuousTraverse => ArtilleryPieceCatalog.Default.ContinuousTraverse;
    public static ProjectileProfile Educational155Profile => ArtilleryPieceCatalog.Default.Projectile;

    public float ElevationDegrees { get; private set; }

    /// <summary>World bearing. Azimuth 0 fires east (+X).</summary>
    public float AzimuthDegrees { get; private set; }

    /// <summary>Hull / preferred-sector center (opening lay).</summary>
    public float CenterAzimuthDegrees { get; private set; }

    public int ChargeIndex { get; private set; }

    public float ElevationMils => NatoMils.DegreesToMils(ElevationDegrees);

    public float AzimuthMils => NatoMils.DegreesToMils(AzimuthDegrees);

    public float DeflectionDegrees => AzimuthDegrees - CenterAzimuthDegrees;

    public float DeflectionMils => NatoMils.DegreesToMils(DeflectionDegrees);

    public bool AtTraverseStop =>
        !Spec.ContinuousTraverse && MathF.Abs(Wrap180(DeflectionDegrees)) >= Spec.MaxTraverseDegrees - 0.05f;

    public float MuzzleSpeedMps => Spec.ChargeAt(ChargeIndex).MuzzleSpeedMps;

    public string ChargeLabel => Spec.ChargeAt(ChargeIndex).Label;

    public void NudgeElevation(float delta) =>
        ElevationDegrees = Math.Clamp(ElevationDegrees + delta, Spec.MinElevationDegrees, Spec.MaxElevationDegrees);

    public void SetElevation(float degrees) =>
        ElevationDegrees = Math.Clamp(degrees, Spec.MinElevationDegrees, Spec.MaxElevationDegrees);

    public void LayCenter(float worldAzimuthDegrees)
    {
        CenterAzimuthDegrees = worldAzimuthDegrees;
        AzimuthDegrees = worldAzimuthDegrees;
    }

    public void NudgeAzimuth(float delta)
    {
        var deflection = Math.Clamp(Wrap180(DeflectionDegrees + delta), -Spec.MaxTraverseDegrees, Spec.MaxTraverseDegrees);
        AzimuthDegrees = CenterAzimuthDegrees + deflection;
    }

    public void SetCharge(int index) =>
        ChargeIndex = Math.Clamp(index, 0, Spec.Charges.Count - 1);

    public Vector3 BarrelDirection() => DirectionOf(ElevationDegrees, AzimuthDegrees);

    public static Vector3 DirectionOf(float elevationDegrees, float azimuthDegrees)
    {
        var elev = elevationDegrees * (MathF.PI / 180f);
        var az = azimuthDegrees * (MathF.PI / 180f);
        var cosE = MathF.Cos(elev);
        return Vector3.Normalize(new Vector3(cosE * MathF.Cos(az), MathF.Sin(elev), cosE * MathF.Sin(az)));
    }

    public Vector3 MuzzlePosition(Vector3 gunPivot) =>
        gunPivot + BarrelDirection() * Spec.BarrelLengthMeters;

    public ProjectileState CreateProjectileState(Vector3 muzzle, Vector3 barrelDir, float? muzzleSpeedMps = null)
    {
        var speed = muzzleSpeedMps ?? MuzzleSpeedMps;
        return new(muzzle, barrelDir * speed, Spec.Projectile.MassKg, timeSeconds: 0);
    }

    public ProjectileState PredictState(Vector3 gunPivot, float elevationDegrees, float azimuthDegrees, float muzzleSpeedMps)
    {
        var direction = DirectionOf(elevationDegrees, azimuthDegrees);
        return new ProjectileState(
            gunPivot + direction * Spec.BarrelLengthMeters,
            direction * muzzleSpeedMps,
            Spec.Projectile.MassKg,
            timeSeconds: 0);
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

        DrawBox(wires, deck, hullForward, hullRight, 3.6f, 1.55f, 1.05f, hullPaint);
        wires.Line(deck - hullRight * 1.7f - hullForward * 3.2f, deck - hullRight * 1.7f + hullForward * 3.2f, hullPaint);
        wires.Line(deck + hullRight * 1.7f - hullForward * 3.2f, deck + hullRight * 1.7f + hullForward * 3.2f, hullPaint);

        DrawBox(wires, turret, hullForward, hullRight, 1.9f, 1.35f, 0.75f, turretPaint);
        DrawBox(wires, turret - hullForward * 1.5f, hullForward, hullRight, 1.1f, 1.15f, 0.55f, turretPaint);
        wires.AxisRing(turret, Vector3.UnitY, 1.05f, turretPaint);

        DrawBundle(wires, trunnion, muzzle, dir, 0.095f, tube);
        wires.AxisRing(muzzle, dir, 0.17f, copper);
        wires.AxisRing(trunnion, dir, 0.14f, turretPaint);
        wires.Line(trunnion + tubeRight * 0.55f, trunnion - tubeRight * 0.55f, turretPaint);

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
