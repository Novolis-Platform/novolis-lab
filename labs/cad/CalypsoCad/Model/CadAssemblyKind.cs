namespace CalypsoCad.Model;

/// <summary>Engineering assembly kinds under transverse / longitudinal / deck systems.</summary>
internal enum CadAssemblyKind
{
    Other,

    // —— Transverse / RingFrames ——
    OrdinaryRingFrame,
    DeepWebRingFrame,
    ReinforcedRingFrame,

    // —— Transverse / Bulkheads ——
    SolidBulkhead,
    PressureBulkhead,
    RingBulkhead,

    // —— Longitudinal ——
    LongitudinalRun,

    /// <summary>One continuous outer-hull plating knuckle stringer (vertex 0…7).</summary>
    HullKnuckleStringer,

    // —— Deck ——
    DeckBay,
}
