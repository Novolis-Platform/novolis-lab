namespace AssetStudioLab;

/// <summary>One named visual program: material, light, volume, effect, or nested block.</summary>
public sealed record VisualDefinition
{
    public required VisualDefinitionKind Kind { get; init; }
    public required string Name { get; init; }
    public IReadOnlyList<VisualParameter> Parameters { get; init; } = [];
    public IReadOnlyList<VisualProperty> Properties { get; init; } = [];
    public IReadOnlyList<VisualDefinition> Children { get; init; } = [];

    public VisualDefinition WithProperties(IReadOnlyList<VisualProperty> properties) =>
        this with { Properties = properties };

    public VisualDefinition WithChildren(IReadOnlyList<VisualDefinition> children) =>
        this with { Children = children };

    public VisualDefinition ReplaceProperty(string name, VisualExpr value, VisualAssignOp assign = VisualAssignOp.Set)
    {
        var list = Properties.ToList();
        var index = list.FindIndex(p =>
            string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)
            && p.Assign == VisualAssignOp.Set);
        var next = new VisualProperty(name, value, assign);
        if (assign == VisualAssignOp.Set && index >= 0)
            list[index] = next;
        else
            list.Add(next);
        return WithProperties(list);
    }
}
