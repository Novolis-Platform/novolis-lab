namespace AssetStudioLab;

/// <summary>Type-checks and lowers a visual document to normalized IR.</summary>
public static class VisualCompiler
{
    public static VisualIr Compile(VisualDocument document)
    {
        var diagnostics = new List<CompileDiagnostic>();
        var definitions = new List<IrDefinition>();
        foreach (var definition in document.Definitions)
            definitions.Add(LowerDefinition(definition, diagnostics));
        return new VisualIr(definitions, diagnostics);
    }

    private static IrDefinition LowerDefinition(VisualDefinition definition, List<CompileDiagnostic> diagnostics)
    {
        var nodes = new List<IrNode>();
        var outputs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var parameters = definition.Parameters.ToDictionary(p => p.Name, p => p, StringComparer.OrdinalIgnoreCase);
        var resolving = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenOps = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var property in definition.Properties)
        {
            var expected = VisualBuiltins.PropertyKind(property.Name);
            if (property.Assign == VisualAssignOp.Layer)
                expected = VisualKind.FieldScalar;
            var nodeId = LowerExpr(
                property.Value,
                expected,
                property.Name,
                parameters,
                resolving,
                nodes,
                seenOps,
                diagnostics);
            var op = property.Assign switch
            {
                VisualAssignOp.Multiply => "mul",
                VisualAssignOp.Add => "add",
                VisualAssignOp.Layer => LowerLayerOp(property.Name),
                _ => "bind",
            };
            if (property.Assign is VisualAssignOp.Multiply or VisualAssignOp.Add)
            {
                var previous = outputs.GetValueOrDefault(property.Name);
                if (previous is not null)
                {
                    var mixId = NextId(nodes, op);
                    nodes.Add(new IrNode(
                        mixId,
                        op,
                        expected,
                        [new IrOperand(previous, null, null), new IrOperand(nodeId, null, null)],
                        new Dictionary<string, double>()));
                    nodeId = mixId;
                }
            }

            if (property.Assign == VisualAssignOp.Layer)
            {
                var target = InferLayerTarget(property.Name);
                var previous = outputs.GetValueOrDefault(target);
                var mixId = NextId(nodes, "mix");
                nodes.Add(new IrNode(
                    mixId,
                    LowerLayerOp(property.Name),
                    VisualKind.FieldScalar,
                    [
                        new IrOperand(previous, null, target),
                        new IrOperand(nodeId, null, property.Name),
                    ],
                    new Dictionary<string, double>()));
                outputs[target] = mixId;
            }
            else
            {
                outputs[property.Name] = nodeId;
            }
        }

        foreach (var child in definition.Children)
        {
            var childIr = LowerDefinition(child, diagnostics);
            foreach (var node in childIr.Nodes)
                nodes.Add(node);
            foreach (var (key, value) in childIr.Outputs)
                outputs[$"{child.Name}.{key}"] = value;
        }

