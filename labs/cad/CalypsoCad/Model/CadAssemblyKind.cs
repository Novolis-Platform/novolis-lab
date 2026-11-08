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

    // —— Other systems ——
    LongitudinalRun,
    DeckBay,
}
