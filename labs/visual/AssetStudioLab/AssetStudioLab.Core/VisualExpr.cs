namespace AssetStudioLab;

/// <summary>Canonical visual expression. GUI, source, commands, and C# all mutate this tree.</summary>
public sealed record VisualExpr
{
    public VisualExprKind Kind { get; init; }
    public VisualValue? Literal { get; init; }
    public string? Name { get; init; }
    public IReadOnlyList<VisualArg> Args { get; init; } = [];
    public VisualExpr? Left { get; init; }
    public VisualExpr? Right { get; init; }
    public VisualBinaryOp? BinaryOp { get; init; }

    public static VisualExpr Lit(VisualValue value) =>
        new() { Kind = VisualExprKind.Literal, Literal = value };

    public static VisualExpr Ident(string name) =>
        new() { Kind = VisualExprKind.Ident, Name = name };

    public static VisualExpr Context(string name) =>
        new() { Kind = VisualExprKind.Context, Name = name };

    public static VisualExpr Call(string name, params VisualArg[] args) =>
        new() { Kind = VisualExprKind.Call, Name = name, Args = args };

    public static VisualExpr Call(string name, IReadOnlyList<VisualArg> args) =>
        new() { Kind = VisualExprKind.Call, Name = name, Args = args };

    public static VisualExpr Binary(VisualBinaryOp op, VisualExpr left, VisualExpr right) =>
        new()
        {
            Kind = VisualExprKind.Binary,
            BinaryOp = op,
            Left = left,
            Right = right,
        };
}
