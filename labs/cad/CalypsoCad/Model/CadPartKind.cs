namespace CalypsoCad.Model;

/// <summary>Physical part kinds — independent of geometric primitive (box vs mesh).</summary>
internal enum CadPartKind
{
    ShellPanel,
    RingFrame,
    WebFrame,
    BulkheadPlate,
    HullStringer,
    LongitudinalGirder,
    DeckPanel,
    DeckBeam,
    StructuralBracket,
    Hatch,
    Partition,
    ThrustMember,
    Unknown,
}
