using System.Numerics;
using Novolis.Math.Geometry;

namespace CharacterLab.Demo;

/// <summary>
/// Named grip / barrel points in rifle local space. Hands Soft-IK / FullBodyIk lock to these after the weapon pose is placed.
/// </summary>
internal readonly record struct WeaponHoldPoint(string Name, Vector3 LocalPosition);

/// <summary>Hold-point set for a long gun (primary / secondary grip + butt / muzzle).</summary>
internal sealed class WeaponHoldSet
{
    public WeaponHoldSet(
        WeaponHoldPoint primaryGrip,
        WeaponHoldPoint secondaryGrip,
        WeaponHoldPoint butt,
        WeaponHoldPoint muzzle)
    {
        PrimaryGrip = primaryGrip;
        SecondaryGrip = secondaryGrip;
        Butt = butt;
        Muzzle = muzzle;
    }

    public WeaponHoldPoint PrimaryGrip { get; }
    public WeaponHoldPoint SecondaryGrip { get; }
    public WeaponHoldPoint Butt { get; }
    public WeaponHoldPoint Muzzle { get; }

    public IEnumerable<WeaponHoldPoint> All
    {
        get
        {
            yield return PrimaryGrip;
            yield return SecondaryGrip;
            yield return Butt;
            yield return Muzzle;
        }
    }

    /// <summary>
    /// Builds holds for a rifle whose longest axis was remapped to +Z and centered at origin.
    /// </summary>
    public static WeaponHoldSet ForCenteredLongGun(float lengthMeters)
    {
        var half = lengthMeters * 0.5f;
        return new WeaponHoldSet(
            new WeaponHoldPoint("primary", new Vector3(0.02f, -0.02f, -half * 0.15f)),
            new WeaponHoldPoint("secondary", new Vector3(-0.01f, 0.01f, half * 0.25f)),
            new WeaponHoldPoint("butt", new Vector3(0f, 0f, -half * 0.92f)),
            new WeaponHoldPoint("muzzle", new Vector3(0f, 0f, half * 0.95f)));
    }

    /// <summary>Mesh-aware overload (length taken from caller; mesh unused — kept for call-site clarity).</summary>
    public static WeaponHoldSet ForCenteredLongGun(TriangleMesh? mesh, float lengthMeters) =>
        ForCenteredLongGun(lengthMeters);

    public Vector3 World(WeaponHoldPoint point, Matrix4x4 weaponWorld) =>
        Vector3.Transform(point.LocalPosition, weaponWorld);
}
