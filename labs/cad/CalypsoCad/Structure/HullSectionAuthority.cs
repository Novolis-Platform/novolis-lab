using CalypsoCad.Geometry;

namespace CalypsoCad.Structure;

/// <summary>
/// Shared outer/inner hull cross-section authority for transverse frames and bulkheads.
/// Delegates exclusively to <see cref="HullGeometry"/> — never to LockGenerator statics.
/// </summary>
internal static class HullSectionAuthority
{
    public static (float Y, float Up)[] OuterRing(float zStem) =>
        HullGeometry.RequireActive().OuterOctagonRing(zStem);

    public static (float Y, float Up)[] InnerRing(
        float zStem,
        float structuralInset = CalypsoShipbuildingFrames.DefaultSkinInset) =>
        HullGeometry.RequireActive().InnerOctagonRing(zStem, structuralInset);

    public static void Section(
        float zStem,
        out float halfBeam,
        out float halfHeight,
        out float midUp,
        out float chamferX,
        out float chamferY) =>
        HullGeometry.RequireActive().SectionAt(zStem, out halfBeam, out halfHeight, out midUp, out chamferX, out chamferY);

    public static bool HasStructuralSection(float zStem, float minHalf = 0.45f) =>
        HullGeometry.RequireActive().HasStructuralSection(zStem, minHalf);

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
