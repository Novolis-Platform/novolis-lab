using System.Numerics;

namespace CalypsoCad.Drawing;

internal static class DrawingMeasurements
{
    public static IReadOnlyList<DrawingMeasurement> ComputeOverall(
        IReadOnlyList<DrawableGeometry> geometry,
        DimensionPolicy policy)
    {
        if (policy.Kind == DimensionPolicyKind.None)
            return [];

        var bounds = DrawingBounds3.Empty;
        foreach (var g in geometry)
            bounds = bounds.Union(g.Bounds);
        if (bounds.IsEmpty)
            return [];

        var list = new List<DrawingMeasurement>
        {
            new("overall-longitudinal", "Overall longitudinal (Z)", bounds.Size.Z, IsTrue3D: false,
                "World Z extent (stem→stern axis)"),
            new("overall-beam", "Overall beam (X)", bounds.Size.X, IsTrue3D: false,
                "World X extent (port↔starboard)"),
            new("overall-height", "Overall height (Y)", bounds.Size.Y, IsTrue3D: false,
                "World Y extent (keel→weather)"),
        };

        var pathLen = SumPolylineTrueLength(geometry);
        if (pathLen > 1e-4f)
            list.Add(new DrawingMeasurement("path-true-length", "True 3D path length", pathLen, IsTrue3D: true,
                "Sum of polyline segment lengths in model space"));

        return list;
    }

    public static float SumPolylineTrueLength(IReadOnlyList<DrawableGeometry> geometry)
    {
        var sum = 0f;
        foreach (var g in geometry)
        {
            if (g.Polylines is null)
                continue;
            foreach (var poly in g.Polylines)
            {
                for (var i = 0; i + 1 < poly.Length; i++)
                    sum += Vector3.Distance(poly[i], poly[i + 1]);
            }

        }

        return sum;
    }
}
