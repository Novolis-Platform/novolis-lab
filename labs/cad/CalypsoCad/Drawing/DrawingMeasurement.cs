namespace CalypsoCad.Drawing;

internal sealed record DrawingMeasurement(
    string Id,
    string Label,
    float ValueMeters,
    bool IsTrue3D,
    string? SourceNote = null);
