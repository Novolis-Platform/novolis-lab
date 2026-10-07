namespace AssetStudioLab;

/// <summary>Result of applying a command-mode script to the shared AST.</summary>
public sealed record VisualCommandResult(bool Success, VisualDocument Document, string Message);
