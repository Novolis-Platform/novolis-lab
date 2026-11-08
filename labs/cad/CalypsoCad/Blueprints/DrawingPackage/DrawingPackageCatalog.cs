using System.Globalization;
using CalypsoCad.Structure;
using Novolis.Cad.Primitives;

namespace CalypsoCad.Blueprints.DrawingPackage;

/// <summary>Builds the numbered drawing register from <c>calypso.cadjson</c> only.</summary>
internal static class DrawingPackageCatalog
{
    public static IReadOnlyList<DrawingEntry> Build(CadDocument cad)
    {
        var entries = new List<DrawingEntry>();
        var seq = new Dictionary<DrawingCategoryId, int>();
        var loa = CadJsonStructureQuery.Loa(cad);

        DrawingEntry Add(
            DrawingCategoryId cat,
            string title,
            string label,
            string fileStem,
            string? detailSource,
            string? subjectId,
            VolumeCirculationKind volume,
            params string[] tags)
        {
            var n = seq.GetValueOrDefault(cat) + 1;
            seq[cat] = n;
            var spec = DrawingPackageCategories.Get(cat);
            var number = $"CAL-{spec.SystemCode}-{n:D3}";
            var file = $"{number}-{Sanitize(fileStem)}.html";
            var relative = Path.Combine(spec.FolderName, file).Replace('\\', '/');
            var entry = new DrawingEntry(
                cat, n, number, title, label, relative, detailSource, subjectId, volume, tags);
            entries.Add(entry);
            return entry;
        }

        Add(DrawingCategoryId.General,
            "Drawing package index",
            "PKG-INDEX",
            "INDEX",
            null,
            "PACKAGE",
            VolumeCirculationKind.None,
            "iso-7200", "drawing-list", "cadjson");

        // —— Structure: bulkheads (from CAD bh-*-plate + schedule notes) ——
        var notes = CalypsoBulkheads.BuildSchedule(loa, CadJsonStructureQuery.StationMap(cad))
            .ToDictionary(s => s.Id, s => s.Note, StringComparer.OrdinalIgnoreCase);
        foreach (var bh in CadJsonStructureQuery.BulkheadStations(cad, loa))
        {
            notes.TryGetValue(bh.Id, out var note);
            Add(DrawingCategoryId.Bulkheads,
                $"Bulkhead {bh.Id} @ z={bh.ZStem.ToString("0.##", CultureInfo.InvariantCulture)} m from stem",
                $"BH-{bh.Id}",
                $"BH-{bh.Id}",
                detailSource: null,
                bh.Id,
                VolumeCirculationKind.None,
                "structure", "bulkhead", $"z={bh.ZStem.ToString("0.##", CultureInfo.InvariantCulture)}",
                note ?? "heavy bulkhead");
        }

        // —— Structure: frames (stations with annular plate-web mesh and/or legacy ring bars) ——
        foreach (var fr in CadJsonStructureQuery.PhysicalFrameStations(cad))
        {
            // Prefer id token (F61.1) over rounded metres so sheets match CAD entity names.
            var token = fr.FrameId.StartsWith("frame-F", StringComparison.OrdinalIgnoreCase)
                ? fr.FrameId["frame-F".Length..]
                : fr.ZStem.ToString("0.#", CultureInfo.InvariantCulture);
            Add(DrawingCategoryId.Frames,
                $"Transverse frame {fr.FrameId} @ z={fr.ZStem.ToString("0.##", CultureInfo.InvariantCulture)} m ({fr.PartCount} perimeter parts)",
                $"FRM-Z{token}",
                $"FRAME-z{token}",
                detailSource: null,
                fr.FrameId,
                VolumeCirculationKind.None,
                "structure", "frame", $"z={fr.ZStem.ToString("0.##", CultureInfo.InvariantCulture)}", fr.FrameId);
        }

        // —— Structure: deck plates ——
        foreach (var (deckNum, deckLabel, fileStem) in new[]
                 {
                     (-1, "DK−1", "DKM1"),
                     (0, "DK0", "DK0"),
                     (1, "DK+1", "DKP1"),
                 })
        {
            var sandwiches = CadJsonStructureQuery.SandwichPanels(cad, loa)
                .Where(s => s.DeckNum == deckNum)
                .ToList();
            Add(DrawingCategoryId.DeckPlates,
                $"Deck plates — {deckLabel} sandwich face schedule ({sandwiches.Count} panels)",
                $"DECK-{deckLabel}",
                $"DECK-{fileStem}",
                detailSource: null,
                $"deck:{deckNum}:{deckLabel}",
                VolumeCirculationKind.None,
                "structure", "deck-plate", deckLabel, $"deckNum={deckNum}");
        }

        // —— Structure: members ——
        var members = StructuralMemberNames(cad);
        var memberSeq = 0;
        foreach (var chunk in members.Chunk(40))
        {
            memberSeq++;
            Add(DrawingCategoryId.StructuralMembers,
                $"Structural members register — sheet {memberSeq} ({chunk.Length} ids)",
                $"MEM-REG-{memberSeq:D2}",
                $"MEMBERS-{memberSeq:D2}",
                null,
                $"members-{memberSeq}",
                VolumeCirculationKind.None,
                "structure", "member-register");
        }

        if (members.Count == 0)
        {
            Add(DrawingCategoryId.StructuralMembers,
                "Structural members register — empty",
                "MEM-REG-01",
                "MEMBERS-01",
                null,
                "members-1",
                VolumeCirculationKind.None,
                "structure", "member-register");
        }

        // —— Volumes from CAD spaces ——
        foreach (var space in CadJsonStructureQuery.Spaces(cad))
        {
            var kind = VolumeCirculation.Classify(space.Id);
            if (kind == VolumeCirculationKind.None)
                continue;
            var cat = VolumeCirculation.CategoryFor(kind);
            Add(cat,
                $"{VolumeCirculation.DisplayName(kind)} — {space.Id}",
                space.Id,
                space.Id,
                detailSource: null,
                space.Id,
                kind,
                "volume", kind.ToString(), $"deck={space.Deck}");
        }

        Add(DrawingCategoryId.Bom, "Master bill of materials", "BOM-MASTER", "MASTER",
            null, "master", VolumeCirculationKind.None, "bom", "master");
        Add(DrawingCategoryId.Bom, "Structure BOM — bulkheads / frames / decks / members", "BOM-STR", "STRUCTURE",
            null, "structure", VolumeCirculationKind.None, "bom", "structure");
        Add(DrawingCategoryId.Bom, "Volume circulation BOM — shirt-sleeves / airlocks / hybrid", "BOM-VOL", "VOLUMES",
            null, "volumes", VolumeCirculationKind.None, "bom", "volumes");

        return entries;
    }

    public static IReadOnlyList<string> StructuralMemberNames(CadDocument cad) =>
        cad.Entities
            .Where(e => e.Name is not null && IsStructuralMemberName(e.Name))
            .Select(e => e.Name!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static bool IsStructuralMemberName(string name) =>
        name.StartsWith("girder-", StringComparison.OrdinalIgnoreCase)
        || name.StartsWith("long-", StringComparison.OrdinalIgnoreCase)
        || name.StartsWith("longitudinal-", StringComparison.OrdinalIgnoreCase)
        || name.StartsWith("coaming-", StringComparison.OrdinalIgnoreCase)
        || name.StartsWith("stringer-", StringComparison.OrdinalIgnoreCase)
        || name.StartsWith("frm-", StringComparison.OrdinalIgnoreCase)
        || (name.StartsWith("frame-", StringComparison.OrdinalIgnoreCase)
            && !name.Contains("-web", StringComparison.OrdinalIgnoreCase));

    private static string Sanitize(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return "X";
        var chars = raw.Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '-').ToArray();
        return new string(chars);
    }
}
