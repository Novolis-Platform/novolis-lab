using System.Globalization;
using System.Text;

namespace AssetStudioLab;

/// <summary>Emits fluent C# from a visual document — a real compiler target, not a novelty export.</summary>
public static class VisualCsharpEmitter
{
    public static string Emit(VisualDocument document)
    {
        var sb = new StringBuilder();
        sb.AppendLine("using AssetStudioLab;");
        sb.AppendLine();
        sb.AppendLine("public static class GeneratedVisualAssets");
        sb.AppendLine("{");
        foreach (var definition in document.Definitions)
        {
            if (definition.Kind != VisualDefinitionKind.Material)
                continue;
            sb.Append("    public static readonly MaterialDefinition ").Append(definition.Name).AppendLine(" =");
            sb.Append("        Material.Gray(\"");
            var color = definition.Properties.FirstOrDefault(p => p.Name.Equals("color", StringComparison.OrdinalIgnoreCase));
            sb.Append(color is null ? "#747a7d" : VisualLanguageFormatter.FormatExpr(color.Value));
            sb.AppendLine("\")");
            var roughness = definition.Properties.FirstOrDefault(p =>
                p.Name.Equals("roughness", StringComparison.OrdinalIgnoreCase) && p.Assign == VisualAssignOp.Set);
            if (roughness is not null)
            {
                sb.Append("            .WithRoughness(");
                sb.Append(FormatFloat(roughness.Value));
                sb.AppendLine(")");
            }

            foreach (var property in definition.Properties.Where(p =>
                         p.Name.Equals("roughness", StringComparison.OrdinalIgnoreCase) && p.Assign == VisualAssignOp.Multiply))
            {
                sb.AppendLine("            .ModifyRoughness(");
                sb.Append("                ");
                sb.Append(FormatNoise(property.Value));
                sb.AppendLine(")");
            }

            foreach (var layer in definition.Properties.Where(p => p.Assign == VisualAssignOp.Layer))
            {
                sb.Append("            .With(");
                sb.Append(Title(layer.Name));
                sb.Append(".Amount(");
                var amount = layer.Value.Args.FirstOrDefault()?.Value;
                sb.Append(amount is null ? "0" : FormatFloat(amount));
                sb.AppendLine("))");
            }

            sb.Length -= Environment.NewLine.Length;
            sb.AppendLine(";");
            sb.AppendLine();
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string FormatFloat(VisualExpr expr)
    {
        if (expr.Kind == VisualExprKind.Literal && expr.Literal is not null)
            return expr.Literal.Scalar.ToString(CultureInfo.InvariantCulture) + "f";
        return "0f";
    }

    private static string FormatNoise(VisualExpr expr)
    {
        if (expr.Kind != VisualExprKind.Call)
            return "Noise.Amount(0f)";
        string? scale = null;
        string? amount = null;
        foreach (var arg in expr.Args)
        {
            if (string.Equals(arg.Name, "scale", StringComparison.OrdinalIgnoreCase)
                && arg.Value.Literal is { Kind: VisualKind.Distance } d)
            {
                scale = (d.Scalar * 1000.0).ToString("0.###", CultureInfo.InvariantCulture) + ".Millimeters()";
            }

            if (string.Equals(arg.Name, "amount", StringComparison.OrdinalIgnoreCase))
                amount = FormatFloat(arg.Value);
        }

        var sb = new StringBuilder("Noise");
        if (scale is not null)
            sb.Append(".Scale(").Append(scale).Append(')');
        sb.Append(".Amount(").Append(amount ?? "0.05f").Append(')');
        return sb.ToString();
    }

    private static string Title(string name) =>
        name.Length == 0 ? name : char.ToUpperInvariant(name[0]) + name[1..];
}
