namespace CalypsoCad.Constraints;

/// <summary>Location that requires a transverse assembly (frame or heavy bulkhead).</summary>
internal sealed record StructuralStation(
    string Id,
    float ZStem,
    StructuralStationKind Kind,
    bool PressureTight = false,
    string? Note = null);
