using System.Numerics;
using MeshBench.Models;

namespace MeshBench.Services;

internal static class SceneBounds
{
    public static (Vector3 Center, float Radius) Compute(MeshSceneDocument scene)
    {
        if (scene.Parts.Count == 0)
            return (new Vector3(0f, 0.45f, 0f), 2.5f);

        var min = new Vector3(float.PositiveInfinity);
        var max = new Vector3(float.NegativeInfinity);

        foreach (var part in scene.Parts)
        {
            var center = ToVector3(part.Center);
            if (part.Kind.Equals("sphere", StringComparison.OrdinalIgnoreCase))
            {
                var r = part.Radius;
                Expand(ref min, ref max, center - new Vector3(r));
                Expand(ref min, ref max, center + new Vector3(r));
            }
            else
            {
                var he = ToVector3(part.HalfExtents);
                Expand(ref min, ref max, center - he);
                Expand(ref min, ref max, center + he);
            }
        }

        var boundsCenter = (min + max) * 0.5f;
        var extent = max - min;
        var radius = MathF.Max(0.75f, MathF.Max(extent.X, MathF.Max(extent.Y, extent.Z)) * 0.65f);
        return (boundsCenter, radius);
    }

    private static void Expand(ref Vector3 min, ref Vector3 max, Vector3 point)
    {
        min = Vector3.Min(min, point);
        max = Vector3.Max(max, point);
    }

    private static Vector3 ToVector3(float[] values) =>
        values.Length >= 3 ? new Vector3(values[0], values[1], values[2]) : Vector3.Zero;
}
