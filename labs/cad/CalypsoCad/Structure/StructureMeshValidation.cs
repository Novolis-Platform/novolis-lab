using System.Numerics;
using System.Text.Json;
using Novolis.Math.Geometry;

namespace CalypsoCad.Structure;

/// <summary>
/// Geometric gate for structural mesh entities before they enter the CAD document.
/// </summary>
internal static class StructureMeshValidation
{
    public sealed record Result(
        string PartId,
        float StationZStem,
        int ExpectedOpenings,
        int GeneratedOpenings,
        float MaterialAreaM2,
        float MeshVolumeM3,
        float ExpectedVolumeM3,
        int OpenBoundaryEdges,
        int NonManifoldEdges,
        int DegenerateTriangles,
        int TriangleCount,
        int VertexCount,
        bool Passed,
        IReadOnlyList<string> Failures,
        IReadOnlyList<string> Checks);

    public static Result Validate(
        string partId,
        float stationZStem,
        EditableMesh mesh,
        PolygonWithHolesExtrude.ExtrudeReport? extrude = null,
        int? expectedOpenings = null)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        var failures = new List<string>();
        var checks = new List<string>();

        if (mesh.VertexCount < 8)
            failures.Add($"vertexCount={mesh.VertexCount} (closed plate solid needs ≥8)");
        if (mesh.TriangleCount < 4)
            failures.Add($"triangleCount={mesh.TriangleCount}");

        var edgeCounts = new Dictionary<(int, int), int>();
        var degenerates = 0;
        for (var i = 0; i < mesh.Indices.Count; i += 3)
        {
            var ia = mesh.Indices[i];
            var ib = mesh.Indices[i + 1];
            var ic = mesh.Indices[i + 2];
            if (ia == ib || ib == ic || ic == ia)
            {
                degenerates++;
                continue;
            }

            var a = mesh.Vertices[ia];
            var b = mesh.Vertices[ib];
            var c = mesh.Vertices[ic];
            if (Vector3.Cross(b - a, c - a).LengthSquared() < 1e-16f)
                degenerates++;

            AddEdge(edgeCounts, ia, ib);
            AddEdge(edgeCounts, ib, ic);
            AddEdge(edgeCounts, ic, ia);
        }

        var openEdges = edgeCounts.Count(kv => kv.Value == 1);
        var nonManifold = edgeCounts.Count(kv => kv.Value > 2);
        if (openEdges != 0)
            failures.Add($"openBoundaryEdges={openEdges}");
        else
            checks.Add("closed manifold: no open boundary edges");

        if (nonManifold != 0)
            failures.Add($"nonManifoldEdges={nonManifold}");
        else
            checks.Add("manifold: no edge with >2 faces");

        if (degenerates != 0)
            failures.Add($"degenerateTriangles={degenerates}");
        else
            checks.Add("no degenerate triangles");

        var meshVolume = MathF.Abs(SignedVolume(mesh));
        var materialArea = extrude?.MaterialAreaM2 ?? 0f;
        var expectedVol = extrude?.ExpectedVolumeM3 ?? 0f;
        var genOpenings = extrude?.AppliedOpenings ?? expectedOpenings ?? -1;
        var expOpenings = expectedOpenings ?? extrude?.ExpectedOpenings ?? -1;

        if (expOpenings >= 0 && genOpenings >= 0 && expOpenings != genOpenings)
            failures.Add($"openings generated={genOpenings} expected={expOpenings}");

        if (expectedVol > 1e-6f)
        {
            var rel = MathF.Abs(meshVolume - expectedVol) / expectedVol;
            if (rel > 0.10f)
                failures.Add($"volume {meshVolume:F5} vs expected {expectedVol:F5} (rel {rel:P1})");
            else
                checks.Add($"volume {meshVolume:F5} ≈ {expectedVol:F5} m³");
        }

        if (extrude is not null)
            checks.AddRange(extrude.Checks);

        return new Result(
            partId,
            stationZStem,
            expOpenings,
            genOpenings,
            materialArea,
            meshVolume,
            expectedVol,
            openEdges,
            nonManifold,
            degenerates,
            mesh.TriangleCount,
            mesh.VertexCount,
            failures.Count == 0,
            failures,
            checks);
    }

    public static void WriteJsonReport(string path, IEnumerable<Result> results)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var payload = results.Select(r => new
        {
            partId = r.PartId,
            stationZStem = r.StationZStem,
            expectedOpenings = r.ExpectedOpenings,
            generatedOpenings = r.GeneratedOpenings,
            materialAreaM2 = r.MaterialAreaM2,
            meshVolumeM3 = r.MeshVolumeM3,
            expectedVolumeM3 = r.ExpectedVolumeM3,
            openBoundaryEdges = r.OpenBoundaryEdges,
            nonManifoldEdges = r.NonManifoldEdges,
            degenerateTriangles = r.DegenerateTriangles,
            triangleCount = r.TriangleCount,
            vertexCount = r.VertexCount,
            passed = r.Passed,
            failures = r.Failures,
            checks = r.Checks,
        });
        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }

    private static void AddEdge(Dictionary<(int, int), int> counts, int a, int b)
    {
        var key = a < b ? (a, b) : (b, a);
        counts[key] = counts.TryGetValue(key, out var n) ? n + 1 : 1;
    }

    private static float SignedVolume(EditableMesh mesh)
    {
        double acc = 0;
        for (var i = 0; i < mesh.Indices.Count; i += 3)
        {
            var a = mesh.Vertices[mesh.Indices[i]];
            var b = mesh.Vertices[mesh.Indices[i + 1]];
            var c = mesh.Vertices[mesh.Indices[i + 2]];
            acc += Vector3.Dot(a, Vector3.Cross(b, c));
        }

        return (float)(acc / 6.0);
    }
}
