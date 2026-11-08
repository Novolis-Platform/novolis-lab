using System.Text.Json;
using CalypsoCad.Model;
using CalypsoCad.Publication;
using CalypsoCad.Session;
using Novolis.Cad.Primitives;
using Novolis.Cad.SceneBridge.Tessellation;
using Novolis.Math.Geometry;

namespace CalypsoCad.Export;

/// <summary>
/// Identifies every large structural mesh (esp. orange BH plates vs annular webs) by part ID.
/// </summary>
internal static class StructureSurfaceInventory
{
    public sealed record SurfaceRow(
        string PartId,
        string AssemblyId,
        string PartKind,
        string System,
        float? StationZStem,
        int TriangleCount,
        string GeometrySource,
        string ExpectedBoundary,
        string BoundaryMatch,
        string DisplayName,
        Guid EntityId);

    public static IReadOnlyList<SurfaceRow> Collect(CadDocument doc, ShipPartRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(doc);
        ArgumentNullException.ThrowIfNull(registry);

        var rows = new List<SurfaceRow>();
        foreach (var e in doc.Entities ?? [])
        {
            if (!string.Equals(e.Kind, "mesh", StringComparison.OrdinalIgnoreCase))
                continue;
            if (e.Name is null)
                continue;
            if (!CadDocumentAdapter.IsClassifiableEntity(e)
                && !e.Name.StartsWith("bh-", StringComparison.OrdinalIgnoreCase)
                && !e.Name.StartsWith("frame-", StringComparison.OrdinalIgnoreCase))
                continue;

            CadPart? part = null;
            if (CadDocumentAdapter.TryReadStamped(e, out var stamped))
                part = stamped;
            else if (registry.TryGetPartByDisplayName(e.Name, out var reg))
                part = reg;

            var kind = part?.Kind.ToString() ?? "Unknown";
            var asm = part?.Assembly.Value ?? ReadAsm(e) ?? "(unresolved)";
            var partId = part?.Id.Value ?? e.Name;
            var system = part?.System.ToString() ?? "Unknown";
            var station = part?.StationZStem ?? ReadFloat(e, "stationZ");

            var mesh = CadSolidTessellator.TryTessellate(e);
            var tris = mesh is null ? 0 : mesh.Indices.Count / 3;
            var source = mesh is null ? "unresolved" : "EditableMesh (CAD tessellation)";

            var expected = kind switch
            {
                nameof(CadPartKind.BulkheadPlate) =>
                    "Full IML cross-section plate with passage openings; transverse at BH station",
                nameof(CadPartKind.AnnularWebPlate) or nameof(CadPartKind.RingFrame) or nameof(CadPartKind.WebFrame) =>
                    "Annular open-web ring on IML inset; large central void; not a closed wall",
                nameof(CadPartKind.KnuckleStringer) or nameof(CadPartKind.HullStringer) =>
                    "Swept rect section along outer-hull knuckle path",
                _ => "See assembly / part kind",
            };

            var match = EvaluateBoundary(kind, mesh, e.Name);

            rows.Add(new SurfaceRow(
                partId, asm, kind, system, station, tris, source, expected, match, e.Name, e.Id));
        }

        return rows
            .OrderByDescending(r => r.TriangleCount)
            .ThenBy(r => r.PartId, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static string WriteJson(CadDocument doc, ShipPartRegistry registry, string? directory = null)
    {
        var dir = directory ?? Path.Combine(CalypsoProjectPaths.RendersDirectory, "structure-inspection");
        Directory.CreateDirectory(dir);
        var rows = Collect(doc, registry);
        var orange = rows.Where(r =>
            string.Equals(r.PartKind, nameof(CadPartKind.BulkheadPlate), StringComparison.OrdinalIgnoreCase)
            || r.TriangleCount >= 200).ToList();

        var payload = new
        {
            generatedUtc = DateTime.UtcNow.ToString("o"),
            totalMeshes = rows.Count,
            bulkheadPlates = rows.Count(r => r.PartKind == nameof(CadPartKind.BulkheadPlate)),
            annularWebs = rows.Count(r =>
                r.PartKind is nameof(CadPartKind.AnnularWebPlate) or nameof(CadPartKind.RingFrame)
                    or nameof(CadPartKind.WebFrame)),
            note = "Inspection colors: BH=orange, annular webs=steel. Large orange surfaces should be BulkheadPlate only.",
            bulkheadAndLargeSurfaces = orange,
            allStructuralMeshes = rows,
        };

        var path = Path.Combine(dir, "surface-inventory.json");
        File.WriteAllText(path, JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));
        return path;
    }

    private static string EvaluateBoundary(string kind, EditableMesh? mesh, string name)
    {
        if (mesh is null)
            return "fail: no mesh";
        var tris = mesh.Indices.Count / 3;
        if (kind == nameof(CadPartKind.BulkheadPlate))
        {
            // Closed BH plate should be a substantial triangulation with openings.
            return tris >= 40
                ? "ok: substantial plate mesh (openings expected)"
                : $"warn: sparse plate tris={tris}";
        }

        if (kind is nameof(CadPartKind.AnnularWebPlate) or nameof(CadPartKind.RingFrame) or nameof(CadPartKind.WebFrame)
            || name.Contains("annulus", StringComparison.OrdinalIgnoreCase))
        {
            return tris >= 16
                ? "ok: annular web mesh present"
                : $"warn: sparse annulus tris={tris}";
        }

        return "ok: classified";
    }

    private static string? ReadAsm(CadEntity e)
    {
        if (e.Properties is null || !e.Properties.TryGetValue("assemblyId", out var el))
            return null;
        return el.ValueKind == JsonValueKind.String ? el.GetString() : null;
    }

    private static float? ReadFloat(CadEntity e, string key)
    {
        if (e.Properties is null || !e.Properties.TryGetValue(key, out var el))
            return null;
        return el.ValueKind == JsonValueKind.Number ? el.GetSingle() : null;
    }
}
