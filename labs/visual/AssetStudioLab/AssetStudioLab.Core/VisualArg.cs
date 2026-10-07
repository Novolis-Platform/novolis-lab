namespace AssetStudioLab;

/// <summary>Named or positional argument.</summary>
public sealed record VisualArg(string? Name, VisualExpr Value)
{
    public static VisualArg Positional(VisualExpr value) => new(null, value);

    public static VisualArg Named(string name, VisualExpr value) => new(name, value);
}
