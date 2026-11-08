using System.Globalization;
using System.Text.Json;
using CalypsoCad.Geometry;
using Novolis.Cad.Primitives;

namespace CalypsoCad.Structure;

/// <summary>
/// Independent geometric check: each manufactured knuckle-stringer segment midpoint
/// must lie near the loft knuckle at that stem Z (not the emit-time self-check).
/// </summary>
internal static class HullKnucklePathValidation
{
    public const float MaxDeviationM = 0.35f;

    public sealed record SegmentCheck(
        string PartId,
        int Vertex,
        float Z0,
        float Z1,
        float MidZ,
        float DeviationM,
        bool Passed);

    public sealed record Report(
        int PathCount,
        int SegmentCount,
        int Failures,
        float MaxDeviationM,
        IReadOnlyList<SegmentCheck> Segments,
        bool Passed);

    public static Report Evaluate(CadDocument doc, HullGeometry hull, float skinInset)
    {
        ArgumentNullException.ThrowIfNull(doc);
        ArgumentNullException.ThrowIfNull(hull);

        var checks = new List<SegmentCheck>();
        foreach (var e in doc.Entities ?? [])
        {
            if (e.Name is null
                || !e.Name.StartsWith("stringer-knuckle-v", StringComparison.OrdinalIgnoreCase)
                || e.Properties is null
                || !e.Properties.TryGetValue("platingJoinVertex", out var vEl)
                || !e.Properties.TryGetValue("z0", out var z0El)
                || !e.Properties.TryGetValue("z1", out var z1El)
                || e.Points is not { Count: >= 1 })
                continue;

            var vertex = vEl.GetInt32();
            var z0 = z0El.GetSingle();
            var z1 = z1El.GetSingle();
            var midZ = (z0 + z1) * 0.5f;
            var ring = hull.InnerOctagonRing(midZ, skinInset);
            if (vertex < 0 || vertex >= ring.Length)
            {
                checks.Add(new SegmentCheck(e.Name, vertex, z0, z1, midZ, float.PositiveInfinity, false));
                continue;
            }

            var expect = ring[vertex];
            var (cy, cu) = ring.GetSectionCenter();
            // Emit pulls inward by ~section half — compare along the ray, allow that pull.
            var c = e.Points[0];
            var dx = c[0] - expect.Y;
            var du = c[1] - expect.Up;
            var dist = MathF.Sqrt(dx * dx + du * du);
            // Prefer distance to the knuckle after projecting onto inward ray from knuckle.
            var toInY = cy - expect.Y;
            var toInU = cu - expect.Up;
            var inLen = MathF.Sqrt(toInY * toInY + toInU * toInU);
            float deviation;
            if (inLen > 1e-4f)
            {
                var along = (dx * toInY + du * toInU) / inLen;
                var perpY = dx - toInY / inLen * along;
                var perpU = du - toInU / inLen * along;
                var perp = MathF.Sqrt(perpY * perpY + perpU * perpU);
                // Along-ray pull is expected (0.05–0.25 m); only lateral miss is structural error.
                deviation = perp;
                if (along < -0.05f || along > 0.55f)
                    deviation = MathF.Max(deviation, MathF.Abs(along) * 0.5f);
            }
            else
            {
                deviation = dist;
            }

            checks.Add(new SegmentCheck(
                e.Name, vertex, z0, z1, midZ, deviation, deviation <= MaxDeviationM));
        }

        var fails = checks.Count(c => !c.Passed);
        var maxDev = checks.Count == 0 ? 0f : checks.Max(c => c.DeviationM);
        var paths = checks.Select(c => c.Vertex).Distinct().Count();
        return new Report(paths, checks.Count, fails, maxDev, checks, fails == 0 && paths == 8 && checks.Count >= 40);
    }

    public static string WriteJson(string path, Report report)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var payload = new
        {
            pathCount = report.PathCount,
            segmentCount = report.SegmentCount,
            failures = report.Failures,
            maxDeviationM = report.MaxDeviationM,
            maxAllowedM = MaxDeviationM,
            passed = report.Passed,
            method = "Independent loft re-sample at segment mid-Z; lateral deviation from knuckle inward ray",
            segments = report.Segments.Select(s => new
            {
                partId = s.PartId,
                vertex = s.Vertex,
                z0 = s.Z0,
                z1 = s.Z1,
                midZ = s.MidZ,
                deviationM = s.DeviationM,
                passed = s.Passed,
            }),
        };
        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
        return path;
    }

    public static string FormatSummary(Report report) =>
        $"paths={report.PathCount} segs={report.SegmentCount} fails={report.Failures} maxDev={report.MaxDeviationM.ToString("0.###", CultureInfo.InvariantCulture)}m";
}
