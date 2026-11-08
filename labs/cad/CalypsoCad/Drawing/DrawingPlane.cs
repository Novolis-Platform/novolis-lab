using System.Numerics;

namespace CalypsoCad.Drawing;

/// <summary>Lightweight plane (origin + unit normal). Avoids inventing a parallel math package.</summary>
internal readonly record struct DrawingPlane(Vector3 Origin, Vector3 Normal)
{
    public static DrawingPlane FromPointNormal(Vector3 origin, Vector3 normal)
    {
        var n = Vector3.Normalize(normal);
        if (n.LengthSquared() < 1e-12f)
            throw new ArgumentException("Section plane normal must be non-zero.", nameof(normal));
        return new DrawingPlane(origin, n);
    }

    public float SignedDistance(Vector3 p) => Vector3.Dot(p - Origin, Normal);
}
