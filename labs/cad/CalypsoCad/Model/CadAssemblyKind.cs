namespace CalypsoCad.Model;

/// <summary>Engineering and organizational assembly kinds for the ownership tree.</summary>
internal enum CadAssemblyKind
{
    Other,

    // —— Organizational (Explorer tree, not manufactured) ——
    ShipRoot,
    ReferenceGeometry,
    PhysicalStructure,
    SystemGroup,
    DiagnosticsGroup,

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

    // —— Closures / Interior / Cargo ——
    AirlockAssembly,
    HatchAssembly,
    PartitionGroup,
    OutfitGroup,
    CargoStack,
}
