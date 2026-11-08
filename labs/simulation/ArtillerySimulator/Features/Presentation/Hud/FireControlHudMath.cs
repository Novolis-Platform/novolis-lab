using System.Numerics;

namespace ArtillerySimulator;

internal static class FireControlHudMath
{
    public static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        var dx = b.X - a.X;
        var dz = b.Z - a.Z;
        return MathF.Sqrt(dx * dx + dz * dz);
    }
}
