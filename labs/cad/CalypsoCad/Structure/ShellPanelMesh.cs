using System.Numerics;
using Novolis.Math.Geometry;

namespace CalypsoCad.Structure;

/// <summary>
/// Thin shell plate between two knuckle edges at consecutive stations (ruled quad + thickness).
/// Follows the changing hull cross-section — not a mid-bay axis-aligned box.
/// </summary>
internal static class ShellPanelMesh
{
    /// <param name="a0">Knuckle A at fore station (world).</param>
    /// <param name="b0">Knuckle B at fore station (world).</param>
    /// <param name="a1">Knuckle A at aft station (world).</param>
    /// <param name="b1">Knuckle B at aft station (world).</param>
    /// <param name="inward">Unit inward normal in the section plane (approx).</param>
    /// <param name="thickness">Plate thickness along inward.</param>
    public static EditableMesh Build(
        Vector3 a0, Vector3 b0, Vector3 a1, Vector3 b1, Vector3 inward, float thickness)
    {
        var n = inward.LengthSquared() > 1e-8f ? Vector3.Normalize(inward) : Vector3.UnitX;
        var t = MathF.Max(0.02f, thickness);
        // Outer face on knuckles; inner face pulled inboard.
        var o00 = a0;
        var o10 = b0;
        var o01 = a1;
        var o11 = b1;
        var i00 = a0 + n * t;
        var i10 = b0 + n * t;
        var i01 = a1 + n * t;
        var i11 = b1 + n * t;

        var mesh = new EditableMesh();
        var oa = mesh.AddVertex(o00);
        var ob = mesh.AddVertex(o10);
        var oc = mesh.AddVertex(o11);
        var od = mesh.AddVertex(o01);
        var ia = mesh.AddVertex(i00);
        var ib = mesh.AddVertex(i10);
        var ic = mesh.AddVertex(i11);
        var id = mesh.AddVertex(i01);

        // Outer (outward = −n): o00,o01,o11,o10
        Quad(mesh, oa, od, oc, ob);
        // Inner (outward = +n): i00,i10,i11,i01
        Quad(mesh, ia, ib, ic, id);
        // Sides
        Quad(mesh, oa, ob, ib, ia); // fore edge A–B
        Quad(mesh, od, oa, ia, id); // knuckle A
        Quad(mesh, ob, oc, ic, ib); // knuckle B
        Quad(mesh, oc, od, id, ic); // aft edge

        return mesh;
    }

    private static void Quad(EditableMesh mesh, int a, int b, int c, int d)
    {
        mesh.AddTriangle(a, b, c);
        mesh.AddTriangle(a, c, d);
    }
}
