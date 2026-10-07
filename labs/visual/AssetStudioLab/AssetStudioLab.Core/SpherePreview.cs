using System.Numerics;
using Novolis.Math.Geometry;

namespace AssetStudioLab;

/// <summary>CPU sphere preview of a material definition.</summary>
public static class SpherePreview
{
    public static Rgba32[] Render(VisualDefinition material, int width, int height)
    {
        var pixels = new Rgba32[width * height];
        var colorExpr = material.Properties.LastOrDefault(p => p.Name.Equals("color", StringComparison.OrdinalIgnoreCase) && p.Assign == VisualAssignOp.Set)?.Value
                        ?? VisualExpr.Lit(VisualValue.FromColor(new Rgba32(0x74, 0x7a, 0x7d)));
        var roughnessExpr = CompileScalar(material, "roughness", 0.72);
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            var u = (x + 0.5) / width * 2 - 1;
            var v = 1 - (y + 0.5) / height * 2;
            var r2 = u * u + v * v;
            if (r2 > 1)
            {
                pixels[y * width + x] = new Rgba32(12, 14, 18);
                continue;
            }

            var z = Math.Sqrt(1 - r2);
            var normal = Vector3.Normalize(new Vector3((float)u, (float)v, (float)z));
            var context = new FieldContext(
                normal,
                normal,
                new Vector2((float)((u + 1) * 0.5), (float)((v + 1) * 0.5)),
                0,
                0,
                Vector3.Zero,
                (float)z,
                Vector3.UnitZ,
                (float)(x * 0.137 + y * 0.091));
            var color = EvaluateAlbedo(material, colorExpr, context);
            var roughness = FieldEvaluator.EvaluateScalar(roughnessExpr, context);
            var light = Math.Clamp(Vector3.Dot(normal, Vector3.Normalize(new Vector3(-0.35f, 0.65f, 0.7f))), 0.08f, 1f);
            var spec = Math.Pow(Math.Max(0, Vector3.Dot(normal, Vector3.Normalize(new Vector3(-0.2f, 0.4f, 1f)))), 8) * (1 - roughness);
            pixels[y * width + x] = new Rgba32(
                ToByte(color.X * light + spec),
                ToByte(color.Y * light + spec),
                ToByte(color.Z * light + spec));
        }

        return pixels;
    }

    private static VisualValue EvaluateAlbedo(VisualDefinition material, VisualExpr colorExpr, FieldContext context)
    {
        var color = FieldEvaluator.Evaluate(colorExpr, context);
        foreach (var property in material.Properties)
        {
            if (property.Assign == VisualAssignOp.Layer)
            {
                var amount = FieldEvaluator.EvaluateScalar(property.Value, context);
                if (property.Name.Equals("edgeWear", StringComparison.OrdinalIgnoreCase))
                {
                    color = VisualValue.FromColor(color.ToRgba32(),
                        color.X + amount * 0.35,
                        color.Y + amount * 0.32,
                        color.Z + amount * 0.28);
                }
                else if (property.Name.Equals("cavityGrime", StringComparison.OrdinalIgnoreCase))
                {
                    color = VisualValue.FromColor(color.ToRgba32(),
                        color.X * (1 - amount * 2),
                        color.Y * (1 - amount * 2),
                        color.Z * (1 - amount * 2));
                }
            }
        }

        return color;
    }

    private static VisualExpr CompileScalar(VisualDefinition material, string name, double fallback)
    {
        VisualExpr expr = VisualExpr.Lit(VisualValue.FromScalar(fallback));
        foreach (var property in material.Properties.Where(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
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

    private static byte ToByte(double channel) =>
        (byte)Math.Clamp((int)Math.Round(channel * 255.0), 0, 255);
}
