namespace CalypsoCad.Layout;

/// <summary>One lock compartment footprint used by both structure and later wall emit.</summary>
internal sealed record CompartmentBound(
    string Id,
    int Deck,
    float Z0,
    float Z1,
    float Y0,
    float Y1,
    float Up0,
    float Up1,
    bool IsPassageway,
    bool IsCargoClearance,
    bool IsMachineryAtrium);
