using System.Numerics;

namespace KatoriLab.Demo;

/// <summary>Named grip / tip points in ken local space (+Z along blade toward kissaki, origin at weapon center).</summary>
internal readonly record struct KenHoldPoint(string Name, Vector3 LocalPosition);

/// <summary>
/// Classical two-hand ken hold: left near kashira, right just behind tsuba, tsuba, tip.
/// </summary>
internal sealed class KenHoldSet
{
    public KenHoldSet(
        KenHoldPoint primaryGrip,
        KenHoldPoint secondaryGrip,
        KenHoldPoint kashira,
        KenHoldPoint tsuba,
        KenHoldPoint kissaki)
    {
        PrimaryGrip = primaryGrip;
        SecondaryGrip = secondaryGrip;
        Kashira = kashira;
        Tsuba = tsuba;
        Kissaki = kissaki;
    }

    /// <summary>Right hand — just behind the tsuba (closer to the blade).</summary>
    public KenHoldPoint PrimaryGrip { get; }

    /// <summary>Left hand — near the kashira (farther from the blade).</summary>
    public KenHoldPoint SecondaryGrip { get; }

    public KenHoldPoint Kashira { get; }
    public KenHoldPoint Tsuba { get; }
    public KenHoldPoint Kissaki { get; }

    /// <summary>Midpoint between the two hands on the tsuka (for placing the weapon).</summary>
    public Vector3 GripMidLocal => (PrimaryGrip.LocalPosition + SecondaryGrip.LocalPosition) * 0.5f;

    public float GripSpanMeters =>
        Vector3.Distance(PrimaryGrip.LocalPosition, SecondaryGrip.LocalPosition);

    public IEnumerable<KenHoldPoint> All
    {
        get
        {
            yield return PrimaryGrip;
            yield return SecondaryGrip;
            yield return Kashira;
            yield return Tsuba;
            yield return Kissaki;
        }
    }

    /// <summary>
    /// Bokken/ken along +Z, centered. Tsuka ≈ 28 cm; proper hand spacing (~22 cm).
    /// Slight lateral offsets so left/right don't occupy the same ray.
    /// </summary>
    public static KenHoldSet ForCenteredBokken(float lengthMeters)
    {
        var half = lengthMeters * 0.5f;
        const float tsukaLen = 0.28f;
        var tsubaZ = -half + tsukaLen;
        var leftZ = -half + 0.035f;      // left near kashira
        var rightZ = tsubaZ - 0.028f;    // right just behind tsuba (~22 cm span)
        return new KenHoldSet(
            new KenHoldPoint("primary", new Vector3(0.022f, -0.014f, rightZ)),
            new KenHoldPoint("secondary", new Vector3(-0.022f, 0.014f, leftZ)),
            new KenHoldPoint("kashira", new Vector3(0f, 0f, -half * 0.98f)),
            new KenHoldPoint("tsuba", new Vector3(0f, 0f, tsubaZ)),
            new KenHoldPoint("kissaki", new Vector3(0f, 0f, half * 0.98f)));
    }

    public Vector3 World(KenHoldPoint point, Matrix4x4 weaponWorld) =>
        Vector3.Transform(point.LocalPosition, weaponWorld);
}
