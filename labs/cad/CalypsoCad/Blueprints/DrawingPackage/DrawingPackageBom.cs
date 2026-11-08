using System.Globalization;
using Novolis.Cad.Primitives;
using Novolis.Ship.Structure;

namespace CalypsoCad.Blueprints.DrawingPackage;

/// <summary>Aggregates structure + volume BOM lines from <c>calypso.cadjson</c>.</summary>
internal static class DrawingPackageBom
{
    public static IReadOnlyList<DrawingPackageBomLine> Build(
        CadDocument cad,
        IReadOnlyList<DrawingEntry> drawings)
    {
        var lines = new List<DrawingPackageBomLine>();
        var deckDrawing = drawings
            .Where(d => d.Category == DrawingCategoryId.DeckPlates && d.SubjectId is not null)
            .ToDictionary(
                d => ParseDeckNumFromSubject(d.SubjectId!),
                d => d.DrawingNumber);

        var memberSheets = drawings
            .Where(d => d.Category == DrawingCategoryId.StructuralMembers)
            .OrderBy(d => d.Sequence)
            .ToList();

        if (ShipStructureDocument.TryGetBom(cad, out var shipBom) && shipBom is not null)
        {
            var i = 0;
            foreach (var row in shipBom.Lines)
            {
                i++;
                lines.Add(new DrawingPackageBomLine(
                    $"STR-SHIP-{i:D3}",
                    "structure",
                    row.PartId,
                    $"{row.Group} · t={row.ThicknessM.ToString("0.###", CultureInfo.InvariantCulture)} m × qty {row.Qty}",
                    "ea",
                    row.Qty,
                    row.MassKg,
                    row.AreaM2,
                    row.MaterialDesignation ?? "AISI 316L",
                    null));
            }
        }

        // Deck sandwich: steel volume of faces + peri + ribs (not solid stack depth).
        foreach (var (deckNum, deckLabel) in new[] { (-1, "DK−1"), (0, "DK0"), (1, "DK+1") })
        {
            var steel = CadJsonStructureQuery.SandwichSteelMass(cad, deckNum);
            var faces = CadJsonStructureQuery.SandwichPanels(cad, CadJsonStructureQuery.Loa(cad))
                .Count(p => p.DeckNum == deckNum);
            deckDrawing.TryGetValue(deckNum, out var dwg);
            lines.Add(new DrawingPackageBomLine(
                $"STR-DECK-{deckLabel}",
                "deck-plates",
                $"deck-sandwich-{deckLabel}",
                $"{deckLabel} cellular sandwich steel · {faces} face-top bays · {steel.PartCount} steel parts " +
                $"(faces+peri+ribs) · V={steel.SteelVolumeM3.ToString("0.###", CultureInfo.InvariantCulture)} m³ · " +
                "hollow core excluded",
                "assy",
                1,
                steel.MassKg,
                steel.FaceTopAreaM2,
                "AISI 316L",
                dwg));
        }

        // Structural members — each line maps to the register sheet that lists it.
        var members = DrawingPackageCatalog.StructuralMemberNames(cad);
        var memSeq = 0;
        foreach (var name in members)
        {
            memSeq++;
            var sheetIndex = (memSeq - 1) / 40;
            var dwg = sheetIndex < memberSheets.Count ? memberSheets[sheetIndex].DrawingNumber : null;
            var kind = name.Split('-', 2)[0];
            lines.Add(new DrawingPackageBomLine(
                $"STR-MEM-{memSeq:D3}",
                "structural-members",
                name,
                $"Primary member · {kind} · mass unresolved (no section properties on entity)",
                "ea",
                1,
                null,
                null,
                "AISI 316L",
                dwg));
        }

        foreach (var d in drawings.Where(d => d.Category is DrawingCategoryId.Bulkheads or DrawingCategoryId.Frames))
        {
            lines.Add(new DrawingPackageBomLine(
                $"STR-DWG-{d.DrawingNumber}",
                d.Category == DrawingCategoryId.Bulkheads ? "bulkheads" : "frames",
                d.SubjectId ?? d.Label,
                $"{d.Title} · mass unresolved (see CAD plate/frame solids)",
                "dwg",
                1,
                null,
                null,
                "AISI 316L",
                d.DrawingNumber));
        }

        var volSeq = 0;
        foreach (var sp in CadJsonStructureQuery.Spaces(cad))
        {
            var kind = VolumeCirculation.Classify(sp.Id);
            if (kind == VolumeCirculationKind.None)
                continue;

            volSeq++;
            var area = MathF.Abs((sp.Z1 - sp.Z0) * (sp.Y1 - sp.Y0));
            var volume = area * MathF.Abs(sp.Up1 - sp.Up0);
            var dwg = drawings.FirstOrDefault(d =>
                string.Equals(d.SubjectId, sp.Id, StringComparison.OrdinalIgnoreCase))?.DrawingNumber;
            lines.Add(new DrawingPackageBomLine(
                $"VOL-{volSeq:D3}",
                VolumeCirculation.DisplayName(kind),
                sp.Id,
                $"{VolumeCirculation.DisplayName(kind)} volume · footprint {area:0.##} m² · ≈{volume:0.##} m³",
                "space",
                1,
                null,
                area,
                "habitable / pressure boundary",
                dwg));
        }

        return lines;
    }

    public static IReadOnlyList<DrawingPackageBomLine> FilterStructure(IReadOnlyList<DrawingPackageBomLine> all) =>
        all.Where(l => l.Category is "structure" or "deck-plates" or "structural-members" or "bulkheads" or "frames")
            .ToList();

    public static IReadOnlyList<DrawingPackageBomLine> FilterVolumes(IReadOnlyList<DrawingPackageBomLine> all) =>
        all.Where(l => l.LineId.StartsWith("VOL-", StringComparison.OrdinalIgnoreCase)).ToList();

    private static int ParseDeckNumFromSubject(string subjectId)
    {
        // "deck:{num}:{label}"
        if (subjectId.StartsWith("deck:", StringComparison.OrdinalIgnoreCase))
        {
            var parts = subjectId.Split(':', 3);
            if (parts.Length >= 2
                && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var n))
                return n;
        }

        return 0;
    }
}
