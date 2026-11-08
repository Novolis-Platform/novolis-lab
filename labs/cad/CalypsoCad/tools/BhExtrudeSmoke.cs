#:property TargetFramework=net10.0
#:property OutputType=Exe
#:property ImplicitUsings=enable
#:property Nullable=enable
#:property NovolisUseProjectReferences=true
#:project ../CalypsoCad.csproj

// Smoke: square plate with one rectangular opening — must be manifold.
using CalypsoCad.Structure;

var outer = new (float Y, float Up)[]
{
    (-5f, 0f), (5f, 0f), (5f, 10f), (-5f, 10f),
};
var hole = new (float Y, float Up)[]
{
    (-1f, 3f), (-1f, 7f), (1f, 7f), (1f, 3f),
};

try
{
    var mesh = PolygonWithHolesExtrude.ExtrudeAlongZ(
        outer, [hole], zCad: 0f, halfThicknessZ: 0.1f, "smoke-square", out var report);
    Console.WriteLine(
        $"OK tris={report.TriangleCount} verts={report.VertexCount} " +
        $"open={report.OpenBoundaryEdges} nonManifold={report.NonManifoldEdges} " +
        $"vol={report.MeshVolumeM3:F4} expected={report.ExpectedVolumeM3:F4} openings={report.AppliedOpenings}");
    return report.OpenBoundaryEdges == 0 && report.NonManifoldEdges == 0 ? 0 : 2;
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex);
    return 1;
}
