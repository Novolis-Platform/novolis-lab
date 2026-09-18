using System.Numerics;
using Novolis.Rendering.Materials;
using Novolis.Rendering.Scene;

namespace MeshBench;

internal static class MeshPrimitives
{
    public static SceneBuilder AddGroundAndLight(SceneBuilder builder) =>
        builder
            .AddGround(MaterialPresets.Standard(new Vector3(0.22f, 0.24f, 0.28f), roughness: 0.92f), size: 8f)
            .AddDirectionalLight(new Vector3(-0.4f, -1f, -0.25f), new Vector3(1f, 0.98f, 0.94f), 2.2f);

    public static void AddBox(SceneBuilder builder, Vector3 center, Vector3 halfExtents, Vector3 color) =>
        builder.AddBox(center, halfExtents, MaterialPresets.Standard(color, roughness: 0.45f));

    public static void AddSphere(SceneBuilder builder, Vector3 center, float radius, Vector3 color, int segments = 24)
    {
        var (vertices, indices) = CreateUvSphere(radius, segments);
        var transform = Matrix4x4.CreateTranslation(center);
        builder.AddMesh(vertices, indices, MaterialPresets.Standard(color, roughness: 0.38f), transform);
    }

    public static (Vector3[] Vertices, int[] Indices) CreateUvSphere(float radius, int segments)
    {
        segments = Math.Clamp(segments, 8, 64);
        var rings = segments;
        var sectors = segments * 2;
        var vertices = new List<Vector3>((rings + 1) * (sectors + 1));
        for (var r = 0; r <= rings; r++)
        {
            var v = r / (float)rings;
            var phi = v * MathF.PI;
            for (var s = 0; s <= sectors; s++)
            {
                var u = s / (float)sectors;
                var theta = u * MathF.Tau;
                var x = MathF.Sin(phi) * MathF.Cos(theta);
                var y = MathF.Cos(phi);
                var z = MathF.Sin(phi) * MathF.Sin(theta);
                vertices.Add(new Vector3(x, y, z) * radius);
            }
        }

        var indices = new List<int>();
        for (var r = 0; r < rings; r++)
        {
            for (var s = 0; s < sectors; s++)
            {
                var a = r * (sectors + 1) + s;
                var b = a + sectors + 1;
                indices.Add(a);
                indices.Add(b);
                indices.Add(a + 1);
                indices.Add(b);
                indices.Add(b + 1);
                indices.Add(a + 1);
            }
        }

        return (vertices.ToArray(), indices.ToArray());
    }
}
