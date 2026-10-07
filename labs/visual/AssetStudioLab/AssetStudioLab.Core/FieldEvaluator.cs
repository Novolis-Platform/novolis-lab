using System.Numerics;
using Novolis.Math.Geometry;

namespace AssetStudioLab;

/// <summary>CPU field evaluator used by previews and baking.</summary>
public static class FieldEvaluator
{
    public static VisualValue Evaluate(VisualExpr expr, FieldContext context)
    {
        switch (expr.Kind)
        {
            case VisualExprKind.Literal:
                return expr.Literal ?? VisualValue.FromScalar(0);
            case VisualExprKind.Ident:
            case VisualExprKind.Context:
                return EvaluateIdent(expr.Name ?? "", context);
            case VisualExprKind.Call:
                return EvaluateCall(expr, context);
            case VisualExprKind.Binary:
                return EvaluateBinary(expr, context);
            default:
                return VisualValue.FromScalar(0);
        }
    }

    public static Rgba32 EvaluateColor(VisualExpr expr, FieldContext context)
    {
        var value = Evaluate(expr, context);
        return value.Kind == VisualKind.Color
            ? new Rgba32(ToByte(value.X), ToByte(value.Y), ToByte(value.Z))
            : value.ToRgba32();
    }

    public static double EvaluateScalar(VisualExpr expr, FieldContext context) =>
        Evaluate(expr, context).Scalar;

    private static VisualValue EvaluateIdent(string name, FieldContext context) =>
        name.ToLowerInvariant() switch
        {
            "position" => VisualValue.FromVector3(context.Position.X, context.Position.Y, context.Position.Z),
            "normal" => VisualValue.FromVector3(context.Normal.X, context.Normal.Y, context.Normal.Z),
            "uv" => VisualValue.FromScalar(context.Uv.X),
            "time" => VisualValue.FromScalar(context.Time),
            "age" => VisualValue.FromScalar(context.Age),
            "seed" => VisualValue.FromScalar(context.Seed),
            "distance" => VisualValue.FromScalar(context.Distance),
            "inherit" => VisualValue.FromVector3(context.Velocity.X, context.Velocity.Y, context.Velocity.Z),
            _ => VisualValue.FromScalar(0),
        };

    private static VisualValue EvaluateCall(VisualExpr expr, FieldContext context)
    {
        var fn = expr.Name?.ToLowerInvariant() ?? "";
        double Arg(string name, int index, double fallback)
        {
            var named = expr.Args.FirstOrDefault(a => string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase));
            if (named is not null)
                return EvaluateScalar(named.Value, context);
            if (index < expr.Args.Count)
                return EvaluateScalar(expr.Args[index].Value, context);
            return fallback;
        }

        VisualExpr? ArgExpr(string name, int index) =>
            expr.Args.FirstOrDefault(a => string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase))?.Value
            ?? (index < expr.Args.Count ? expr.Args[index].Value : null);

        switch (fn)
        {
            case "noise":
            {
                var scale = Arg("scale", 0, 1);
                var amount = Arg("amount", 1, 0.05);
                var min = Arg("min", 2, 0);
                var max = Arg("max", 3, 1);
                var n = HashNoise(context.Position * (float)(scale <= 0 ? 1 : 1.0 / scale));
                return VisualValue.FromScalar(min + (max - min) * n * amount + (1 - amount) * 0.5);
            }
            case "edgewear":
            {
                var amount = Arg("amount", 0, 0.08);
                var rim = 1.0 - Math.Abs(Vector3.Dot(context.Normal, Vector3.UnitY));
                return VisualValue.FromScalar(rim * amount);
            }
            case "cavitygrime":
            case "cavity":
            {
                var amount = Arg("amount", 0, 0.12);
                var cavity = Math.Clamp(0.5 - context.Normal.Y, 0, 1);
                return VisualValue.FromScalar(cavity * amount);
            }
            case "brushed":
            {
                var strength = Arg("strength", 1, 0.03);
                return VisualValue.FromVector3(context.Normal.X, context.Normal.Y, context.Normal.Z + (float)strength);
            }
            case "radial":
            {
                var core = ArgExpr("core", 0);
                var edge = ArgExpr("edge", 1);
                var t = Math.Clamp(context.Uv.X, 0, 1);
                var a = core is null ? VisualValue.FromColor(Rgba32.White) : Evaluate(core, context);
                var b = edge is null ? VisualValue.FromColor(Rgba32.Red) : Evaluate(edge, context);
                return VisualValue.FromColor(
                    Rgba32.Black,
                    a.X + (b.X - a.X) * t,
                    a.Y + (b.Y - a.Y) * t,
                    a.Z + (b.Z - a.Z) * t);
            }
            case "fade":
                return VisualValue.FromScalar(Math.Clamp(1.0 - context.Age, 0, 1));
            case "mix":
            {
                var a = expr.Args.Count > 0 ? EvaluateScalar(expr.Args[0].Value, context) : 0;
                var b = expr.Args.Count > 1 ? EvaluateScalar(expr.Args[1].Value, context) : 1;
                var t = expr.Args.Count > 2 ? EvaluateScalar(expr.Args[2].Value, context) : 0.5;
                return VisualValue.FromScalar(a + (b - a) * t);
            }
            default:
                return VisualValue.FromScalar(0);
        }
    }

    private static VisualValue EvaluateBinary(VisualExpr expr, FieldContext context)
    {
        var left = Evaluate(expr.Left!, context);
        var right = Evaluate(expr.Right!, context);
        if (left.Kind == VisualKind.Color || right.Kind == VisualKind.Color)
        {
            var scale = right.Kind == VisualKind.Color ? 1 : right.Scalar;
            var color = left.Kind == VisualKind.Color ? left : right;
            var otherScale = left.Kind == VisualKind.Color ? scale : left.Scalar;
            return VisualValue.FromColor(
                color.ToRgba32(),
                color.X * otherScale,
                color.Y * otherScale,
                color.Z * otherScale);
        }

        var n = expr.BinaryOp switch
        {
            VisualBinaryOp.Add => left.Scalar + right.Scalar,
            VisualBinaryOp.Subtract => left.Scalar - right.Scalar,
            _ => left.Scalar * right.Scalar,
        };
        return VisualValue.FromScalar(n);
    }

    public static double HashNoise(Vector3 p)
    {
        var n = Math.Sin(p.X * 12.9898 + p.Y * 78.233 + p.Z * 37.719) * 43758.5453;
        return n - Math.Floor(n);
    }

    private static byte ToByte(double channel) =>
        (byte)Math.Clamp((int)Math.Round(channel * 255.0), 0, 255);
}
