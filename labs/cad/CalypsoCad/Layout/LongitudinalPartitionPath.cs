namespace CalypsoCad.Layout;

/// <summary>Longitudinal partition plane (constant Y) spanning a Z/up band.</summary>
internal sealed record LongitudinalPartitionPath(
    string Id,
    float Y,
    float Z0,
    float Z1,
    float Up0,
    float Up1,
    string OwnerCompartmentId,
    bool IsPassageBoundary);
