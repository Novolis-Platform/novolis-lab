using System.Text;

namespace AssetStudioLab;

/// <summary>Pretty stack view of the same AST the source editor shows.</summary>
public static class VisualStackPrinter
{
    public static string Print(VisualDocument document)
    {
        var sb = new StringBuilder();
        foreach (var definition in document.Definitions)
        {
            if (sb.Length > 0)
                sb.AppendLine();
            PrintDefinition(sb, definition, 0);
        }

        return sb.ToString().TrimEnd();
    }

    private static void PrintDefinition(StringBuilder sb, VisualDefinition definition, int indent)
    {
        Pad(sb, indent);
        sb.AppendLine(definition.Name);
        var sets = definition.Properties.Where(p => p.Assign == VisualAssignOp.Set).ToList();
        var mods = definition.Properties.Where(p => p.Assign is VisualAssignOp.Multiply or VisualAssignOp.Add).ToList();
        var layers = definition.Properties.Where(p => p.Assign == VisualAssignOp.Layer).ToList();

        foreach (var property in sets)
        {
            Pad(sb, indent);
            sb.Append(Label(property.Name).PadRight(16));
            sb.AppendLine(VisualLanguageFormatter.FormatExpr(property.Value));
        }

        if (mods.Count + layers.Count > 0 || definition.Children.Count > 0)
        {
            sb.AppendLine();
            Pad(sb, indent);
            sb.AppendLine("Layers");
            var items = mods.Concat(layers).ToList();
            for (var i = 0; i < items.Count; i++)
            {
                var last = i == items.Count - 1 && definition.Children.Count == 0;
                PrintLayer(sb, items[i], indent, last);
            }

            for (var i = 0; i < definition.Children.Count; i++)
            {
                var last = i == definition.Children.Count - 1;
                Pad(sb, indent);
                sb.Append(last ? "└─ " : "├─ ");
                sb.AppendLine(definition.Children[i].Name);
                PrintDefinition(sb, definition.Children[i], indent + 1);
            }
        }
    }

    private static void PrintLayer(StringBuilder sb, VisualProperty property, int indent, bool last)
    {
        Pad(sb, indent);
        sb.Append(last ? "└─ " : "├─ ");
        if (property.Assign == VisualAssignOp.Layer)
        {
            sb.Append(SplitName(property.Name));
            if (property.Value.Args.Count == 1)
            {
                sb.Append("    ");
                sb.Append(VisualLanguageFormatter.FormatExpr(property.Value.Args[0].Value));
            }

            sb.AppendLine();
            return;
        }

        sb.AppendLine(TitleCase(property.Value.Name ?? property.Name));
        var args = property.Value.Kind == VisualExprKind.Call ? property.Value.Args : [];
        for (var a = 0; a < args.Count; a++)
        {
            Pad(sb, indent);
            sb.Append(last ? "    " : "│   ");
            var name = args[a].Name ?? "Value";
            sb.Append(Label(name).PadRight(12));
            sb.AppendLine(VisualLanguageFormatter.FormatExpr(args[a].Value));
        }

        Pad(sb, indent);
        sb.Append(last ? "    " : "│   ");
        sb.Append("Target      ");
        sb.AppendLine(Label(property.Name));
    }

    private static string Label(string name)
    {
        if (name.Equals("color", StringComparison.OrdinalIgnoreCase))
            return "Base Color";
        return TitleCase(name);
    }

    private static string SplitName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return name;
        var sb = new StringBuilder();
        sb.Append(char.ToUpperInvariant(name[0]));
        for (var i = 1; i < name.Length; i++)
        {
            if (char.IsUpper(name[i]) && sb.Length > 0)
                sb.Append(' ');
            sb.Append(name[i]);
        }

        return sb.ToString();
    }

    private static string TitleCase(string name)
    {
        if (string.IsNullOrEmpty(name))
            return name;
        var chars = new char[name.Length];
        chars[0] = char.ToUpperInvariant(name[0]);
        for (var i = 1; i < name.Length; i++)
            chars[i] = name[i];
        return new string(chars);
    }

    private static void Pad(StringBuilder sb, int indent)
    {
        for (var i = 0; i < indent; i++)
            sb.Append("    ");
    }
}
