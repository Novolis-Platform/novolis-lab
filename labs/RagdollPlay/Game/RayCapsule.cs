using System.Drawing;
using System.Numerics;
using Novolis.Physics.Collision.Simple;
using Novolis.Raylib.Game;

namespace RagdollPlay.Game;

internal static class RayCapsule
{
    public static bool TryHit(Vector3 origin, Vector3 dir, Vector3 a, Vector3 b, float radius, out float t)
    {
        var ab = b - a;
        var abLenSq = ab.LengthSquared();
        if (abLenSq < 1e-8f)
            return RaySphere.TryHit(origin, dir, a, radius, out t);

        var ao = origin - a;
        var dab = Vector3.Dot(dir, ab);
        var daa = Vector3.Dot(dir, ao);
        var aab = Vector3.Dot(ab, ao);

        var aCoeff = Vector3.Dot(dir, dir) - dab * dab / abLenSq;
        var bCoeff = 2f * (daa - dab * aab / abLenSq);
        var cCoeff = Vector3.Dot(ao, ao) - aab * aab / abLenSq - radius * radius;

        if (MathF.Abs(aCoeff) < 1e-8f)
        {
            t = -1f;
            return false;
        }

        var disc = bCoeff * bCoeff - 4f * aCoeff * cCoeff;
        if (disc < 0f)
        {
            t = -1f;
            return false;
        }

        t = (-bCoeff - MathF.Sqrt(disc)) / (2f * aCoeff);
        return t >= 0f;
    }
}
