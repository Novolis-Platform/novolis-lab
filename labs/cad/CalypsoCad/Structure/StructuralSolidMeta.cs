namespace CalypsoCad.Structure;

/// <summary>Optional weld / assembly tags for a structure bar.</summary>
internal readonly record struct StructuralSolidMeta(
    string Role,
    string Assembly,
    float? StationZ = null,
    float? DeckUp = null,
    IReadOnlyList<string>? Joins = null);
