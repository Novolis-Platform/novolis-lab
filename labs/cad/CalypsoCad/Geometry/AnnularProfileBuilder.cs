using CalypsoCad.Structure;

namespace CalypsoCad.Geometry;

/// <summary>
/// Builds <see cref="AnnularProfile"/> from a hull-conforming outer section + inward web offset.
/// Geometry operation only — does not invent engineering purpose (frame vs ring bulkhead).
/// </summary>
internal static class AnnularProfileBuilder
{
    /// <summary>
    /// OuterProfile = Hull.InnerSection(station); InnerProfile = OffsetInward(Outer, webDepth).
    /// </summary>
    public static AnnularProfile? FromHullConforming(
        HullGeometry hull,
        float zStem,
        float webDepth,
        float skinInset = CalypsoShipbuildingFrames.DefaultSkinInset)
    {
        ArgumentNullException.ThrowIfNull(hull);
        var conforming = HullConformingSection.FromHull(hull, zStem, skinInset);
        return FromOuter(conforming.OuterProfile, webDepth);
    }

    public static AnnularProfile? FromOuter(
        IReadOnlyList<(float Y, float Up)> outer,
        float webDepth)
    {
        if (outer.Count < 3)
            return null;
        var webWidth = MathF.Max(0.08f, webDepth);
        var pts = outer as (float Y, float Up)[] ?? outer.ToArray();
        var inner = PolygonInset2D.Inset(pts, webWidth);
        if (inner.Length != pts.Length)
            return null;
        return new AnnularProfile(pts, inner);
    }
}
