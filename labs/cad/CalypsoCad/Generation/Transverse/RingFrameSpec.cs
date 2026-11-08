using CalypsoCad.Model;
using CalypsoCad.Structure;

namespace CalypsoCad.Generation.Transverse;

/// <summary>Legacy emit bag — prefer <see cref="RingFrameDefinition"/> for new call sites.</summary>
internal sealed record RingFrameSpec(
    AssemblyId Assembly,
    string FrameEntityPrefix,
    float ZStem,
    float Web,
    float HalfDepthZ,
    bool BowAligned,
    bool HoldPerimeterOnly,
    IReadOnlyList<float> DeckUps)
{
    public RingFrameDefinition ToDefinition() =>
        new(
            Assembly,
            ZStem,
            WebDepth: Web,
            PlateThickness: HalfDepthZ * 2f,
            RingFrameGenerator.InferType(Web, HoldPerimeterOnly),
            SkinInset: CalypsoShipbuildingFrames.DefaultSkinInset,
            BowAligned,
            HoldPerimeterOnly,
            FrameEntityPrefix);
}
