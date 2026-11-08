using System.Text.Json;
using CalypsoCad.Layout;
using CalypsoCad.Publication;
using Novolis.Cad.Primitives;

namespace CalypsoCad.Export;

/// <summary>Validates partition attachment coverage vs layout — not appearance.</summary>
internal static class PartitionAttachmentValidator
{
    public sealed record Report(
        int RequiredWallSites,
        int CoveredSites,
        int Posts,
        int Jambs,
        int Headers,
        int PassageViolations,
        IReadOnlyList<string> Missing,
        bool Passed);

    public static Report Evaluate(CadDocument doc, CompartmentLayout? layout)
    {
        var entities = doc.Entities ?? [];
        var posts = entities.Where(e => e.Name?.Contains("-int-post-", StringComparison.OrdinalIgnoreCase) == true).ToList();
        var jambs = entities.Where(e => e.Name?.Contains("-int-jamb-", StringComparison.OrdinalIgnoreCase) == true).ToList();
        var headers = entities.Where(e => e.Name?.Contains("-int-hdr-", StringComparison.OrdinalIgnoreCase) == true).ToList();

        var missing = new List<string>();
        var required = 0;
        var covered = 0;
        var passageHits = 0;

        if (layout is not null)
        {
            // Sample midbody accommodation frames where CREW walls exist.
            foreach (var path in layout.LongitudinalPartitions.Where(p => !p.IsPassageBoundary))
            {
                var zMid = (path.Z0 + path.Z1) * 0.5f;
                if (layout.YInsideAnyPassage(path.Y, zMid))
                    continue;
                required++;
                var hit = posts.Any(p =>
                {
                    if (!TryStationZ(p, out var z) || MathF.Abs(z - zMid) > 2.2f)
                        return false;
                    return MathF.Abs(EntityY(p) - path.Y) < 0.35f;
                });
                if (hit)
                    covered++;
                else
                    missing.Add($"{path.Id} y={path.Y:0.##} near z={zMid:0.##}");
            }

            foreach (var post in posts)
            {
                if (!TryStationZ(post, out var z))
                    continue;
                if (layout.YInsideAnyPassage(EntityY(post), z, margin: 0.15f))
                    passageHits++;
            }
        }

        var passed = posts.Count >= 4
                     && jambs.Count >= 4
                     && headers.Count >= 2
                     && passageHits == 0
                     && (layout is null || covered >= Math.Max(1, required / 3));

        return new Report(
            required, covered, posts.Count, jambs.Count, headers.Count, passageHits, missing, passed);
    }

    public static string WriteJson(CadDocument doc, CompartmentLayout? layout, string directory)
    {
        Directory.CreateDirectory(directory);
        var report = Evaluate(doc, layout);
        var path = Path.Combine(directory, "partition-attachment.json");
        File.WriteAllText(path, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        return path;
    }

    private static bool TryStationZ(CadEntity e, out float z)
    {
        z = 0;
        if (e.Properties is null || !e.Properties.TryGetValue("stationZ", out var el))
            return false;
        if (el.ValueKind != JsonValueKind.Number)
            return false;
        z = (float)el.GetDouble();
        return true;
    }

    private static float EntityY(CadEntity e)
    {
        // World X is starboard (= lock Y). Prefer points[0] center.
        if (e.Points is { Count: > 0 } pts && pts[0] is { Length: > 0 } c)
            return c[0];
        return 0f;
    }
}
