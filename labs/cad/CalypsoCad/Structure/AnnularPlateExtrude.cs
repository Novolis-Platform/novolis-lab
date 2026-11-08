using System.Numerics;
using Novolis.Math.Geometry;

namespace CalypsoCad.Structure;

/// <summary>
/// Extrudes a matching outer/inner polygon pair into a watertight annular plate (plate-web ring).
/// Vertex correspondence is by index — both rings must share topology (e.g. offset octagons).
/// </summary>
internal static class AnnularPlateExtrude
{
    public static EditableMesh ExtrudeAlongZ(
        IReadOnlyList<(float Y, float Up)> outer,
        IReadOnlyList<(float Y, float Up)> inner,
        float zCad,
        float halfThicknessZ,
        string? partId = null)
    {
        ArgumentNullException.ThrowIfNull(outer);
        ArgumentNullException.ThrowIfNull(inner);
        var id = string.IsNullOrWhiteSpace(partId) ? "annulus" : partId;
        if (outer.Count < 3 || inner.Count < 3)
            throw new ArgumentException($"{id}: outer and inner need ≥3 vertices.");
        if (outer.Count != inner.Count)
        {
            throw new ArgumentException(
                $"{id}: outer/inner vertex counts differ ({outer.Count} vs {inner.Count}). " +
                "Annular extrusion requires topological correspondence.");
        }

        if (halfThicknessZ <= 1e-5f)
            throw new ArgumentOutOfRangeException(nameof(halfThicknessZ));

        var n = outer.Count;
        var z0 = zCad - halfThicknessZ;
        var z1 = zCad + halfThicknessZ;
        var mesh = new EditableMesh();

        var oFront = new int[n];
        var oRear = new int[n];
        var iFront = new int[n];
        var iRear = new int[n];
        for (var i = 0; i < n; i++)
        {
            var (oy, ou) = outer[i];
            var (iy, iu) = inner[i];
            oFront[i] = mesh.AddVertex(new Vector3(oy, ou, z0));
            oRear[i] = mesh.AddVertex(new Vector3(oy, ou, z1));
            iFront[i] = mesh.AddVertex(new Vector3(iy, iu, z0));
            iRear[i] = mesh.AddVertex(new Vector3(iy, iu, z1));
        }

        for (var i = 0; i < n; i++)
        {
            var j = (i + 1) % n;
            // Front face (outward −Z): outer→inner quad, CW when viewed from +Z.
            mesh.AddTriangle(oFront[i], iFront[i], iFront[j]);
            mesh.AddTriangle(oFront[i], iFront[j], oFront[j]);
            // Rear face (outward +Z).
            mesh.AddTriangle(oRear[i], oRear[j], iRear[j]);
            mesh.AddTriangle(oRear[i], iRear[j], iRear[i]);
            // Outer perimeter wall.
            mesh.AddTriangle(oFront[i], oFront[j], oRear[j]);
            mesh.AddTriangle(oFront[i], oRear[j], oRear[i]);
            // Inner opening wall (inward).
            mesh.AddTriangle(iFront[i], iRear[j], iFront[j]);
            mesh.AddTriangle(iFront[i], iRear[i], iRear[j]);
        }

        mesh = MeshWeld.Apply(mesh, new WeldOptions(1e-4f, WeldPositionMode.Average, WeldScope.EntireMesh));
        var open = mesh.FindBoundaryEdges().Count;
        if (open != 0)
        {
            throw new InvalidOperationException(
                $"{id}: annular plate not closed after weld (openBoundaryEdges={open}).");
        }

        return mesh;
    }
}
