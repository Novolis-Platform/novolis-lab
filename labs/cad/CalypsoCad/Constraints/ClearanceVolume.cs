namespace CalypsoCad.Constraints;

/// <summary>Reserved space that constrains physical parts — not a part itself.</summary>
internal sealed record ClearanceVolume(
    string Id,
    ClearanceKind Kind,
    float Z0Stem,
    float Z1Stem,
    float? Y0 = null,
    float? Y1 = null,
    float? Up0 = null,
    float? Up1 = null,
    string? Note = null)
{
    public bool ContainsStation(float zStem) =>
        zStem >= Z0Stem - 1e-3f && zStem <= Z1Stem + 1e-3f;
}
