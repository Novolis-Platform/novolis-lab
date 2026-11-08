namespace CalypsoCad.Layout;

/// <summary>Required clear passageway volume — absence of structure, not a solid.</summary>
internal sealed record PassageEnvelope(
    string Id,
    float Y0,
    float Y1,
    float Z0,
    float Z1,
    float FloorUp,
    float ClearWidth,
    float ClearHeight);
