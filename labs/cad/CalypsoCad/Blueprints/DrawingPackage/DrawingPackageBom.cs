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
        var bySubject = drawings
            .Where(d => d.SubjectId is not null)
            .GroupBy(d => d.SubjectId!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().DrawingNumber, StringComparer.OrdinalIgnoreCase);

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
                    bySubject.GetValueOrDefault(row.PartId)));
            }
        }

        var loa = CadJsonStructureQuery.Loa(cad);
        var sandwiches = CadJsonStructureQuery.SandwichPanels(cad, loa);
        var deckSeq = 0;
        foreach (var s in sandwiches.OrderBy(s => s.DeckNum).ThenBy(s => s.Z0))
        {
            deckSeq++;
            var deckLabel = s.DeckNum switch { -1 => "DK−1", 0 => "DK0", 1 => "DK+1", _ => $"DK{s.DeckNum}" };
            var area = MathF.Abs(s.Z1 - s.Z0) * MathF.Abs(s.HalfY) * 2f;
            var mass = area * s.TPlate * 8000f;
            lines.Add(new DrawingPackageBomLine(
                $"STR-DECK-{deckSeq:D3}",
                "deck-plates",
                $"deck-sandwich-{deckLabel}-z{s.Z0:0}",
                $"{deckLabel} sandwich face · z={s.Z0:0.##}–{s.Z1:0.##} m · t={s.TPlate:0.###} m",
                "ea",
                1,
                mass,
                area,
                "AISI 316L",
                bySubject.GetValueOrDefault(deckLabel)));
        }

        var members = DrawingPackageCatalog.StructuralMemberNames(cad);
        var memSeq = 0;
        foreach (var name in members)
        {
            memSeq++;
            var kind = name.Split('-', 2)[0];
            lines.Add(new DrawingPackageBomLine(
                $"STR-MEM-{memSeq:D3}",
                "structural-members",
                name,
                $"Primary member · {kind}",
                "ea",
                1,
                null,
                null,
                "AISI 316L",
                drawings.FirstOrDefault(d => d.Category == DrawingCategoryId.StructuralMembers)?.DrawingNumber));
        }

        foreach (var d in drawings.Where(d => d.Category is DrawingCategoryId.Bulkheads or DrawingCategoryId.Frames))
        {
            lines.Add(new DrawingPackageBomLine(
                $"STR-DWG-{d.DrawingNumber}",
                d.Category == DrawingCategoryId.Bulkheads ? "bulkheads" : "frames",
                d.SubjectId ?? d.Label,
                d.Title,
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
                bySubject.GetValueOrDefault(sp.Id)));
        }

        return lines;
    }

    public static IReadOnlyList<DrawingPackageBomLine> FilterStructure(IReadOnlyList<DrawingPackageBomLine> all) =>
        all.Where(l => l.Category is "structure" or "deck-plates" or "structural-members" or "bulkheads" or "frames")
            .ToList();

    public static IReadOnlyList<DrawingPackageBomLine> FilterVolumes(IReadOnlyList<DrawingPackageBomLine> all) =>
        all.Where(l => l.LineId.StartsWith("VOL-", StringComparison.OrdinalIgnoreCase)).ToList();
}
