namespace CalypsoCad.Drawing;

/// <summary>Fully declarative drawing request — reproducible independent of viewport visibility.</summary>
internal sealed record DrawingRequest(
    string SubjectId,
    IReadOnlyList<DrawingViewDefinition> Views,
    DrawingReferenceSet References,
    DimensionPolicy Dimensions,
    string? DrawingNumber = null,
    string? Title = null,
    string? Label = null);
