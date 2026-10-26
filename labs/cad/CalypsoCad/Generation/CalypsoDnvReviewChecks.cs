using System.Text.Json;
using CalypsoCad.Models;
using Novolis.Cad.Primitives;
using Novolis.Ship.Primitives;
using Novolis.Ship.Structure;

namespace CalypsoCad.Generation;

/// <summary>Classification-style design-review readiness (not DNV certification).</summary>
internal static class CalypsoDnvReviewChecks
{
    internal static readonly Guid LayerStruct = Guid.Parse("d1000000-0000-4000-8000-00000000000a");

    internal readonly record struct Check(string Name, bool Ok, string Detail);

    public static List<Check> Evaluate(CadDocument doc, CadLayersDocument? layersDoc)
    {
        var checks = new List<Check>();
        void Add(string name, bool ok, string detail = "") => checks.Add(new Check(name, ok, detail));

        Add(
            "review package id on document",
            doc.Properties is not null
            && doc.Properties.TryGetValue("reviewDrawingPackage", out var pkg)
            && pkg.GetString()?.Contains("CAL-DNV-REVIEW", StringComparison.OrdinalIgnoreCase) == true);

        Add(
            "units SI (m)",
            doc.Properties is not null
            && doc.Properties.TryGetValue("units", out var u)
            && string.Equals(u.GetString(), "m", StringComparison.OrdinalIgnoreCase));

        Add(
            "coordinate frame documented",
            doc.Properties is not null
            && doc.Properties.TryGetValue("coordinateFrame", out var cf)
            && cf.ValueKind == JsonValueKind.Object
            && cf.TryGetProperty("cad", out _));

        Add(
            "lock authority cited",
            doc.Properties is not null
            && doc.Properties.TryGetValue("source", out var src)
            && src.GetString()?.Contains("CAL-INT-GA-001", StringComparison.OrdinalIgnoreCase) == true);

        Add(
            "manufacturer OML cited",
            doc.Properties is not null
            && doc.Properties.TryGetValue("outerHull", out var oh)
            && oh.GetString()?.Contains("CAL-HULL-CAD-001", StringComparison.OrdinalIgnoreCase) == true);

        Add(
            "deck drawing cited",
            doc.Properties is not null
            && doc.Properties.TryGetValue("deckDrawing", out var dd)
            && dd.GetString()?.Contains("CAL-INT-DK-001", StringComparison.OrdinalIgnoreCase) == true);

        var spaces = ShipCad.Spaces(doc).ToList();
        var traced = spaces.Count(s =>
            s.Properties is not null
            && s.Properties.TryGetValue("drawing", out var d)
            && d.GetString()?.Contains("CAL-INT-DK-001", StringComparison.OrdinalIgnoreCase) == true);
        Add("compartments traceable to deck drawing", traced == spaces.Count && spaces.Count >= 20,
            $"{traced}/{spaces.Count}");

        var structCatalog = layersDoc?.Layers.Any(l =>
            string.Equals(l.Name, "S-STRUCT", StringComparison.OrdinalIgnoreCase)) == true;
        Add("S-STRUCT in layer catalog", structCatalog);

        var structSolids = doc.Entities.Where(e =>
            e.Name?.StartsWith("frame-", StringComparison.OrdinalIgnoreCase) == true
            || e.Name?.StartsWith("stringer-", StringComparison.OrdinalIgnoreCase) == true
            || e.Name?.StartsWith("coaming-", StringComparison.OrdinalIgnoreCase) == true).ToList();
        var onStructLayer = structSolids.Count(e => e.LayerId == LayerStruct);
        Add(
            "primary structure on S-STRUCT",
            structSolids.Count == 0 || onStructLayer >= structSolids.Count * 0.98f,
            $"onLayer={onStructLayer}/{structSolids.Count}");

        Add(
            "OML skin mass statement",
            ShipStructureDocument.TryGetMass(doc, out var mass)
            && mass is not null
            && mass.MassKg > 1_000f
            && mass.ThicknessM > 0.005f,
            mass is null ? "missing" : $"{mass.MassT:0.###} t @ t={mass.ThicknessM * 1000:0.#} mm");

        var openings = ShipCad.Openings(doc).ToList();
        var d3 = openings.FirstOrDefault(o =>
            o.Name?.StartsWith("D3-", StringComparison.OrdinalIgnoreCase) == true);
        Add(
            "exterior vacuum closure (D3) registered",
            d3 is not null && ShipCad.IsVacuumAssisted(d3!),
            d3?.Name ?? "missing");

        Add(
            "hold cargo hatch registered",
            openings.Any(o => string.Equals(o.Name, "HOLD-P-DKM1", StringComparison.OrdinalIgnoreCase)));

        Add(
            "frame-first ring webs",
            doc.Entities.Count(e => e.Name?.Contains("-webplate-", StringComparison.OrdinalIgnoreCase) == true) >= 60,
            "transverse web plates with passageway voids");
        Add(
            "no portal knee solids",
            !doc.Entities.Any(e => e.Name?.Contains("-knee-", StringComparison.OrdinalIgnoreCase) == true));

        return checks;
    }
}
