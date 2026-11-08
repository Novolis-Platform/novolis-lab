namespace CalypsoCad.Drawing;

internal sealed record DrawingReferenceSet(
    DrawingReferenceFlags Flags = DrawingReferenceFlags.DefaultContext,
    IReadOnlyList<float>? ExtraStationsZStem = null);
