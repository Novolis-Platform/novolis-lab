namespace CalypsoCad.Structure;

/// <summary>One station in the station-driven framing schedule.</summary>
internal sealed record ScheduledStation(
    string Id,
    float ZStem,
    StructuralStationRole Role,
    string? BulkheadId = null);