        return new IrDefinition(definition.Name, definition.Kind, Dce(nodes, outputs.Values.ToHashSet()), outputs);
    }

    private static string LowerExpr(
        VisualExpr expr,
        VisualKind expected,
        string path,
        IReadOnlyDictionary<string, VisualParameter> parameters,
        HashSet<string> resolving,
        List<IrNode> nodes,
        Dictionary<string, string> seenOps,
        List<CompileDiagnostic> diagnostics)
    {
        var actual = TypeOf(expr, parameters, diagnostics, path);
        if (!VisualBuiltins.Assignable(actual, expected))
        {
            diagnostics.Add(new CompileDiagnostic(
                CompileDiagnosticSeverity.Error,
                $"Cannot assign {actual} to {expected} at {path}."));
        }

        switch (expr.Kind)
        {
            case VisualExprKind.Literal:
            {
                var key = "lit:" + VisualQuantityParser.Format(expr.Literal!);
                if (seenOps.TryGetValue(key, out var existing))
                    return existing;
                var id = NextId(nodes, "const");
                nodes.Add(new IrNode(
                    id,
                    "const",
                    actual,
                    [new IrOperand(null, expr.Literal, null)],
                    new Dictionary<string, double> { ["x"] = expr.Literal!.Scalar }));
                seenOps[key] = id;
                return id;
            }
            case VisualExprKind.Ident:
            {
                var ident = expr.Name ?? "";
                if (VisualBuiltins.ContextNames.Contains(ident))
                    return ContextNode(ident, nodes, seenOps);
                if (resolving.Contains(ident))
                {
                    diagnostics.Add(new CompileDiagnostic(
                        CompileDiagnosticSeverity.Error,
                        $"Cycle through '{ident}'."));
                    return ContextNode("seed", nodes, seenOps);
                }

                if (parameters.TryGetValue(ident, out var parameter))
                    return LowerExpr(parameter.Default, parameter.Kind, ident, parameters, resolving, nodes, seenOps, diagnostics);
                return ContextNode(ident, nodes, seenOps);
            }
            case VisualExprKind.Context:
                return ContextNode(expr.Name ?? "position", nodes, seenOps);
            case VisualExprKind.Call:
            {
                var fn = expr.Name ?? "call";
                if (fn.Equals("bloom", StringComparison.OrdinalIgnoreCase) && expected is VisualKind.FieldScalar or VisualKind.Scalar)
                {
                    diagnostics.Add(new CompileDiagnostic(
                        CompileDiagnosticSeverity.Error,
                        $"Cannot plug Bloom (FrameEffect) into {expected} at {path}."));
                }

                var operands = new List<IrOperand>();
                for (var i = 0; i < expr.Args.Count; i++)
                {
                    var arg = expr.Args[i];
                    var argExpected = VisualBuiltins.ExpectedArg(fn, arg.Name, i);
                    var argId = LowerExpr(arg.Value, argExpected, $"{path}.{arg.Name ?? i.ToString()}", parameters, resolving, nodes, seenOps, diagnostics);
                    operands.Add(new IrOperand(argId, null, arg.Name));
                }

                var op = LowerCallOp(fn);
                var key = op + ":" + string.Join(",", operands.Select(o => o.NodeId));
                if (seenOps.TryGetValue(key, out var dup))
                    return dup;
                var id = NextId(nodes, op);
                var resultKind = VisualBuiltins.ReturnKind(fn) ?? expected;
                nodes.Add(new IrNode(id, op, resultKind, operands, new Dictionary<string, double>()));
                seenOps[key] = id;
                return id;
            }
            case VisualExprKind.Binary:
            {
                var leftKind = TypeOf(expr.Left!, parameters, diagnostics, path);
                var rightKind = TypeOf(expr.Right!, parameters, diagnostics, path);
                if (leftKind == VisualKind.Light || rightKind == VisualKind.Light
                    || leftKind == VisualKind.FrameEffect || rightKind == VisualKind.FrameEffect)
                {
                    diagnostics.Add(new CompileDiagnostic(
                        CompileDiagnosticSeverity.Error,
                        $"Operator {expr.BinaryOp} cannot combine {leftKind} and {rightKind} at {path}."));
                }

                var left = LowerExpr(expr.Left!, leftKind, path + ".left", parameters, resolving, nodes, seenOps, diagnostics);
                var right = LowerExpr(expr.Right!, rightKind, path + ".right", parameters, resolving, nodes, seenOps, diagnostics);
                var op = expr.BinaryOp switch
                {
                    VisualBinaryOp.Add => "add",
                    VisualBinaryOp.Subtract => "sub",
                    _ => "mul",
                };
                if (TryFold(expr, out var folded))
                    return LowerExpr(VisualExpr.Lit(folded), expected, path, parameters, resolving, nodes, seenOps, diagnostics);
                var id = NextId(nodes, op);
                nodes.Add(new IrNode(id, op, expected, [new IrOperand(left, null, null), new IrOperand(right, null, null)], new Dictionary<string, double>()));
                return id;
            }
            default:
                return ContextNode("seed", nodes, seenOps);
        }
    }

    private static VisualKind TypeOf(
        VisualExpr expr,
        IReadOnlyDictionary<string, VisualParameter> parameters,
        List<CompileDiagnostic> diagnostics,
        string path)
    {
        _ = diagnostics;
        _ = path;
        return expr.Kind switch
        {
            VisualExprKind.Literal => expr.Literal?.Kind ?? VisualKind.Scalar,
            VisualExprKind.Ident when VisualBuiltins.ContextNames.Contains(expr.Name ?? "") =>
                expr.Name!.Equals("normal", StringComparison.OrdinalIgnoreCase)
                || expr.Name.Equals("position", StringComparison.OrdinalIgnoreCase)
                || expr.Name.Equals("velocity", StringComparison.OrdinalIgnoreCase)
                || expr.Name.Equals("viewDirection", StringComparison.OrdinalIgnoreCase)
                    ? VisualKind.Vector3
                    : VisualKind.Scalar,
            VisualExprKind.Ident when parameters.TryGetValue(expr.Name ?? "", out var p) => p.Kind,
            VisualExprKind.Call => VisualBuiltins.ReturnKind(expr.Name ?? "") ?? VisualKind.Scalar,
            VisualExprKind.Binary when expr.Left is not null && expr.Right is not null =>
                CombineKinds(
                    TypeOf(expr.Left, parameters, diagnostics, path),
                    TypeOf(expr.Right, parameters, diagnostics, path)),
            _ => VisualKind.Scalar,
        };
    }

    private static VisualKind CombineKinds(VisualKind left, VisualKind right)
    {
        if (left == VisualKind.Color || right == VisualKind.Color)
            return VisualKind.Color;
        if (left == VisualKind.FieldColor || right == VisualKind.FieldColor)
            return VisualKind.FieldColor;
        if (left == VisualKind.Light || right == VisualKind.Light)
            return VisualKind.Light;
        if (left == VisualKind.FrameEffect || right == VisualKind.FrameEffect)
            return VisualKind.FrameEffect;
        return left;
    }

    private static bool TryFold(VisualExpr expr, out VisualValue value)
    {
        value = default!;
        if (expr is not { Kind: VisualExprKind.Binary, Left.Kind: VisualExprKind.Literal, Right.Kind: VisualExprKind.Literal })
            return false;
        var a = expr.Left.Literal!;
        var b = expr.Right.Literal!;
        if (a.Kind == VisualKind.Color || b.Kind == VisualKind.Color)
        {
            var scale = b.Kind == VisualKind.Color ? 1 : b.Scalar;
            var color = a.Kind == VisualKind.Color ? a : b;
            value = VisualValue.FromColor(color.ToRgba32(), color.X * scale, color.Y * scale, color.Z * scale);
            return true;
        }

        if (a.Kind is VisualKind.Scalar or VisualKind.Distance && b.Kind is VisualKind.Scalar or VisualKind.Distance)
        {
            var n = expr.BinaryOp switch
            {
                VisualBinaryOp.Add => a.Scalar + b.Scalar,
                VisualBinaryOp.Subtract => a.Scalar - b.Scalar,
                _ => a.Scalar * b.Scalar,
            };
            value = VisualValue.FromScalar(n, a.Unit);
            return true;
        }

        return false;
    }

    private static string ContextNode(string name, List<IrNode> nodes, Dictionary<string, string> seenOps)
    {
        var key = "ctx:" + name.ToLowerInvariant();
        if (seenOps.TryGetValue(key, out var existing))
            return existing;
        var id = NextId(nodes, "context");
        nodes.Add(new IrNode(id, "context", VisualKind.Vector3, [new IrOperand(null, null, name)], new Dictionary<string, double>()));
        seenOps[key] = id;
        return id;
    }

    private static string LowerCallOp(string name) =>
        name.ToLowerInvariant() switch
        {
            "edgewear" => "scalar_from_curvature",
            "cavitygrime" or "cavity" => "scalar_from_cavity",
            "noise" => "hash_noise",
            "brushed" => "anisotropy",
            "radial" => "radial_mix",
            "fade" => "age_fade",
            "bloom" => "frame_bloom",
            _ => name.ToLowerInvariant(),
        };

    private static string LowerLayerOp(string name) =>
        name.ToLowerInvariant() switch
        {
            "edgewear" => "scalar_from_curvature",
            "cavitygrime" => "scalar_from_cavity",
            _ => "mix",
        };

    private static string InferLayerTarget(string name) =>
        name.ToLowerInvariant() switch
        {
            "edgewear" => "color",
            "cavitygrime" => "color",
            _ => "roughness",
        };

    private static string NextId(List<IrNode> nodes, string prefix) => $"{prefix}_{nodes.Count + 1}";

    private static IReadOnlyList<IrNode> Dce(List<IrNode> nodes, HashSet<string> live)
    {
        var byId = nodes.ToDictionary(n => n.Id, StringComparer.Ordinal);
        var keep = new HashSet<string>(live, StringComparer.Ordinal);
        var stack = new Stack<string>(live);
        while (stack.Count > 0)
        {
            var id = stack.Pop();
            if (!byId.TryGetValue(id, out var node))
                continue;
            foreach (var operand in node.Operands)
            {
                if (operand.NodeId is not null && keep.Add(operand.NodeId))
                    stack.Push(operand.NodeId);
            }
        }

        return nodes.Where(n => keep.Contains(n.Id)).ToArray();
    }
}
