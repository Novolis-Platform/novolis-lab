using System.Text;
using System.Text.Json;
using Novolis.Cad.Primitives;

namespace CalypsoCad.Structure;

/// <summary>
/// Diagnose deck-sandwich solids from the CAD document (not screenshots).
/// </summary>
internal static class DeckCadDiagnostics
{
    public sealed record DeckCounts(
        string DeckTag,
        int FaceTops,
        int FaceBots,
        int RibLong,
        int RibAth,
        int Peri,
        int Other);

    public sealed record Report(
        IReadOnlyList<DeckCounts> ByDeck,
        int DuplicateNameCount,
        int OverlappingFacePairs,
        int RibsPastPeriEstimate,
        int PlacementFailures,
        IReadOnlyList<string> Notes)
    {
        public string WriteMarkdown(string path)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Deck CAD diagnostics");
            sb.AppendLine();
            sb.AppendLine("## Per-deck component counts");
            sb.AppendLine();
            sb.AppendLine("| Deck | Face tops | Face bots | Rib long | Rib ath | Peri | Other |");
            sb.AppendLine("| --- | --- | --- | --- | --- | --- | --- |");
            foreach (var d in ByDeck)
            {
                sb.AppendLine(
                    $"| `{d.DeckTag}` | {d.FaceTops} | {d.FaceBots} | {d.RibLong} | {d.RibAth} | {d.Peri} | {d.Other} |");
            }

            sb.AppendLine();
            sb.AppendLine("## Overlap / duplication");
            sb.AppendLine();
            sb.AppendLine($"| Duplicate names | Coplanar face overlaps | Ribs past peri (est.) | Placement failures |");
            sb.AppendLine("| --- | --- | --- | --- |");
            sb.AppendLine(
                $"| {DuplicateNameCount} | {OverlappingFacePairs} | {RibsPastPeriEstimate} | {PlacementFailures} |");
            sb.AppendLine();
            sb.AppendLine("## Notes");
            sb.AppendLine();
            foreach (var n in Notes)
                sb.AppendLine($"- {n}");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, sb.ToString());
            return path;
        }
    }

    public static Report Analyze(CadDocument doc, float loa = 69f)
    {
        var sandwiches = doc.Entities
            .Where(e => e.Kind == "box" && e.Name is not null
                        && e.Name.StartsWith("deck-sandwich-", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var byDeck = new Dictionary<string, DeckCounts>(StringComparer.OrdinalIgnoreCase);
        DeckCounts Acc(string tag) =>
            byDeck.TryGetValue(tag, out var c) ? c : new DeckCounts(tag, 0, 0, 0, 0, 0, 0);

        void Set(string tag, DeckCounts c) => byDeck[tag] = c;

        foreach (var e in sandwiches)
        {
            var tag = DeckTag(e.Name!);
            var c = Acc(tag);
            var n = e.Name!;
            if (n.Contains("-face-top", StringComparison.OrdinalIgnoreCase))
                Set(tag, c with { FaceTops = c.FaceTops + 1 });
            else if (n.Contains("-face-bot", StringComparison.OrdinalIgnoreCase))
                Set(tag, c with { FaceBots = c.FaceBots + 1 });
            else if (n.Contains("-rib-long-", StringComparison.OrdinalIgnoreCase))
                Set(tag, c with { RibLong = c.RibLong + 1 });
            else if (n.Contains("-rib-ath-", StringComparison.OrdinalIgnoreCase))
                Set(tag, c with { RibAth = c.RibAth + 1 });
            else if (n.Contains("-peri-", StringComparison.OrdinalIgnoreCase))
                Set(tag, c with { Peri = c.Peri + 1 });
            else
                Set(tag, c with { Other = c.Other + 1 });
        }

        var nameGroups = sandwiches.GroupBy(e => e.Name!, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .ToList();
        var dupNames = nameGroups.Sum(g => g.Count() - 1);

        // Panel bases: strip -face-top|-face-bot|-rib-*|-peri-*
        var faceTops = sandwiches
            .Where(e => e.Name!.Contains("-face-top", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var overlappingFaces = 0;
        for (var i = 0; i < faceTops.Count; i++)
        {
            if (!TryExtents(faceTops[i], out var a))
                continue;
            for (var j = i + 1; j < faceTops.Count; j++)
            {
                if (!TryExtents(faceTops[j], out var b))
                    continue;
                if (MathF.Abs(a.Cup - b.Cup) > 0.08f)
                    continue;
                if (BoxesOverlapXyZ(a, b, 0.05f))
                    overlappingFaces++;
            }
        }

        // Ribs whose athwart or long extent exceeds sibling peri of same panel base.
        var pastPeri = 0;
        var byPanel = sandwiches.GroupBy(e => PanelBase(e.Name!), StringComparer.OrdinalIgnoreCase);
        foreach (var panel in byPanel)
        {
            var peri = panel.Where(e => e.Name!.Contains("-peri-", StringComparison.OrdinalIgnoreCase)).ToList();
            var ribs = panel.Where(e =>
                e.Name!.Contains("-rib-", StringComparison.OrdinalIgnoreCase)).ToList();
            if (peri.Count == 0 || ribs.Count == 0)
                continue;
            float maxHy = 0f, maxHz = 0f;
            foreach (var p in peri)
            {
                if (!TryExtents(p, out var pe))
                    continue;
                maxHy = MathF.Max(maxHy, MathF.Abs(pe.Cy) + pe.Hy);
                maxHz = MathF.Max(maxHz, MathF.Abs(pe.Cz) + pe.Hz);
            }

            // Approximate panel center from face-top if present.
            var face = panel.FirstOrDefault(e => e.Name!.Contains("-face-top", StringComparison.OrdinalIgnoreCase));
            if (face is null || !TryExtents(face, out var fe))
                continue;
            foreach (var rib in ribs)
            {
                if (!TryExtents(rib, out var re))
                    continue;
                // Compare in panel-local space (corridor strips are off-CL).
                var ribY = MathF.Abs(re.Cy - fe.Cy) + re.Hy;
                var ribZ = MathF.Abs(re.Cz - fe.Cz) + re.Hz;
                if (ribY > fe.Hy * 1.02f || ribZ > fe.Hz * 1.02f)
                    pastPeri++;
            }
        }

        var notes = new List<string>
        {
            $"Total deck-sandwich boxes: {sandwiches.Count}",
            $"Face-top panels: {faceTops.Count} (each panel should be one bay footprint, not ~1 m slices)",
            overlappingFaces > 0
                ? $"DEFECT: {overlappingFaces} coplanar face-sheet overlap pairs — likely overlapping longitudinal segment sandwiches (HullConformingSegments + halfLenZ web overlap)."
                : "No coplanar face-sheet overlaps detected.",
            dupNames > 0
                ? $"DEFECT: {dupNames} duplicate entity names."
                : "No duplicate deck-sandwich names.",
            pastPeri > 0
                ? $"DEFECT/estimate: {pastPeri} ribs extend past owning face half-extents."
                : "Ribs appear within face half-extents (AABB estimate).",
        };

        // Density hint: panels per deck elevation
        foreach (var d in byDeck.Values.OrderBy(x => x.DeckTag))
        {
            notes.Add(
                $"Deck {d.DeckTag}: faces={d.FaceTops}, longRibs={d.RibLong}, athRibs={d.RibAth}, peri={d.Peri} " +
                $"(~{(d.FaceTops == 0 ? 0 : (d.RibLong + d.RibAth) / (float)d.FaceTops):0.#} ribs/panel)");
        }

        return new Report(
            byDeck.Values.OrderBy(d => d.DeckTag).ToList(),
            dupNames,
            overlappingFaces,
            pastPeri,
            StructurePlacementLog.Count,
            notes);
    }

    private static string DeckTag(string name)
    {
        // deck-sandwich-dk{tag}-...
        var i = name.IndexOf("dk", StringComparison.OrdinalIgnoreCase);
        if (i < 0)
            return "?";
        var rest = name[(i + 2)..];
        var cut = rest.IndexOf('-');
        return cut < 0 ? rest : rest[..cut];
    }

    private static string PanelBase(string name)
    {
        foreach (var suffix in new[] { "-face-top", "-face-bot", "-rib-long-", "-rib-ath-", "-peri-" })
        {
            var i = name.IndexOf(suffix, StringComparison.OrdinalIgnoreCase);
            if (i > 0)
                return name[..i];
        }

        return name;
    }

    private readonly record struct Ext(float Cy, float Cup, float Cz, float Hy, float Hu, float Hz);

    private static bool TryExtents(CadEntity box, out Ext e)
    {
        e = default;
        if (box.Points is null || box.Points.Count < 2)
            return false;
        var a = box.Points[0];
        var b = box.Points[1];
        if (a is null || b is null || a.Length < 3 || b.Length < 3)
            return false;
        // Stored as center + half-extents in Calypso MakeSolid.
        e = new Ext((float)a[0], (float)a[1], (float)a[2],
            MathF.Abs((float)b[0]), MathF.Abs((float)b[1]), MathF.Abs((float)b[2]));
        return true;
    }

    private static bool BoxesOverlapXyZ(Ext a, Ext b, float margin)
    {
        var ox = MathF.Abs(a.Cy - b.Cy) < a.Hy + b.Hy - margin;
        var oz = MathF.Abs(a.Cz - b.Cz) < a.Hz + b.Hz - margin;
        return ox && oz;
    }
}
