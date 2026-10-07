namespace AssetStudioLab;

/// <summary>Substitutes definition parameters so one material can instance many ships.</summary>
public static class VisualInstantiator
{
    public static VisualDefinition Instantiate(VisualDefinition definition, IReadOnlyDictionary<string, VisualExpr> arguments)
    {
        var map = definition.Parameters.ToDictionary(
            p => p.Name,
            p => arguments.TryGetValue(p.Name, out var value) ? value : p.Default,
            StringComparer.OrdinalIgnoreCase);
        return definition with
        {
            Parameters = [],
            Properties = definition.Properties.Select(p => p with { Value = Substitute(p.Value, map) }).ToArray(),
            Children = definition.Children.Select(c => Instantiate(c, arguments)).ToArray(),
        };
    }

    private static VisualExpr Substitute(VisualExpr expr, IReadOnlyDictionary<string, VisualExpr> map)
    {
        if (expr.Kind == VisualExprKind.Ident && expr.Name is not null && map.TryGetValue(expr.Name, out var replacement))
            return replacement;
        if (expr.Kind == VisualExprKind.Call)
        {
            return expr with
            {
                Args = expr.Args.Select(a => a with { Value = Substitute(a.Value, map) }).ToArray(),
            };
        }

        if (expr.Kind == VisualExprKind.Binary)
        {
            return expr with
            {
                Left = Substitute(expr.Left!, map),
                Right = Substitute(expr.Right!, map),
            };
        }

        return expr;
    }
}
