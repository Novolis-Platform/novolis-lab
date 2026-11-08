using Novolis.Math.Geometry;

namespace CalypsoCad.Structure;

/// <summary>
/// Shared outer/inner hull cross-section authority for transverse frames and bulkheads.
/// Both generators must consume these profiles — never invent independent approximations.
/// </summary>
internal static class HullSectionAuthority
{
    /// <summary>Outer (OML) octagon ring at stem station — plate outer face from loft metrics.</summary>
    public static (float Y, float Up)[] OuterRing(float zStem) =>
        CalypsoShipbuildingFrames.OuterOctagonRing(zStem);

    /// <summary>
    /// Inner structural ring at stem station via geometric polygon offset of <see cref="OuterRing"/>.
    /// Pass the same inset the frame/BH emitter uses so generators share one profile path.
    /// </summary>
    public static (float Y, float Up)[] InnerRing(
        float zStem,
        float structuralInset = CalypsoShipbuildingFrames.DefaultSkinInset) =>
        CalypsoShipbuildingFrames.InnerOctagonRing(zStem, structuralInset);

    /// <summary>Canonical section metrics at a stem station (interpolated loft).</summary>
    public static void Section(
        float zStem,
        out float halfBeam,
        out float halfHeight,
        out float midUp,
        out float chamferX,
        out float chamferY) =>
        CalypsoLockGenerator.SectionAtPublic(zStem, out halfBeam, out halfHeight, out midUp, out chamferX, out chamferY);

    /// <summary>True when the station has a usable structural cross-section.</summary>
    public static bool HasStructuralSection(float zStem, float minHalf = 0.45f)
    {
        Section(zStem, out var hb, out var hh, out _, out _, out _);
        return hb >= minHalf && hh >= minHalf;
    }

    /// <summary>
    /// Max absolute half-beam deviation between a candidate ring and the authority IML ring
    /// (vertex-wise, order-matched).
    /// </summary>
    public static float MaxRingDeviation(
        (float Y, float Up)[] candidate,
        float zStem,
        float structuralInset = CalypsoShipbuildingFrames.DefaultSkinInset)
    {
        var auth = InnerRing(zStem, structuralInset);
        if (candidate.Length == 0 || auth.Length == 0)
            return float.PositiveInfinity;
        var n = Math.Min(candidate.Length, auth.Length);
        var max = 0f;
        for (var i = 0; i < n; i++)
        {
            var dy = candidate[i].Y - auth[i].Y;
            var du = candidate[i].Up - auth[i].Up;
            max = MathF.Max(max, MathF.Sqrt(dy * dy + du * du));
        }

        return max;
    }
}
