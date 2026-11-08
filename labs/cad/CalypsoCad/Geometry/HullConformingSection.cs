namespace CalypsoCad.Geometry;

/// <summary>
/// Geometric relationship: a transverse profile is derived from the hull inner section
/// at a station — not assembled from unrelated rectangular beams.
/// Foundational alongside lofted shell panels and swept longitudinals.
/// </summary>
internal sealed record HullConformingSection(
    float StationZStem,
    float SkinInset,
    IReadOnlyList<(float Y, float Up)> OuterProfile)
{
    public static HullConformingSection FromHull(HullGeometry hull, float zStem, float skinInset)
    {
        ArgumentNullException.ThrowIfNull(hull);
        var outer = hull.InnerOctagonRing(zStem, skinInset);
        return new HullConformingSection(zStem, skinInset, outer);
    }
}
