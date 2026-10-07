namespace AssetStudioLab;

/// <summary>One property, compound assignment, or layer on a definition.</summary>
public sealed record VisualProperty(
    string Name,
    VisualExpr Value,
    VisualAssignOp Assign = VisualAssignOp.Set);
