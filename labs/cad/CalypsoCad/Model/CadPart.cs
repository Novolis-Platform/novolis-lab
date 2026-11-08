namespace CalypsoCad.Model;

/// <summary>Semantic physical part — engineering meaning independent of representation.</summary>
internal sealed record CadPart(
    PartId Id,
    AssemblyId Assembly,
    CadSystem System,
    CadPartKind Kind,
    PartFunction Functions,
    string DisplayName,
    GeometrySource? Geometry = null,
    float? StationZStem = null,
    float? DeckUp = null,
    string? FabPartId = null,
    string? LegacyRole = null,
    Guid? EntityId = null,
    bool Classified = true);
