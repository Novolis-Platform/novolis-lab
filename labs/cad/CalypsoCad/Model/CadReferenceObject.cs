namespace CalypsoCad.Model;

/// <summary>Typed nonphysical design object (loft, station, envelope, clearance).</summary>
internal sealed record CadReferenceObject(
    string Id,
    CadReferenceKind Kind,
    string DisplayName,
    AssemblyId? ParentAssemblyId = null,
    Guid? EntityId = null,
    float? StationZStem = null,
    float? DeckUp = null,
    string? CompartmentId = null);
