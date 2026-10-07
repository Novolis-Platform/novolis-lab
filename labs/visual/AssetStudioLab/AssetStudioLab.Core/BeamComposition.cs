using System.Numerics;
using Novolis.Math.Geometry;

namespace AssetStudioLab;

/// <summary>
/// Visible flashlight beam is not an asset — it is the product of evaluating
/// a light definition together with a volume definition.
/// </summary>
public static class BeamComposition
{
    public static Rgba32[] Render(VisualDefinition light, VisualDefinition volume, int width, int height)
    {
        var pixels = new Rgba32[width * height];
        var intensity = PropertyScalar(light, "intensity", 1700);
        var range = PropertyScalar(light, "range", 28);
        var cone = PropertyScalar(light, "cone", 44 * Math.PI / 180.0);
        var densityExpr = ComposeScalar(volume, "density", 0.025);
        var lightPos = new Vector3(0, 0, -0.2f);
        var lightDir = Vector3.UnitZ;
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            var u = (x + 0.5) / width * 2 - 1;
            var v = 1 - (y + 0.5) / height * 2;
            var dir = Vector3.Normalize(new Vector3((float)u * 0.6f, (float)v * 0.6f, 1));
            var origin = new Vector3(0, 0, -1);
            var acc = Vector3.Zero;
            var transmittance = 1f;
            const int steps = 32;
            for (var i = 0; i < steps; i++)
            {
                var t = (i + 0.5f) / steps * 2.4f;
                var p = origin + dir * t;
                var toLight = p - lightPos;
                var dist = toLight.Length();
                var toLightN = dist > 0 ? toLight / dist : lightDir;
                var angle = Math.Acos(Math.Clamp(Vector3.Dot(Vector3.Normalize(lightDir), toLightN), -1, 1));
                var inCone = angle < cone ? 1.0 : Math.Exp(-(angle - cone) * 18);
                var atten = inCone * intensity / (1 + dist * dist / Math.Max(range, 0.1));
                var context = new FieldContext(
                    p,
                    Vector3.UnitY,
                    new Vector2(p.X, p.Y),
                    0,
                    0,
                    Vector3.Zero,
                    dist,
                    -dir,
                    p.X * 3.1f);
                var density = Math.Max(0, FieldEvaluator.EvaluateScalar(densityExpr, context));
                var scatter = (float)(density * atten * 0.0004);
                acc += new Vector3(1.0f, 0.96f, 0.82f) * scatter * transmittance;
                transmittance *= (float)Math.Exp(-density * 0.08);
            }

            pixels[y * width + x] = new Rgba32(
                ToByte(acc.X),
                ToByte(acc.Y),
                ToByte(acc.Z));
        }

        return pixels;
    }

    public static float MeanLuminance(Rgba32[] pixels)
    {
        if (pixels.Length == 0)
            return 0;
        double sum = 0;
        foreach (var p in pixels)
            sum += (0.2126 * p.R + 0.7152 * p.G + 0.0722 * p.B) / 255.0;
        return (float)(sum / pixels.Length);
    }

    private static VisualExpr ComposeScalar(VisualDefinition definition, string name, double fallback)
    {
        VisualExpr expr = VisualExpr.Lit(VisualValue.FromScalar(fallback));
        foreach (var property in definition.Properties.Where(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
        {
            expr = property.Assign switch
            {
                VisualAssignOp.Multiply => VisualExpr.Binary(VisualBinaryOp.Multiply, expr, property.Value),
                VisualAssignOp.Add => VisualExpr.Binary(VisualBinaryOp.Add, expr, property.Value),
                _ => property.Value,
            };
        }

        return expr;
    }

    private static double PropertyScalar(VisualDefinition definition, string name, double fallback)
    {
        var property = definition.Properties.LastOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (property is null)
            return fallback;
        var zero = new FieldContext(Vector3.Zero, Vector3.UnitY, Vector2.Zero, 0, 0, Vector3.Zero, 0, Vector3.UnitZ, 0);
        return FieldEvaluator.EvaluateScalar(property.Value, zero);
    }

    private static byte ToByte(double channel) =>
        (byte)Math.Clamp((int)Math.Round(channel * 255.0), 0, 255);
}
