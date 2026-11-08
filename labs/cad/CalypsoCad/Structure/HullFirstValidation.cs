using System.Text;
using Novolis.Cad.Primitives;

namespace CalypsoCad.Structure;

/// <summary>Geometric/topological gates for hull + ordinary frames + heavy bulkheads.</summary>
internal static class HullFirstValidation
{
    public sealed record Check(string Name, bool Ok, string Detail);

    public sealed record Report(
        int FrameAssemblies,
        int BulkheadAssemblies,
        int ExpectedBulkheads,
        int DuplicateFrameAtBh,
        IReadOnlyList<Check> Checks)
    {
        public bool Ok => Checks.All(c => c.Ok);
    }

    public static Report Evaluate(CadDocument doc, float loa = 69f)
    {
        var frames = doc.Entities
            .Where(e => e.Name is not null
                        && e.Name.StartsWith("frame-", StringComparison.OrdinalIgnoreCase)
                        && (e.Kind is "box" or "mesh"))
            .Select(e => e.Name!)
            .ToList();
        var bhs = doc.Entities
            .Where(e => e.Name is not null
                        && e.Name.StartsWith("bh-", StringComparison.OrdinalIgnoreCase)
                        && (e.Kind is "box" or "mesh"))
            .Select(e => e.Name!)
            .ToList();

        var frameAsms = frames
            .Select(AssemblyOf)
            .Where(a => a is not null)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var bhAsms = bhs
            .Select(AssemblyOf)
            .Where(a => a is not null)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var schedule = CalypsoBulkheads.BuildSchedule(loa);
        var missingBh = schedule
            .Where(s => !bhAsms.Any(a => a!.Contains(s.Id, StringComparison.OrdinalIgnoreCase)))
            .Select(s => s.Id)
            .ToList();

        static bool HasAnnulus(IEnumerable<string> names, string asm) =>
            names.Any(n => n.StartsWith(asm + "-", StringComparison.OrdinalIgnoreCase)
                           && n.Contains("annulus", StringComparison.OrdinalIgnoreCase));

        // Hull-conforming annular frames must not sit on heavy-BH stations.
        var dup = 0;
        foreach (var asm in frameAsms)
        {
            if (asm is null || !asm.StartsWith("frame-F", StringComparison.OrdinalIgnoreCase))
                continue;
            if (!HasAnnulus(frames, asm))
                continue;
            var num = asm["frame-F".Length..];
            if (!float.TryParse(num, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var zStem))
                continue;
            if (CalypsoBulkheads.OwnsFrameStation(zStem, schedule))
                dup++;
        }

        // Count assemblies that own a connected annular plate-web (not bar stubs).
        var ringedFrames = frameAsms
            .Where(a => a is not null && HasAnnulus(frames, a!))
            .ToList();
        var ringsMissing = frameAsms.Count(a => a is not null && a.StartsWith("frame-F", StringComparison.OrdinalIgnoreCase))
                           - ringedFrames.Count;

        var checks = new List<Check>
        {
            new("hull meshes present",
                doc.Entities.Any(e => e.Kind == "mesh" && (e.Name?.Contains("oml", StringComparison.OrdinalIgnoreCase) == true
                                                           || e.Name?.Contains("hull", StringComparison.OrdinalIgnoreCase) == true)),
                "oml/iml"),
            new("ordinary frame assemblies", ringedFrames.Count >= 10, $"annular={ringedFrames.Count}"),
            new("heavy BH assemblies match schedule", missingBh.Count == 0,
                missingBh.Count == 0 ? $"bh={bhAsms.Count}" : $"missing={string.Join(',', missingBh)}"),
            new("no duplicate ordinary frame at BH station", dup == 0, $"dups={dup}"),
            new("BH stations have structural section",
                schedule.All(s => HullSectionAuthority.HasStructuralSection(s.ZStem)),
                $"scheduled={schedule.Count}"),
            new("frames have hull-conforming annulus", ringsMissing == 0,
                $"assembliesWithoutAnnulus={ringsMissing}"),
        };

        return new Report(frameAsms.Count, bhAsms.Count, schedule.Count, dup, checks);
    }

    public static string WriteMarkdown(Report report, string path)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Hull-first transverse skeleton validation");
        sb.AppendLine();
        sb.AppendLine($"Overall: **{(report.Ok ? "PASS" : "FAIL")}**");
        sb.AppendLine();
        sb.AppendLine(
            $"| Frame asms | BH asms | Expected BH | Dup frame@BH |");
        sb.AppendLine("| --- | --- | --- | --- |");
        sb.AppendLine(
            $"| {report.FrameAssemblies} | {report.BulkheadAssemblies} | {report.ExpectedBulkheads} | {report.DuplicateFrameAtBh} |");
        sb.AppendLine();
        sb.AppendLine("| Check | Result | Detail |");
        sb.AppendLine("| --- | --- | --- |");
        foreach (var c in report.Checks)
            sb.AppendLine($"| {c.Name} | {(c.Ok ? "PASS" : "FAIL")} | {c.Detail} |");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, sb.ToString());
        return path;
    }

    private static string? AssemblyOf(string name)
    {
        // frame-F12-ring-0 → frame-F12 ; bh-ENG-AFT-web-0 → bh-ENG-AFT
        if (name.StartsWith("frame-", StringComparison.OrdinalIgnoreCase))
        {
            var parts = name.Split('-');
            return parts.Length >= 2 ? $"frame-{parts[1]}" : name;
        }

        if (name.StartsWith("bh-", StringComparison.OrdinalIgnoreCase))
        {
            var plate = name.IndexOf("-plate", StringComparison.OrdinalIgnoreCase);
            if (plate > 0)
                return name[..plate];
            var parts = name.Split('-');
            return parts.Length >= 3 ? $"bh-{parts[1]}-{parts[2]}" : name;
        }

        return null;
    }
}
