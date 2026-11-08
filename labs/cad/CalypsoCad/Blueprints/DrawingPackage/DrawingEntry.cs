namespace CalypsoCad.Blueprints.DrawingPackage;

/// <summary>One numbered, labeled drawing in the package register.</summary>
internal sealed record DrawingEntry(
    DrawingCategoryId Category,
    int Sequence,
    string DrawingNumber,
    string Title,
    string Label,
    string RelativePath,
    string? DetailSourceFile,
    string? SubjectId,
    VolumeCirculationKind VolumeKind,
    IReadOnlyList<string> Tags);
