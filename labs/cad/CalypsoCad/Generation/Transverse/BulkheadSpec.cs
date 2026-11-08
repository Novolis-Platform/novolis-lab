using CalypsoCad.Model;

namespace CalypsoCad.Generation.Transverse;

internal sealed record BulkheadSpec(
    AssemblyId Assembly,
    float ZStem,
    float Thickness,
    float HalfDepthZ,
    bool PressureTight,
    bool BowAligned,
    IReadOnlyList<(float Y0, float Y1)> AthwartGaps,
    IReadOnlyList<float> DeckUps,
    IReadOnlyList<Structure.FrameWebPlateBuilder.WebPlatePortal>? HatchPortals = null);
