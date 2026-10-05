using System.Numerics;

namespace FrankMoat.Combat;

internal static class RaySegment
{
    public static bool TryHit(
        Vector3 origin,
        Vector3 direction,
        float length,
        Vector3 a,
        Vector3 b,
        out WallHit hit)
    {
        hit = default;
        var rdx = direction.X;
        var rdz = direction.Z;
        var sdx = b.X - a.X;
        var sdz = b.Z - a.Z;
        var denom = rdx * sdz - rdz * sdx;
        if (MathF.Abs(denom) < 1e-6f)
        {
            return false;
        }

        var ax = a.X - origin.X;
        var az = a.Z - origin.Z;
        var t = (ax * sdz - az * sdx) / denom;
        var u = (ax * rdz - az * rdx) / denom;
        if (t < 0f || t > length || u is < 0f or > 1f)
        {
            return false;
        }

        var nx = -sdz;
        var nz = sdx;
        var nlen = MathF.Sqrt(nx * nx + nz * nz);
        if (nlen < 1e-6f)
        {
            return false;
        }

        nx /= nlen;
        nz /= nlen;
        if (nx * rdx + nz * rdz > 0f)
        {
            nx = -nx;
            nz = -nz;
        }

        hit = new WallHit(origin + direction * t, new Vector3(nx, 0f, nz), t);
        return true;
    }

    public static bool TryHitCircle(
        Vector3 origin,
        Vector3 direction,
        float length,
        Vector3 center,
        float radius,
        out float distance)
    {
        var fx = origin.X - center.X;
        var fz = origin.Z - center.Z;
        var a = direction.X * direction.X + direction.Z * direction.Z;
        var b = 2f * (fx * direction.X + fz * direction.Z);
        var c = fx * fx + fz * fz - radius * radius;
        var disc = b * b - 4f * a * c;
        distance = 0f;
        if (disc < 0f || a < 1e-8f)
        {
            return false;
        }

        var t = (-b - MathF.Sqrt(disc)) / (2f * a);
        if (t < 0f || t > length)
        {
            return false;
        }

        distance = t;
        return true;
    }
}
