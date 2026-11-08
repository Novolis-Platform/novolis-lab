using System.Numerics;

namespace CalypsoCad.Drawing;

internal readonly record struct DrawingBounds3(Vector3 Min, Vector3 Max)
{
    public static DrawingBounds3 Empty => new(
        new Vector3(float.PositiveInfinity),
        new Vector3(float.NegativeInfinity));

    public bool IsEmpty =>
        Min.X > Max.X || Min.Y > Max.Y || Min.Z > Max.Z;

    public Vector3 Size => IsEmpty ? Vector3.Zero : Max - Min;
    public Vector3 Center => IsEmpty ? Vector3.Zero : (Min + Max) * 0.5f;

    public DrawingBounds3 Encapsulate(Vector3 p)
    {
        if (IsEmpty)
            return new DrawingBounds3(p, p);
        return new DrawingBounds3(Vector3.Min(Min, p), Vector3.Max(Max, p));
    }

    public DrawingBounds3 Union(DrawingBounds3 other)
    {
        if (IsEmpty) return other;
        if (other.IsEmpty) return this;
        return new DrawingBounds3(Vector3.Min(Min, other.Min), Vector3.Max(Max, other.Max));
    }
}
