using Novolis.Commands.Expressions;
using Novolis.Math.Geometry;

namespace AssetStudioLab;

/// <summary>
/// Applies <see cref="FunctionCallParser"/> scripts as edits against the same
/// definition graph the source and stack editors share.
/// </summary>
public static class VisualCommandEditor
{
    public static VisualCommandResult Apply(VisualDocument document, string? script, string? focusName = null)
    {
        var parsed = FunctionCallParser.TryParseScript(script);
        if (!parsed.Success)
            return new VisualCommandResult(false, document, parsed.Message ?? "Could not parse command.");

        var current = document;
        string? focus = focusName;
        foreach (var call in parsed.Calls)
        {
            var step = ApplyCall(current, call, ref focus);
            if (!step.Success)
                return step;
            current = step.Document;
        }

        return new VisualCommandResult(true, current, "Applied.");
    }

    private static VisualCommandResult ApplyCall(VisualDocument document, FunctionCall call, ref string? focus)
    {
        var name = call.Name.ToLowerInvariant();
        var target = ResolveTarget(document, focus);
        switch (name)
        {
            case "metal":
                return SetMaterial(document, target, "color", Grey(Number(call, 0, 0.7)), ref focus);
            case "roughness":
                return SetMaterial(document, target, "roughness", VisualExpr.Lit(VisualValue.FromScalar(Number(call, 0, 0.5))), ref focus);
            case "noise":
                return AddNoise(document, target, call, ref focus);
            case "edgewear":
                return AddLayer(document, target, "edgeWear", Number(call, 0, 0.1), ref focus);
            case "cavitygrime":
                return AddLayer(document, target, "cavityGrime", Number(call, 0, 0.1), ref focus);
            case "set":
                return SetNamed(document, target, call, ref focus);
            case "duplicate":
                return Duplicate(document, target, ref focus);
            case "add":
                return AddChild(document, call, ref focus);
            case "box":
                return SetGeometry(document, "Box", call, ref focus);
            case "bevel":
                return AppendGeometryOp(document, "Bevel", call, ref focus);
            case "panelize":
                return AppendGeometryOp(document, "Panelize", call, ref focus);
            case "material":
                return BindMaterial(document, call, ref focus);
            default:
                return new VisualCommandResult(false, document, $"Unknown command '{call.Name}'.");
        }
    }

    private static VisualCommandResult SetMaterial(
        VisualDocument document,
        VisualDefinition? target,
        string property,
        VisualExpr value,
        ref string? focus)
    {
        var material = EnsureMaterial(document, target, ref focus);
        var next = material.ReplaceProperty(property, value);
        return new VisualCommandResult(true, document.Replace(next), $"Set {property}.");
    }

    private static VisualCommandResult AddNoise(
        VisualDocument document,
        VisualDefinition? target,
        FunctionCall call,
        ref string? focus)
    {
        var material = EnsureMaterial(document, target, ref focus);
        var targetName = call.Arguments.Count > 0 ? (call.Arguments[0].Text ?? "roughness") : "roughness";
        var min = Number(call, 1, 0.02);
        var max = Number(call, 2, 0.08);
        var noise = VisualExpr.Call(
            "noise",
            VisualArg.Named("min", VisualExpr.Lit(VisualValue.FromScalar(min))),
            VisualArg.Named("max", VisualExpr.Lit(VisualValue.FromScalar(max))));
        var next = material.ReplaceProperty(targetName, noise, VisualAssignOp.Multiply);
        return new VisualCommandResult(true, document.Replace(next), "Added noise.");
    }

    private static VisualCommandResult AddLayer(
        VisualDocument document,
        VisualDefinition? target,
        string layer,
        double amount,
        ref string? focus)
    {
        var material = EnsureMaterial(document, target, ref focus);
        var call = VisualExpr.Call(layer, VisualArg.Named("amount", VisualExpr.Lit(VisualValue.FromScalar(amount))));
        var next = material.ReplaceProperty(layer, call, VisualAssignOp.Layer);
        return new VisualCommandResult(true, document.Replace(next), $"Added {layer}.");
    }

    private static VisualCommandResult SetNamed(
        VisualDocument document,
        VisualDefinition? target,
        FunctionCall call,
        ref string? focus)
    {
        if (call.Arguments.Count < 2)
            return new VisualCommandResult(false, document, "Set expects a name and a value.");
        var property = call.Arguments[0].Text ?? "color";
        var expr = FromArg(call.Arguments[1]);
        return SetMaterial(document, target, property.ToLowerInvariant(), expr, ref focus);
    }

    private static VisualCommandResult Duplicate(VisualDocument document, VisualDefinition? target, ref string? focus)
    {
        if (target is null)
            return new VisualCommandResult(false, document, "Nothing to duplicate.");
        var copy = target with { Name = target.Name + "Copy" };
        focus = copy.Name;
        return new VisualCommandResult(true, document.Add(copy), $"Duplicated {target.Name}.");
    }

