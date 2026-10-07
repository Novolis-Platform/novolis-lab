namespace AssetStudioLab;

/// <summary>Canonical visual document: one AST shared by every editor.</summary>
public sealed record VisualDocument(IReadOnlyList<VisualDefinition> Definitions)
{
    public static VisualDocument Empty { get; } = new([]);

    public VisualDefinition? Find(string name) =>
        Definitions.FirstOrDefault(d => string.Equals(d.Name, name, StringComparison.OrdinalIgnoreCase));

    public VisualDocument Replace(VisualDefinition definition)
    {
        var list = Definitions.ToList();
        var index = list.FindIndex(d => string.Equals(d.Name, definition.Name, StringComparison.OrdinalIgnoreCase));
        if (index >= 0)
            list[index] = definition;
        else
            list.Add(definition);
        return new VisualDocument(list);
    }

    public VisualDocument Add(VisualDefinition definition) =>
        new(Definitions.Concat([definition]).ToArray());
}
