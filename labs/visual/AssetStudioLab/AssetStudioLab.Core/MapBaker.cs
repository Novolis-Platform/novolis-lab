using System.Numerics;
using System.Text;
using Novolis.Math.Geometry;

namespace AssetStudioLab;

/// <summary>Samples material fields onto maps. Same source as the sphere preview.</summary>
public static class MapBaker
{
    public static BakedMaps Bake(VisualDefinition material, int size = 64)
    {
        var albedo = new Rgba32[size * size];
        var roughness = new Rgba32[size * size];
        var normal = new Rgba32[size * size];
        var sphere = SpherePreview.Render(material, size, size);
        Array.Copy(sphere, albedo, sphere.Length);
        for (var y = 0; y < size; y++)
        for (var x = 0; x < size; x++)
        {
            var u = (x + 0.5f) / size;
            var v = (y + 0.5f) / size;
            var context = new FieldContext(
                new Vector3(u, v, 0),
                Vector3.UnitZ,
                new Vector2(u, v),
                0,
                0,
                Vector3.Zero,
                0,
                Vector3.UnitZ,
                u * 9.1f);
            var r = 0.72;
            foreach (var property in material.Properties.Where(p => p.Name.Equals("roughness", StringComparison.OrdinalIgnoreCase)))
            {
                var sample = FieldEvaluator.EvaluateScalar(property.Value, context);
                r = property.Assign switch
                {
                    VisualAssignOp.Multiply => r * sample,
                    VisualAssignOp.Add => r + sample,
                    _ => sample,
                };
            }

            var byteR = (byte)Math.Clamp((int)Math.Round(r * 255), 0, 255);
            roughness[y * size + x] = new Rgba32(byteR, byteR, byteR);
            var nx = (byte)Math.Clamp((int)Math.Round((u * 2 - 1) * 127 + 128), 0, 255);
            var ny = (byte)Math.Clamp((int)Math.Round((v * 2 - 1) * 127 + 128), 0, 255);
            normal[y * size + x] = new Rgba32(nx, ny, 255);
        }

        return new BakedMaps(size, size, albedo, roughness, normal);
    }

    public static string ToPpm(Rgba32[] pixels, int width, int height)
    {
        var sb = new StringBuilder();
        sb.Append("P3\n").Append(width).Append(' ').Append(height).Append("\n255\n");
        for (var i = 0; i < pixels.Length; i++)
        {
            var p = pixels[i];
            sb.Append(p.R).Append(' ').Append(p.G).Append(' ').Append(p.B).Append('\n');
        }

        return sb.ToString();
    }
}
