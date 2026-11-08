namespace CalypsoCad.Geometry;

/// <summary>
/// Authoritative 2D definition of an annular (possibly polygonal) plate:
/// outer boundary, inner cutout, remaining steel = Outer − Inner.
/// Annular does not imply circular — an octagonal ring is a polygonal annulus.
/// </summary>
internal sealed record AnnularProfile(
    IReadOnlyList<(float Y, float Up)> Outer,
    IReadOnlyList<(float Y, float Up)> Inner)
{
    public int VertexCount => Outer.Count;

    public bool IsValid =>
        Outer.Count >= 3
        && Inner.Count == Outer.Count;
}
