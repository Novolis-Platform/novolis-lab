using System.Numerics;

namespace CalypsoCad.Structure;

/// <summary>Lock/world vector helpers for structure emit (no SVG / viewport coupling).</summary>
internal static class CadVec
{
    public static float[] ToArray(Vector3 v) => [v.X, v.Y, v.Z];

    public static Vector3 FromArray(float[] a) =>
        a.Length >= 3 ? new Vector3(a[0], a[1], a[2]) : Vector3.Zero;
}
