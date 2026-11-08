namespace CalypsoCad.Drawing;

internal sealed record DrawingViewDefinition(
    string Id,
    DrawingViewKind Kind,
    StandardViewOrientation Orientation,
    DrawingPlane? SectionPlane = null,
    DrawingBounds3? DepthRange = null,
    string? Title = null);
