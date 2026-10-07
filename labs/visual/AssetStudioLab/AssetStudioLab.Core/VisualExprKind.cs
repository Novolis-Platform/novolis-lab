namespace AssetStudioLab;

/// <summary>Node shape inside <see cref="VisualExpr"/>.</summary>
public enum VisualExprKind
{
    Literal,
    Ident,
    Context,
    Call,
    Binary,
}
