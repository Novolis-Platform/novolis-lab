using System.Drawing;
using System.Numerics;
using Novolis.Physics.Collision.Simple;
using Novolis.Raylib.Game;

namespace RagdollPlay.Game;

internal static class RaySphere
{
    public static bool TryHit(Vector3 origin, Vector3 direction, Vector3 center, float radius, out float t)
    {
        var oc = origin - center;
        var a = Vector3.Dot(direction, direction);
        var b = 2f * Vector3.Dot(oc, direction);
        var c = Vector3.Dot(oc, oc) - radius * radius;
        var disc = b * b - 4f * a * c;
        if (disc < 0f || MathF.Abs(a) < 1e-8f)
        {
            t = -1f;
            return false;
        }

        t = (-b - MathF.Sqrt(disc)) / (2f * a);
        return t >= 0f;
    }
}
