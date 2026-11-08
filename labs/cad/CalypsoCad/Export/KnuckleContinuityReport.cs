using System.Globalization;
using System.Text.Json;
using CalypsoCad.Geometry;
using CalypsoCad.Session;
using CalypsoCad.Structure;
using Novolis.Cad.Primitives;

namespace CalypsoCad.Export;

/// <summary>
/// Per-path continuity: coverage, segment junctions, terminals, loft deviation.
/// A continuous cyan line is not sufficient — gaps and skipped segments are listed.
/// </summary>
internal static class KnuckleContinuityReport
{
    public sealed record PathReport(
        int Vertex,
        string AssemblyId,
        int SegmentCount,
        float ZMin,
        float ZMax,
        float CoverageLoaFraction,
        IReadOnlyList<string> JunctionGaps,
        IReadOnlyList<string> MissingOrInvalid,
        float MaxDeviationM,
        bool Passed);

    public sealed record Report(
        float LoaM,
        int PathCount,
        int PassedPaths,
        IReadOnlyList<PathReport> Paths,
        bool Passed);

    public static Report Evaluate(CadDocument doc, HullGeometry hull, float skinInset, float loa = 69f)
    {
        ArgumentNullException.ThrowIfNull(doc);
        ArgumentNullException.ThrowIfNull(hull);

        var deviation = HullKnucklePathValidation.Evaluate(doc, hull, skinInset);
        var byVertex = new Dictionary<int, List<(string Name, float Z0, float Z1, float Dev, bool Ok)>>();

        foreach (var e in doc.Entities ?? [])
        {
            if (e.Name is null
                || !e.Name.StartsWith("stringer-knuckle-v", StringComparison.OrdinalIgnoreCase)
                || e.Properties is null
                || !e.Properties.TryGetValue("platingJoinVertex", out var vEl)
                || !e.Properties.TryGetValue("z0", out var z0El)
                || !e.Properties.TryGetValue("z1", out var z1El))
                continue;

            var v = vEl.GetInt32();
            var z0 = z0El.GetSingle();
            var z1 = z1El.GetSingle();
            var seg = deviation.Segments.FirstOrDefault(s =>
                string.Equals(s.PartId, e.Name, StringComparison.OrdinalIgnoreCase));
            if (!byVertex.TryGetValue(v, out var list))
            {
                list = [];
                byVertex[v] = list;
            }

            list.Add((e.Name, z0, z1, seg?.DeviationM ?? float.NaN, seg?.Passed ?? false));
        }

        var paths = new List<PathReport>();
        for (var v = 0; v < HullKnucklePath.KnuckleCount; v++)
        {
            byVertex.TryGetValue(v, out var segs);
            segs ??= [];
            segs.Sort((a, b) => a.Z0.CompareTo(b.Z0));

            var gaps = new List<string>();
            var invalid = new List<string>();
            for (var i = 0; i < segs.Count; i++)
            {
                if (!segs[i].Ok)
                    invalid.Add($"{segs[i].Name}: deviation={segs[i].Dev.ToString("0.###", CultureInfo.InvariantCulture)}m");
                if (i == 0)
                    continue;
                var gap = segs[i].Z0 - segs[i - 1].Z1;
                if (gap > 0.55f)
                {
                    gaps.Add(
                        $"junction gap {gap.ToString("0.##", CultureInfo.InvariantCulture)}m between " +
                        $"{segs[i - 1].Name} z1={segs[i - 1].Z1:0.##} and {segs[i].Name} z0={segs[i].Z0:0.##}");
                }
            }

            var zMin = segs.Count == 0 ? 0f : segs.Min(s => s.Z0);
            var zMax = segs.Count == 0 ? 0f : segs.Max(s => s.Z1);
            if (segs.Count == 0)
                invalid.Add("no manufactured segments");
            else
            {
                if (zMin > 1.0f)
                    invalid.Add($"bow terminal missing coverage before z={zMin:0.##}");
                if (loa - zMax > 1.0f)
                    invalid.Add($"stern terminal missing coverage after z={zMax:0.##}");
            }

            var coverage = loa > 1e-3f ? Math.Clamp((zMax - zMin) / loa, 0f, 1f) : 0f;
            var maxDev = segs.Count == 0 ? float.PositiveInfinity : segs.Max(s => float.IsNaN(s.Dev) ? 0f : s.Dev);
            var passed = segs.Count >= 8 && gaps.Count == 0 && invalid.Count == 0 && coverage >= 0.85f;

            paths.Add(new PathReport(
                v,
                $"knuckle-v{v}",
                segs.Count,
                zMin,
                zMax,
                coverage,
                gaps,
                invalid,
                maxDev,
                passed));
        }

        return new Report(loa, paths.Count, paths.Count(p => p.Passed), paths, paths.All(p => p.Passed));
    }

    public static string WriteJson(CadDocument doc, HullGeometry hull, float skinInset, string? directory = null)
    {
        var dir = directory ?? Path.Combine(CalypsoProjectPaths.RendersDirectory, "structure-inspection");
        Directory.CreateDirectory(dir);
        var report = Evaluate(doc, hull, skinInset);
        var path = Path.Combine(dir, "knuckle-continuity.json");
        File.WriteAllText(path, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        return path;
    }
}
