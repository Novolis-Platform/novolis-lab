namespace AssetStudioLab;

/// <summary>Declared input on a definition function, e.g. <c>wear = 0.08</c>.</summary>
public sealed record VisualParameter(string Name, VisualKind Kind, VisualExpr Default);
