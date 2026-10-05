using System.Numerics;

namespace FrankMoat.Rendering;

internal static class HeightProjection
{
    public static readonly Vector3 Direction = new(-0.28f, 0f, 0.62f);

    public static Vector3 Offset(float height) => Direction * height;

    public static Vector3 Elevate(Vector3 gameplay, float elevation) => gameplay + Direction * elevation;
}
