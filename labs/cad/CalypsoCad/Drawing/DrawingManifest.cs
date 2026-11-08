namespace CalypsoCad.Drawing;

internal sealed class DrawingManifest
{
    public string SubjectId { get; init; } = "";
    public string? DrawingNumber { get; init; }
    public IReadOnlyList<string> ViewIds { get; init; } = [];
    public IReadOnlyList<Guid> GeometrySourceIds { get; init; } = [];
    public IReadOnlyList<DrawingMeasurement> Measurements { get; init; } = [];
    public IReadOnlyList<string> Diagnostics { get; init; } = [];
    public IReadOnlyList<PartRegisterLine> PartRegister { get; init; } = [];
    public bool Ok { get; init; }
}
