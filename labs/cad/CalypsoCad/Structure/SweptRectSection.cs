using System.Numerics;
using Novolis.Math.Geometry;

namespace CalypsoCad.Structure;

/// <summary>
/// Builds a watertight rectangular prism swept along an arbitrary 3D segment (world CAD space).
/// Used for hull-knuckle stringer segments that follow changing cross-section diagonals.
/// </summary>
internal static class SweptRectSection
{
    /// <summary>
    /// Prism from <paramref name="a"/> to <paramref name="b"/> with rectangular section
    /// <paramref name="halfWidth"/> × <paramref name="halfHeight"/> in the plane perpendicular to the axis.
    /// </summary>
    public static EditableMesh Build(Vector3 a, Vector3 b, float halfWidth, float halfHeight)
    {
        var axis = b - a;
        var len = axis.Length();
        if (len < 1e-4f)
            throw new ArgumentException("Segment length too small for swept section.");

        axis /= len;
        var (u, v) = OrthonormalFrame(axis);
        var hw = MathF.Max(0.02f, halfWidth);
        var hh = MathF.Max(0.02f, halfHeight);

        // Cross-section corners at A then B (consistent winding looking along +axis).
        var a0 = a - u * hw - v * hh;
        var a1 = a + u * hw - v * hh;
        var a2 = a + u * hw + v * hh;
        var a3 = a - u * hw + v * hh;
        var b0 = b - u * hw - v * hh;
        var b1 = b + u * hw - v * hh;
        var b2 = b + u * hw + v * hh;
        var b3 = b - u * hw + v * hh;

        var mesh = new EditableMesh();
        var i0 = mesh.AddVertex(a0);
        var i1 = mesh.AddVertex(a1);
        var i2 = mesh.AddVertex(a2);
        var i3 = mesh.AddVertex(a3);
        var i4 = mesh.AddVertex(b0);
        var i5 = mesh.AddVertex(b1);
        var i6 = mesh.AddVertex(b2);
        var i7 = mesh.AddVertex(b3);

        // Cap at A (outward −axis): a0,a3,a2,a1
        Quad(mesh, i0, i3, i2, i1);
        // Cap at B (outward +axis): b0,b1,b2,b3
        Quad(mesh, i4, i5, i6, i7);
        // Sides
        Quad(mesh, i0, i1, i5, i4); // −v
        Quad(mesh, i1, i2, i6, i5); // +u
        Quad(mesh, i2, i3, i7, i6); // +v
        Quad(mesh, i3, i0, i4, i7); // −u

        return mesh;
    }

    private static void Quad(EditableMesh mesh, int a, int b, int c, int d)
    {
        mesh.AddTriangle(a, b, c);
        mesh.AddTriangle(a, c, d);
    }

    private static (Vector3 U, Vector3 V) OrthonormalFrame(Vector3 axis)
    {
        // Prefer world-up bias so flange orientation stays readable on near-horizontal runs.
        var refUp = Vector3.UnitY;
        var u = Vector3.Cross(axis, refUp);
        if (u.LengthSquared() < 1e-8f)
        {
            refUp = Vector3.UnitX;
            u = Vector3.Cross(axis, refUp);
        }

        u = Vector3.Normalize(u);
        var v = Vector3.Normalize(Vector3.Cross(axis, u));
        return (u, v);
    }
}
