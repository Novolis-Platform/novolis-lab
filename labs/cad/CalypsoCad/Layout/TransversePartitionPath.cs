namespace CalypsoCad.Layout;

/// <summary>Transverse partition plane (constant Z stem) spanning a Y/up band.</summary>
internal sealed record TransversePartitionPath(
    string Id,
    float ZStem,
    float Y0,
    float Y1,
    float Up0,
    float Up1,
    string OwnerCompartmentId,
    bool AlignsWithHeavyBulkhead);
