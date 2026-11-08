using CalypsoCad.Model;

namespace CalypsoCad.Generation.Transverse;

/// <summary>
/// Semantic definition of a ring-frame assembly before geometry emit.
/// One authoritative annular profile — not eight unrelated beams.
/// </summary>
internal sealed record RingFrameDefinition(
    AssemblyId Id,
    float Station,
    float WebDepth,
    float PlateThickness,
    RingFrameType Type,
    float SkinInset,
    bool BowAligned = false,
    bool HoldPerimeterOnly = false,
    string? FrameEntityPrefix = null)
{
    public CadAssemblyKind AssemblyKind => Type switch
    {
        RingFrameType.DeepWeb => CadAssemblyKind.DeepWebRingFrame,
        RingFrameType.Reinforced => CadAssemblyKind.ReinforcedRingFrame,
        _ => CadAssemblyKind.OrdinaryRingFrame,
    };

    public string EntityPrefix =>
        FrameEntityPrefix
        ?? (Id.Value.StartsWith("FRAME-", StringComparison.OrdinalIgnoreCase)
            ? "frame-" + Id.Value["FRAME-".Length..]
            : Id.Value);
}
