namespace CalypsoCad.Model;

/// <summary>Projection node for the Explorer tree — derived from the registry, not authoritative.</summary>
internal sealed class ShipExplorerNode
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required ShipExplorerNodeKind Kind { get; init; }
    public AssemblyId? AssemblyId { get; init; }
    public PartId? PartId { get; init; }
    public string? ReferenceId { get; init; }
    public List<ShipExplorerNode> Children { get; } = [];
}