    private static VisualCommandResult AddChild(VisualDocument document, FunctionCall call, ref string? focus)
    {
        if (call.Arguments.Count == 0 || call.Arguments[0].Call is null)
            return new VisualCommandResult(false, document, "Add expects a nested call.");
        var nested = call.Arguments[0].Call!;
        if (nested.Name.Equals("PointLight", StringComparison.OrdinalIgnoreCase))
        {
            var light = new VisualDefinition
            {
                Kind = VisualDefinitionKind.Light,
                Name = "PointLight",
                Properties =
                [
                    new VisualProperty("point", VisualExpr.Lit(VisualValue.FromBool(true))),
                    new VisualProperty("color", FromArg(nested.Arguments.ElementAtOrDefault(0))),
                    new VisualProperty("intensity", FromArg(nested.Arguments.ElementAtOrDefault(1))),
                ],
            };
            focus = light.Name;
            return new VisualCommandResult(true, document.Add(light), "Added point light.");
        }

        return new VisualCommandResult(false, document, $"Cannot add '{nested.Name}'.");
    }

    private static VisualCommandResult SetGeometry(
        VisualDocument document,
        string op,
        FunctionCall call,
        ref string? focus)
    {
        var geometry = document.Definitions.LastOrDefault(d => d.Kind == VisualDefinitionKind.Geometry)
                       ?? new VisualDefinition { Kind = VisualDefinitionKind.Geometry, Name = "Geometry" };
        var next = geometry.ReplaceProperty(op, VisualExpr.Call(op, call.Arguments.Select(FromArgNamed).ToArray()));
        focus = next.Name;
        return new VisualCommandResult(true, document.Replace(next), $"Set {op}.");
    }

    private static VisualCommandResult AppendGeometryOp(
        VisualDocument document,
        string op,
        FunctionCall call,
        ref string? focus)
    {
        return SetGeometry(document, op, call, ref focus);
    }

    private static VisualCommandResult BindMaterial(VisualDocument document, FunctionCall call, ref string? focus)
    {
        var geometry = document.Definitions.LastOrDefault(d => d.Kind == VisualDefinitionKind.Geometry)
                       ?? new VisualDefinition { Kind = VisualDefinitionKind.Geometry, Name = "Geometry" };
        var name = call.Arguments.Count > 0 ? call.Arguments[0].Text ?? "NavalSteel" : "NavalSteel";
        var next = geometry.ReplaceProperty("Material", VisualExpr.Ident(name));
        focus = next.Name;
        return new VisualCommandResult(true, document.Replace(next), $"Bound {name}.");
    }

    private static VisualDefinition EnsureMaterial(VisualDocument document, VisualDefinition? target, ref string? focus)
    {
        if (target is { Kind: VisualDefinitionKind.Material })
            return target;
        var existing = document.Definitions.LastOrDefault(d => d.Kind == VisualDefinitionKind.Material);
        if (existing is not null)
        {
            focus = existing.Name;
            return existing;
        }

        var created = new VisualDefinition { Kind = VisualDefinitionKind.Material, Name = "Untitled" };
        focus = created.Name;
        return created;
    }

    private static VisualDefinition? ResolveTarget(VisualDocument document, string? focus) =>
        focus is null ? document.Definitions.LastOrDefault() : document.Find(focus);

    private static double Number(FunctionCall call, int index, double fallback) =>
        call.Arguments.Count > index && call.Arguments[index].Number is { } n ? n : fallback;

    private static VisualArg FromArgNamed(ExpressionArg arg) => VisualArg.Positional(FromArg(arg));

    private static VisualExpr FromArg(ExpressionArg arg)
    {
        if (arg.Call is not null)
            return VisualExpr.Call(arg.Call.Name, arg.Call.Arguments.Select(FromArgNamed).ToArray());
        if (arg.Number is { } n)
            return VisualExpr.Lit(VisualValue.FromScalar(n));
        var text = arg.Text ?? arg.Raw ?? string.Empty;
        if (VisualColors.TryParse(text.Trim('"'), out var color))
            return VisualExpr.Lit(VisualValue.FromColor(color));
        if (TryParseQuantityText(text, out var quantity))
            return VisualExpr.Lit(quantity);
        return VisualExpr.Ident(text);
    }

    private static bool TryParseQuantityText(string text, out VisualValue value)
    {
        value = default!;
        var i = 0;
        while (i < text.Length && (char.IsDigit(text[i]) || text[i] is '.' or '-'))
            i++;
        if (i == 0)
            return false;
        if (!double.TryParse(text[..i], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var n))
            return false;
        var unit = text[i..].Trim();
        return unit.Length > 0 && VisualQuantityParser.TryParse(n, unit, out value);
    }

    private static VisualExpr Grey(double metalness)
    {
        var t = (byte)Math.Clamp((int)Math.Round(70 + metalness * 80), 0, 255);
        return VisualExpr.Lit(VisualValue.FromColor(new Rgba32(t, t, t)));
    }
}
