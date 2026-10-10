using System.Numerics;
using System.Runtime.InteropServices;

namespace ArtillerySimulator.Game;

/// <summary>Extends raylib perspective far clip (default 4000 m) for km-scale scenes.</summary>
internal static class RaylibClipPlanes
{
    [DllImport("raylib", EntryPoint = "rlSetClipPlanes")]
    private static extern void SetClipPlanesNative(double nearPlane, double farPlane);

    public static void ApplyForExtent(Vector3 eye, float extentMeters)
    {
        var maxCornerDist = MaxDistanceToRangeBox(eye, extentMeters);
        var far = Math.Max(maxCornerDist * 1.35, extentMeters * 3.5);
        var near = Math.Clamp(far * 0.00002, 0.5, 25.0);
        SetClipPlanesNative(near, far);
    }

    private static float MaxDistanceToRangeBox(Vector3 eye, float extent)
    {
        var max = 0f;
        for (var xi = 0; xi < 2; xi++)
        for (var zi = 0; zi < 2; zi++)
        {
            var corner = new Vector3(xi * extent, 0f, zi * extent);
            max = MathF.Max(max, Vector3.Distance(eye, corner));
        }

        return max;
    }
}
