using System.Globalization;
using System.Text;

namespace AssetStudioLab;

/// <summary>Pretty-prints a document as visual source.</summary>
public static class VisualLanguageFormatter
{
    public static string Format(VisualDocument document)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < document.Definitions.Count; i++)
        {
            if (i > 0)
                sb.AppendLine();
            FormatDefinition(sb, document.Definitions[i], 0);
        }

        return sb.ToString();
    }

    private static void FormatDefinition(StringBuilder sb, VisualDefinition definition, int indent)
    {
        Pad(sb, indent);
        sb.Append(KindKeyword(definition.Kind));
        sb.Append(' ');
        sb.Append(definition.Name);
        if (definition.Parameters.Count > 0)
        {
            sb.Append('(');
            for (var i = 0; i < definition.Parameters.Count; i++)
            {
                if (i > 0)
                    sb.Append(", ");
                var p = definition.Parameters[i];
                sb.Append(p.Name);
                sb.Append(" = ");
                sb.Append(FormatExpr(p.Default));
            }

            sb.Append(')');
        }

        sb.AppendLine(" {");
        foreach (var property in definition.Properties)
        {
            Pad(sb, indent + 1);
            if (property.Assign == VisualAssignOp.Layer)
            {
                sb.Append("layer ");
                sb.Append(FormatExpr(property.Value));
            }
            else
            {
                sb.Append(property.Name);
                sb.Append(property.Assign switch
                {
                    VisualAssignOp.Multiply => " *= ",
                    VisualAssignOp.Add => " += ",
                    _ => " ",
                });
                sb.Append(FormatExpr(property.Value));
            }

            sb.AppendLine();
        }

        foreach (var child in definition.Children)
            FormatDefinition(sb, child, indent + 1);

        Pad(sb, indent);
        sb.AppendLine("}");
    }

    public static string FormatExpr(VisualExpr expr) =>
        expr.Kind switch
        {
            VisualExprKind.Literal when expr.Literal is not null => FormatLiteral(expr.Literal),
            VisualExprKind.Ident => expr.Name ?? string.Empty,
            VisualExprKind.Context => expr.Name ?? string.Empty,
            VisualExprKind.Call => FormatCall(expr),
            VisualExprKind.Binary => FormatBinary(expr),
            _ => "?",
        };

    private static string FormatCall(VisualExpr expr)
    {
        var sb = new StringBuilder();
        sb.Append(expr.Name);
        sb.Append('(');
        for (var i = 0; i < expr.Args.Count; i++)
        {
            if (i > 0)
                sb.Append(", ");
            var arg = expr.Args[i];
            if (!string.IsNullOrEmpty(arg.Name))
            {
                sb.Append(arg.Name);
                sb.Append(": ");
            }

            sb.Append(FormatExpr(arg.Value));
        }

        sb.Append(')');
        return sb.ToString();
    }

    private static string FormatBinary(VisualExpr expr)
    {
        var op = expr.BinaryOp switch
        {
            VisualBinaryOp.Add => "+",
            VisualBinaryOp.Subtract => "-",
            _ => "*",
        };
        return $"{FormatExpr(expr.Left!)} {op} {FormatExpr(expr.Right!)}";
    }

    private static string FormatLiteral(VisualValue value)
    {
        if (value.Kind == VisualKind.Color)
            return VisualQuantityParser.FormatColor(value);
        if (value.Kind == VisualKind.Bool)
            return value.Flag ? "true" : "false";
        if (value.Kind == VisualKind.Vector3
            && value.X == 0 && value.Y == 1 && value.Z == 0)
            return "y";
        if (!string.IsNullOrEmpty(value.Text))
            return value.Text;
        if (value.Kind is VisualKind.Scalar && value.Unit == VisualUnit.None)
            return value.Scalar.ToString("0.###", CultureInfo.InvariantCulture);
        return VisualQuantityParser.Format(value);
    }

    private static string KindKeyword(VisualDefinitionKind kind) =>
        kind switch
        {
            VisualDefinitionKind.Material => "material",
            VisualDefinitionKind.Light => "light",
            VisualDefinitionKind.Volume => "volume",
            VisualDefinitionKind.Effect => "effect",
            VisualDefinitionKind.Geometry => "geometry",
            VisualDefinitionKind.Surface => "surface",
            VisualDefinitionKind.Trail => "trail",
            VisualDefinitionKind.Emitter => "emitter",
            _ => "material",
        };

    private static void Pad(StringBuilder sb, int indent)
    {
        for (var i = 0; i < indent; i++)
            sb.Append("    ");
    }
}
