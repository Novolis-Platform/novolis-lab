namespace AssetStudioLab;

/// <summary>One operand in the normalized visual IR.</summary>
public sealed record IrOperand(string? NodeId, VisualValue? Literal, string? Name);
