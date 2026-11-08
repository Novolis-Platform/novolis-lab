using System.Numerics;
using Novolis.Math.Geometry;

namespace CalypsoCad.Structure;

/// <summary>
/// Extrudes a simple outer polygon (no holes) into a watertight solid plate.
/// Interim BH path when polygon-with-holes extrusion fails; fab profile still carries openings.
/// </summary>
internal static class SolidPlateExtrude
{
    public static EditableMesh ExtrudeAlongZ(
        IReadOnlyList<(float Y, float Up)> outer,
        float zCad,
        float halfThicknessZ,
        string? partId = null)
    {
        ArgumentNullException.ThrowIfNull(outer);
        var id = string.IsNullOrWhiteSpace(partId) ? "plate" : partId;
        if (outer.Count < 3)
            throw new ArgumentException($"{id}: outer needs ≥3 vertices.");
        if (halfThicknessZ <= 1e-5f)
            throw new ArgumentOutOfRangeException(nameof(halfThicknessZ));

        var pts = outer.ToList();
        if (SignedArea(pts) < 0f)
            pts.Reverse();

        var tris = new List<(int A, int B, int C)>(pts.Count - 2);
        for (var i = 1; i < pts.Count - 1; i++)
            tris.Add((0, i, i + 1));

        var z0 = zCad - halfThicknessZ;
        var z1 = zCad + halfThicknessZ;
        var mesh = new EditableMesh();
        var front = new int[pts.Count];
        var rear = new int[pts.Count];
        for (var i = 0; i < pts.Count; i++)
        {
            var (y, up) = pts[i];
            front[i] = mesh.AddVertex(new Vector3(y, up, z0));
            rear[i] = mesh.AddVertex(new Vector3(y, up, z1));
        }

        foreach (var (a, b, c) in tris)
        {
            mesh.AddTriangle(front[a], front[c], front[b]);
            mesh.AddTriangle(rear[a], rear[b], rear[c]);
        }

        for (var i = 0; i < pts.Count; i++)
        {
            var j = (i + 1) % pts.Count;
            mesh.AddTriangle(front[i], front[j], rear[j]);
            mesh.AddTriangle(front[i], rear[j], rear[i]);
        }

        mesh = MeshWeld.Apply(mesh, new WeldOptions(1e-4f, WeldPositionMode.Average, WeldScope.EntireMesh));
        var open = mesh.FindBoundaryEdges().Count;
        if (open != 0)
            throw new InvalidOperationException($"{id}: solid plate not closed (openBoundaryEdges={open}).");
        return mesh;
    }

    private static float SignedArea(IReadOnlyList<(float Y, float Up)> pts)
    {
        double a = 0;
        for (var i = 0; i < pts.Count; i++)
        {
            var p = pts[i];
            var q = pts[(i + 1) % pts.Count];
            a += (double)p.Y * q.Up - (double)q.Y * p.Up;
        }

        return (float)(0.5 * a);
    }
}
