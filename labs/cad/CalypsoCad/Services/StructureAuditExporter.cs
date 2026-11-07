using System.Globalization;
using System.Text;
using System.Text.Json;
using CalypsoCad.Generation;
using Novolis.Cad.Primitives;

namespace CalypsoCad.Services;

/// <summary>
/// LLM-first structure audit: bh-* (and optional frame) centroids vs ship spine.
/// Spine = mid athwart (X=0) × mid height (SectionAt.mid) × stem→stern.
/// </summary>
internal static class StructureAuditExporter
{
    private const float Loa = 69f;
    private const float TolX = 0.05f;
    private const float TolUp = 0.15f;

    public static string AuditDirectory =>
        Path.Combine(CalypsoProjectPaths.RendersDirectory, "audit");

    public static (string JsonPath, string NdjsonPath, int Failures) Write(
        CadDocument doc,
        string? outDir = null)
    {
        outDir ??= AuditDirectory;
        Directory.CreateDirectory(outDir);

        var groups = new Dictionary<string, List<CadEntity>>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in doc.Entities)
        {
            if (e.Kind != "box" || e.Name is null || e.Points is not { Count: >= 2 })
                continue;
            var key = GroupKey(e.Name);
            if (key is null)
                continue;
            if (!groups.TryGetValue(key, out var list))
            {
                list = [];
                groups[key] = list;
            }

            list.Add(e);
        }

        var rows = new List<object>();
        var nd = new StringBuilder();
        var failures = 0;

        foreach (var (key, cells) in groups.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase))
        {
            double sumV = 0, cx = 0, cy = 0, cz = 0;
            float maxHx = 0, maxHy = 0, maxHz = 0;
            foreach (var e in cells)
            {
                var c = SvgCoords.FromArray(e.Points![0]);
                var he = SvgCoords.FromArray(e.Points[1]);
                var hx = MathF.Abs(he.X);
                var hy = MathF.Abs(he.Y);
                var hz = MathF.Abs(he.Z);
                var v = Math.Max(1e-6, 8.0 * hx * hy * hz);
                sumV += v;
                cx += c.X * v;
                cy += c.Y * v;
                cz += c.Z * v;
                maxHx = Math.Max(maxHx, hx);
                maxHy = Math.Max(maxHy, hy);
                maxHz = Math.Max(maxHz, hz);
            }

            cx /= sumV;
            cy /= sumV;
            cz /= sumV;
            var zStem = Loa * 0.5f - (float)cz;
            // SectionAt mid = OAH/2 spine (set during generate). For audit without regen, use 6.
            CalypsoLockGenerator.SectionAtPublic(zStem, out _, out _, out var sectionMid, out _, out _);
            if (sectionMid < 1f)
                sectionMid = 6f;
            var absX = Math.Abs(cx);
            var deltaUp = cy - sectionMid;
            var passX = absX <= TolX;
            var passUp = Math.Abs(deltaUp) <= TolUp;
            var pass = passX && passUp;
            if (!pass)
                failures++;

            rows.Add(new
            {
                id = key,
                cellCount = cells.Count,
                centroidX = Round(cx),
                centroidY = Round(cy),
                centroidZ = Round(cz),
                zStem = Round(zStem),
                sectionMidUp = Round(sectionMid),
                absX = Round(absX),
                deltaUp = Round(deltaUp),
                halfExtentsMax = new { x = Round(maxHx), y = Round(maxHy), z = Round(maxHz) },
                passX,
                passUp,
                pass,
                tolX = TolX,
                tolUp = TolUp,
            });

            AppendNdjson(nd, "bh-spine-x", passX, absX, TolX, key);
            AppendNdjson(nd, "bh-spine-up", passUp, Math.Abs(deltaUp), TolUp, key);
        }

        var payload = new
        {
            generatedUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
            loa = Loa,
            spine = "mid athwart (X=0) × mid height (SectionAt.mid) × stem→stern",
            tolX = TolX,
            tolUp = TolUp,
            failureCount = failures,
            groups = rows,
        };

        var jsonPath = Path.Combine(outDir, "structure-audit.json");
        var ndPath = Path.Combine(outDir, "structure-audit.ndjson");
        File.WriteAllText(jsonPath, JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));
        File.WriteAllText(ndPath, nd.ToString());
        return (jsonPath, ndPath, failures);
    }

    private static string? GroupKey(string name)
    {
        if (name.StartsWith("bh-", StringComparison.OrdinalIgnoreCase))
        {
            // bh-ENG-FORE-web-solid-0 → bh-ENG-FORE
            var rest = name["bh-".Length..];
            var cut = rest.IndexOf("-web-", StringComparison.OrdinalIgnoreCase);
            if (cut < 0)
                cut = rest.IndexOf("-webplate-", StringComparison.OrdinalIgnoreCase);
            return cut > 0 ? "bh-" + rest[..cut] : "bh-" + rest;
        }

        return null;
    }

    private static void AppendNdjson(StringBuilder nd, string gate, bool pass, double measured, double tol, string id)
    {
        nd.Append('{')
            .Append("\"gate\":\"").Append(gate).Append("\",")
            .Append("\"id\":\"").Append(id).Append("\",")
            .Append("\"pass\":").Append(pass ? "true" : "false").Append(',')
            .Append("\"measured\":").Append(measured.ToString("0.###", CultureInfo.InvariantCulture)).Append(',')
            .Append("\"tol\":").Append(tol.ToString("0.###", CultureInfo.InvariantCulture)).Append(',')
            .Append("\"skill\":\"calypso-structure-verify\"")
            .Append('}')
            .Append('\n');
    }

    private static double Round(double v) => Math.Round(v, 4);
}
