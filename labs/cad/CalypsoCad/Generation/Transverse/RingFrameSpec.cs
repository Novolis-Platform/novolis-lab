using CalypsoCad.Model;

namespace CalypsoCad.Generation.Transverse;

internal sealed record RingFrameSpec(
    AssemblyId Assembly,
    string FrameEntityPrefix,
    float ZStem,
    float Web,
    float HalfDepthZ,
    bool BowAligned,
    bool HoldPerimeterOnly,
    IReadOnlyList<float> DeckUps);
