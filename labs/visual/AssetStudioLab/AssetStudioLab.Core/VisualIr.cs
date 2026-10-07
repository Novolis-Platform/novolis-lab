namespace AssetStudioLab;

/// <summary>Normalized visual IR after name resolution, typing, folding, and DCE.</summary>
public sealed record VisualIr(
    IReadOnlyList<IrDefinition> Definitions,
    IReadOnlyList<CompileDiagnostic> Diagnostics)
{
    public bool Success => Diagnostics.All(d => d.Severity != CompileDiagnosticSeverity.Error);
}
