using System.Numerics;
using Novolis.Cad.Primitives;
using Novolis.Math.Geometry;

namespace CalypsoCad.Generation;

/// <summary>Center / AABB helpers for lock-frame and CAD-world structure placement.</summary>
internal static class CadEntityPlacementExtensions
{
    /// <summary>World-space center of a box entity (Points[0]), or centroid of a footprint ring.</summary>
    public static Vector3 GetWorldCenter(this CadEntity entity)
    {
        if (entity.Points is { Count: >= 1 } pts && pts[0].Length >= 3)
            return new Vector3(pts[0][0], pts[0][1], pts[0][2]);

        if (entity.Footprint is { Count: >= 3 } fp)
        {
            var x = fp.Average(p => p[0]);
            var y = fp.Average(p => p.Length > 1 ? p[1] : 0f);
            var z = fp.Average(p => p.Length > 2 ? p[2] : 0f);
            return new Vector3(x, y, z);
        }

        if (entity.A is { Length: >= 3 } && entity.B is { Length: >= 3 })
            return new Vector3(
                (entity.A[0] + entity.B[0]) * 0.5f,
                (entity.A[1] + entity.B[1]) * 0.5f,
                (entity.A[2] + entity.B[2]) * 0.5f);

        return Vector3.Zero;
    }

    /// <summary>Athwart (lock Y / CAD X) center of a box or wall.</summary>
    public static float GetAthwartCenter(this CadEntity entity) => entity.GetWorldCenter().X;

    /// <summary>CAD-up center of a box or wall.</summary>
    public static float GetUpCenter(this CadEntity entity) => entity.GetWorldCenter().Y;

    /// <summary>Athwart span of an octagon ring, center-aligned on the spine.</summary>
    public static (float Min, float Max) GetAthwartRangeCentered(this (float Y, float Up)[] ring)
    {
        if (ring.Length == 0)
            return (0f, 0f);
        var min = ring.Min(v => v.Y);
        var max = ring.Max(v => v.Y);
        return (min, max).CenterAligned();
    }

    /// <summary>Centroid of an inner-octagon station ring (Y=athwart, Up=height).</summary>
    public static (float Y, float Up) GetSectionCenter(this (float Y, float Up)[] ring)
    {
        if (ring.Length == 0)
            return (0f, 0f);
        return (ring.Average(v => v.Y), ring.Average(v => v.Up));
    }
}
