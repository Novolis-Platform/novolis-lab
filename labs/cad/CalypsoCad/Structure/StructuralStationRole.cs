namespace CalypsoCad.Structure;

/// <summary>Role of a station in the coherent transverse framing schedule.</summary>
internal enum StructuralStationRole
{
    /// <summary>Hull-conforming polygonal annulus (ordinary / deep-web ring frame).</summary>
    OrdinaryRingFrame,

    /// <summary>Heavy bulkhead — replaces the ring frame at this station.</summary>
    HeavyBulkhead,
}
