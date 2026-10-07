namespace AssetStudioLab;

/// <summary>Normalized mixer node. Renderers never see <c>EdgeWear</c> — they see this.</summary>
public sealed record IrNode(
    string Id,
    string Op,
    VisualKind Type,
    IReadOnlyList<IrOperand> Operands,
    IReadOnlyDictionary<string, double> Constants);
