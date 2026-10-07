namespace AssetStudioLab;

/// <summary>A type or semantic diagnostic from visual compilation.</summary>
public sealed record CompileDiagnostic(
    CompileDiagnosticSeverity Severity,
    string Message,
    string? Path = null);
