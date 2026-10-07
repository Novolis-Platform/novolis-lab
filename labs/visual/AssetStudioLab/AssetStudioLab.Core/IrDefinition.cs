namespace AssetStudioLab;

/// <summary>One compiled definition in the visual IR.</summary>
public sealed record IrDefinition(
    string Name,
    VisualDefinitionKind Kind,
    IReadOnlyList<IrNode> Nodes,
    IReadOnlyDictionary<string, string> Outputs);
