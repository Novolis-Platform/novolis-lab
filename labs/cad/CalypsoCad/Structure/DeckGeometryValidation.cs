using System.Text;
using System.Text.Json;
using Novolis.Cad.Primitives;

namespace CalypsoCad.Structure;

/// <summary>
/// Machine-readable geometric checks for cellular sandwich decks (not entity-count vanity).
/// </summary>
internal static class DeckGeometryValidation
{
    public sealed record Check(string Name, bool Ok, string Detail);

    public sealed record Report(
        int FaceTops,
        int FaceBots,
        int Cores,
        int PlacementFailures,
        float MaxHalfBeamOverrunM,
        int OutsideHullCount,
        int HoldIntrusions,
        int EngFullWidthIntrusions,
        IReadOnlyList<Check> Checks,
        IReadOnlyList<string> FailureIds)
    {
        public bool Ok => Checks.All(c => c.Ok);
    }

    public static Report Evaluate(CadDocument doc, float loa = 69f)
    {
        var faces = doc.Entities
            .Where(e => e.Kind == "box" && e.Name is not null
                        && e.Name.StartsWith("deck-sandwich-", StringComparison.OrdinalIgnoreCase)
                        && e.Name.Contains("-face-top", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var bots = doc.Entities
            .Where(e => e.Kind == "box" && e.Name is not null
                        && e.Name.StartsWith("deck-sandwich-", StringComparison.OrdinalIgnoreCase)
                        && e.Name.Contains("-face-bot", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var cores = doc.Entities
            .Where(e => e.Kind == "box" && e.Name is not null
                        && e.Name.StartsWith("deck-sandwich-", StringComparison.OrdinalIgnoreCase)
                        && (e.Name.Contains("-rib-", StringComparison.OrdinalIgnoreCase)
                            || e.Name.Contains("-peri-", StringComparison.OrdinalIgnoreCase)
                            || string.Equals(DeckSurface(e), "core", StringComparison.OrdinalIgnoreCase)))
            .ToList();

        var outside = 0;
        var maxOver = 0f;
        var holdIntrusions = 0;
        var engFull = 0;
        var failIds = new List<string>();

        foreach (var face in faces)
        {
            if (!TryBoxExtents(face, out var cy, out var cup, out var cz, out var hy, out _, out _))
                continue;
            var zStem = loa * 0.5f - cz;
            var allowed = HullDeckGeometry.HalfBeamAtDeck(zStem, cup + 0.02f);
            var overrun = hy - allowed;
            if (overrun > 0.08f)
            {
                outside++;
                maxOver = MathF.Max(maxOver, overrun);
                failIds.Add($"{face.Name} overrun={overrun:0.###}m @zStem={zStem:0.##}");
            }

            // Intermediate full-width decks must not occupy HOLD (49.25–69).
            if (zStem > 49.3f && zStem < 68.5f && hy > 4.5f && MathF.Abs(cy) < 1.5f && cup > 1.2f)
            {
                holdIntrusions++;
                failIds.Add($"{face.Name} HOLD full-width intrusion zStem={zStem:0.##}");
            }

            // ENG atrium (41–49.25): corridor strips only — reject CL-centered wide panels.
            if (zStem > 41.05f && zStem < 49.2f && hy > 4.5f && MathF.Abs(cy) < 1.5f && cup > 1.2f)
            {
                engFull++;
                failIds.Add($"{face.Name} ENG full-width intrusion zStem={zStem:0.##}");
            }
        }

        var placement = StructurePlacementLog.Count;
        var checks = new List<Check>
        {
            new("deck face-tops generated", faces.Count >= 40, $"faceTops={faces.Count}"),
            new("deck face-bots generated", bots.Count >= 40, $"faceBots={bots.Count}"),
            new("deck cellular cores generated", cores.Count >= 80, $"cores={cores.Count}"),
            new("face top/bot parity", Math.Abs(faces.Count - bots.Count) <= 4,
                $"top={faces.Count} bot={bots.Count}"),
            new("placement failures recorded=0", placement == 0, $"failures={placement}"),
            new("no deck half-beam past IML", outside == 0,
                $"outside={outside} maxOverrunM={maxOver:0.###}"),
            new("no intermediate full deck in HOLD", holdIntrusions == 0, $"count={holdIntrusions}"),
            new("no intermediate full deck in ENG atrium", engFull == 0, $"count={engFull}"),
        };

        return new Report(
            faces.Count, bots.Count, cores.Count, placement, maxOver,
            outside, holdIntrusions, engFull, checks, failIds);
    }

    public static string WriteReportMarkdown(Report report, string path)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Deck geometry validation");
        sb.AppendLine();
        sb.AppendLine($"Overall: **{(report.Ok ? "PASS" : "FAIL")}**");
        sb.AppendLine();
        sb.AppendLine("## Counts");
        sb.AppendLine();
        sb.AppendLine("| Face tops | Face bots | Cores | Placement failures |");
        sb.AppendLine("| --- | --- | --- | --- |");
        sb.AppendLine(
            $"| {report.FaceTops} | {report.FaceBots} | {report.Cores} | {report.PlacementFailures} |");
        sb.AppendLine();
        sb.AppendLine("## Checks");
        sb.AppendLine();
        sb.AppendLine("| Check | Result | Detail |");
        sb.AppendLine("| --- | --- | --- |");
        foreach (var c in report.Checks)
            sb.AppendLine($"| {c.Name} | {(c.Ok ? "PASS" : "FAIL")} | {c.Detail} |");
        sb.AppendLine();
        if (report.FailureIds.Count > 0)
        {
            sb.AppendLine("## Failures");
            sb.AppendLine();
            foreach (var id in report.FailureIds.Take(80))
                sb.AppendLine($"- `{id}`");
            if (report.FailureIds.Count > 80)
                sb.AppendLine($"- … +{report.FailureIds.Count - 80} more");
            sb.AppendLine();
        }

        if (StructurePlacementLog.Count > 0)
        {
            sb.AppendLine("## Placement omissions");
            sb.AppendLine();
            foreach (var f in StructurePlacementLog.All.Take(60))
                sb.AppendLine(
                    $"- `{f.ComponentId}` @ Y={f.Y:0.##} Up={f.Up:0.##} Zstem={f.ZStem:0.##} — {f.Reason}");
            sb.AppendLine();
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, sb.ToString());
        return path;
    }

    private static string? DeckSurface(CadEntity e)
    {
        if (e.Properties is null || !e.Properties.TryGetValue("deckSurface", out var s))
            return null;
        return s.ValueKind == JsonValueKind.String ? s.GetString() : null;
    }

    private static bool TryBoxExtents(
        CadEntity box,
        out float cy, out float cup, out float cz,
        out float hy, out float hu, out float hz)
    {
        cy = cup = cz = hy = hu = hz = 0f;
        if (box.Points is null || box.Points.Count < 2)
            return false;
        var a = box.Points[0];
        var b = box.Points[1];
        if (a is null || b is null || a.Length < 3 || b.Length < 3)
            return false;
        var x0 = (float)a[0];
        var y0 = (float)a[1];
        var z0 = (float)a[2];
        var x1 = (float)b[0];
        var y1 = (float)b[1];
        var z1 = (float)b[2];
        // CAD: X stbd, Y up, Z bow↔aft about midship.
        cy = (x0 + x1) * 0.5f;
        cup = (y0 + y1) * 0.5f;
        cz = (z0 + z1) * 0.5f;
        hy = MathF.Abs(x1 - x0) * 0.5f;
        hu = MathF.Abs(y1 - y0) * 0.5f;
        hz = MathF.Abs(z1 - z0) * 0.5f;
        return true;
    }
}
