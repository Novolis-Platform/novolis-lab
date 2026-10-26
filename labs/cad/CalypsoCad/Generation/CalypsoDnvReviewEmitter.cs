using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using CalypsoCad.Models;
using Novolis.Cad.Primitives;
using Novolis.Ship.Primitives;
using Novolis.Ship.Structure;
using Novolis.Ship.Topology;

namespace CalypsoCad.Generation;

/// <summary>
/// Emits CAL-DNV-REVIEW-001 — a classification-style design review bundle (HTML + JSON manifest).
/// Not a substitute for formal DNV plan approval or rule calculation.
/// </summary>
internal static class CalypsoDnvReviewEmitter
{
    private static readonly (string File, string Title)[] VisualAnnex =
    [
        ("layer-cutaway-structure.png", "Structure layer + passageway cutout voids"),
        ("layer-cutaway-fitout.png", "Partitions, outfit, openings (no frame solids)"),
        ("orbit-cutaway-long.png", "Longitudinal cutaway — combined layers"),
        ("plan-deck-0.png", "Deck 0 plan"),
        ("plan-deck-p1.png", "Deck +1 plan"),
        ("interior-cutaway-portCorridor.png", "Port corridor — transverse frames at spine"),
    ];

    public static string Emit(string? outputDirectory = null, bool runHeadlessExports = false)
    {
        var generated = CalypsoLockGenerator.Generate(outputDirectory);
        var exportsDir = Path.Combine(generated, "exports");
        Directory.CreateDirectory(exportsDir);

        if (runHeadlessExports)
        {
            var session = new Services.CalypsoSession();
            session.RegenerateAndLoad();
            var renderer = new Services.CalypsoRenderer(session);
            renderer.Fit();
            renderer.SyncInteriorFromSelection();
            Services.HeadlessPngExporter.ExportViews(session, renderer, generated);
        }

        var cadPath = Path.Combine(generated, "calypso.cadjson");
        var layersPath = Path.Combine(generated, "calypso.cadlayers.json");
        var doc = JsonSerializer.Deserialize<CadDocument>(File.ReadAllText(cadPath), CadJson.Options)
                  ?? throw new InvalidOperationException("Failed to read calypso.cadjson");
        CadLayersDocument? layersDoc = File.Exists(layersPath)
            ? JsonSerializer.Deserialize<CadLayersDocument>(File.ReadAllText(layersPath), CadJson.Options)
            : null;

        var lockPath = ResolveLockPath();
        using var lockJson = JsonDocument.Parse(File.ReadAllText(lockPath));
        var lockRoot = lockJson.RootElement;
        var lockDrawing = lockRoot.TryGetProperty("drawing", out var ld) ? ld.GetString() : "CAL-INT-GA-001";
        var lockRev = lockRoot.TryGetProperty("rev", out var lr) ? lr.GetString() : "?";

        var acceptance = CalypsoAcceptance.Evaluate(doc);
        var review = CalypsoDnvReviewChecks.Evaluate(doc, layersDoc);
        ShipStructureDocument.TryGetMass(doc, out var skinMass);
        var topo = ShipTopology.Analyze(doc);

        var stamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + " UTC";
        var htmlPath = Path.Combine(generated, "CAL-DNV-REVIEW-001.html");
        var jsonPath = Path.Combine(generated, "calypso-dnv-review.json");

        File.WriteAllText(htmlPath, BuildHtml(
            generated,
            exportsDir,
            doc,
            lockRoot,
            lockDrawing ?? "CAL-INT-GA-001",
            lockRev ?? "?",
            stamp,
            acceptance,
            review,
            skinMass,
            topo), Encoding.UTF8);

        var manifest = new
        {
            package = "CAL-DNV-REVIEW-001",
            generatedUtc = stamp,
            lockAuthority = $"{lockDrawing} rev {lockRev}",
            cadModel = "calypso.cadjson",
            exportsDirectory = "exports",
            acceptanceOk = acceptance.All(c => c.Ok),
            reviewOk = review.All(c => c.Ok),
            acceptance = acceptance.Select(c => new { c.Name, c.Ok, c.Detail }),
            review = review.Select(c => new { c.Name, c.Ok, c.Detail }),
            visualAnnex = VisualAnnex.Select(v => v.File).ToArray(),
        };
        File.WriteAllText(jsonPath, JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }),
            Encoding.UTF8);

        return htmlPath;
    }

    private static string BuildHtml(
        string generatedDir,
        string exportsDir,
        CadDocument doc,
        JsonElement lockRoot,
        string lockDrawing,
        string lockRev,
        string stamp,
        List<CalypsoAcceptance.Check> acceptance,
        List<CalypsoDnvReviewChecks.Check> review,
        SkinMassResult? skinMass,
        ShipTopologyResult topo)
    {
        var loa = ShipDocumentMetrics.GetLoaMeters(doc);
        var sb = new StringBuilder();
        sb.Append("<!DOCTYPE html><html lang=\"en\"><head><meta charset=\"utf-8\"/>");
        sb.Append("<title>CAL-DNV-REVIEW-001 — Calypso design review package</title>");
        sb.Append("<style>body{font-family:Segoe UI,system-ui,sans-serif;max-width:960px;margin:2rem auto;line-height:1.45}");
        sb.Append("table{border-collapse:collapse;width:100%;margin:1rem 0}th,td{border:1px solid #ccc;padding:.35rem .5rem;text-align:left}");
        sb.Append(".ok{color:#0a6}.fail{color:#b00}.meta{color:#555;font-size:.9rem}.warn{background:#fff8e6;padding:.75rem;border:1px solid #e6c200}");
        sb.Append("img{max-width:100%;height:auto;border:1px solid #ddd;margin:.5rem 0}</style></head><body>");

        sb.Append("<h1>CAL-DNV-REVIEW-001 — Calypso tramp freighter</h1>");
        sb.Append("<p class=\"meta\">Design review package for third-party classification-style review. ");
        sb.Append("Generated ").Append(E(stamp)).Append(". Not DNV plan approval or rule calculation.</p>");
        sb.Append("<div class=\"warn\"><strong>Scope.</strong> Concept / detailed design model tied to lock ");
        sb.Append(E(lockDrawing)).Append(" rev ").Append(E(lockRev));
        sb.Append(". Suitable for dimensional, compartmentation, structure concept, and opening register review. ");
        sb.Append("Finite-element scantlings, stability booklet, and production nesting are out of scope until listed on the drawing register.</div>");

        sb.Append("<h2>1 — Design basis</h2><table><tr><th>Document</th><th>Role</th></tr>");
        AppendRow(sb, lockDrawing + " rev " + lockRev, "Compartment, hatch, and structure schedule (JSON lock)");
        AppendRow(sb, "CAL-INT-DK-001", "Deck plans — authoritative planRing footprints");
        AppendRow(sb, "CAL-HULL-CAD-001", "Manufacturer OML / IML mesh and skin mass");
        AppendRow(sb, "calypso.cadjson", "3D CAD assembly (this generation)");
        AppendRow(sb, "calypso.cadlayers.json", "Layer discipline (S-HULL, S-STRUCT, A-*)");
        sb.Append("</table>");

        sb.Append("<h2>2 — Principal particulars</h2>");
        sb.Append(EnvelopeTable(lockRoot));
        sb.Append("<p>LOA in model: <strong>").Append(loa.ToString("0.###", CultureInfo.InvariantCulture)).Append(" m</strong>. ");
        sb.Append("Spaces in topology: ").Append(topo.SpaceIds.Count).Append(".</p>");

        sb.Append("<h2>3 — Coordinate systems</h2><table><tr><th>Frame</th><th>Axes</th></tr>");
        AppendRow(sb, "CAD model (+X, +Y, +Z)", "Starboard, up, bow (m)");
        AppendRow(sb, "Lock JSON (y, up, zFromStem)", "Starboard, keel vertical, distance from stem");
        AppendRow(sb, "Manufacturer hull", "X forward, Y starboard, Z up");
        AppendRow(sb, "Transform", "CAD (y, up, LOA/2 − zFromStem)");
        sb.Append("</table>");

        sb.Append("<h2>4 — Compartment schedule</h2>");
        sb.Append(CompartmentTable(doc));

        sb.Append("<h2>5 — Structural concept</h2>");
        sb.Append(StructureSection(lockRoot, doc, skinMass));

        sb.Append("<h2>6 — Opening register</h2>");
        sb.Append(OpeningsTable(doc));

        sb.Append("<h2>7 — Layer discipline</h2><table><tr><th>Layer</th><th>Discipline</th><th>Content</th></tr>");
        AppendRow3(sb, "S-HULL", "Structure", "OML / IML shell meshes");
        AppendRow3(sb, "S-STRUCT", "Structure", "Transverse octagon frames (passageway cutouts), stringers, coaming");
        AppendRow3(sb, "A-WALL / A-DOOR", "Architectural", "Partitions and doors");
        AppendRow3(sb, "A-ZONE-*", "Architectural", "Space finishes and zoning");
        sb.Append("</table><p>Headless exports <code>layer-cutaway-structure.png</code> and ");
        sb.Append("<code>layer-cutaway-fitout.png</code> isolate S-STRUCT vs fitout for reviewer read.</p>");

        sb.Append("<h2>8 — Verification record</h2><h3>Design review checks</h3>");
        sb.Append(CheckTable(review.Select(c => (c.Name, c.Ok, c.Detail)).ToList()));
        sb.Append("<h3>Operational acceptance (CalypsoCad)</h3>");
        sb.Append(CheckTable(acceptance.Select(c => (c.Name, c.Ok, c.Detail)).ToList()));

        sb.Append("<h2>9 — Visual annex</h2><p class=\"meta\">Paths relative to ").Append(E(generatedDir)).Append("</p>");
        foreach (var (file, title) in VisualAnnex)
        {
            var full = Path.Combine(exportsDir, file);
            sb.Append("<h3>").Append(E(title)).Append("</h3>");
            if (File.Exists(full))
            {
                var rel = Path.GetRelativePath(generatedDir, full).Replace('\\', '/');
                sb.Append("<p><code>").Append(E(rel)).Append("</code></p>");
                sb.Append("<img src=\"").Append(E(rel)).Append("\" alt=\"").Append(E(title)).Append("\"/>");
            }
            else
                sb.Append("<p class=\"fail\">Missing — run <code>--headless</code> or <code>--dnv-review --headless</code>.</p>");
        }

        sb.Append("<h2>10 — Regeneration</h2><pre>");
        sb.Append(E("dotnet run --project d:\\novolis\\novolis-lab\\labs\\cad\\CalypsoCad\\CalypsoCad.csproj -p:NovolisUseProjectReferences=true -- --dnv-review --headless"));
        sb.Append("</pre></body></html>");
        return sb.ToString();
    }

    private static string EnvelopeTable(JsonElement lockRoot)
    {
        if (!lockRoot.TryGetProperty("envelope", out var env))
            return "<p>Envelope missing from lock.</p>";
        var sb = new StringBuilder();
        sb.Append("<table><tr><th>Item</th><th>Value</th></tr>");
        foreach (var name in new[] { "LOA", "BEAM", "OAH", "L_FORE", "L_MID", "L_AFT", "CHAMFER", "T_SHELL", "MATERIAL" })
        {
            if (!env.TryGetProperty(name, out var v))
                continue;
            sb.Append("<tr><td>").Append(E(name)).Append("</td><td>").Append(E(v.ToString())).Append("</td></tr>");
        }

        if (lockRoot.TryGetProperty("decks", out var decks))
        {
            foreach (var name in new[] { "m1", "d0", "d1", "roomH" })
            {
                if (!decks.TryGetProperty(name, out var v))
                    continue;
                sb.Append("<tr><td>deck ").Append(E(name)).Append("</td><td>").Append(E(v.ToString())).Append(" m</td></tr>");
            }
        }

        sb.Append("</table>");
        return sb.ToString();
    }

    private static string CompartmentTable(CadDocument doc)
    {
        var rows = ShipCad.Spaces(doc)
            .OrderBy(s => s.Deck)
            .ThenBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var sb = new StringBuilder();
        sb.Append("<table><tr><th>Id</th><th>Deck</th><th>Drawing</th><th>Footprint (CAD)</th></tr>");
        foreach (var s in rows)
        {
            var draw = s.Properties?.TryGetValue("drawing", out var d) == true ? d.GetString() : "";
            var w = 0f;
            var depth = 0f;
            if (s.Points is { Count: >= 3 })
            {
                var ys = s.Points.Select(p => p[0]).ToList();
                var zs = s.Points.Select(p => p[2]).ToList();
                w = ys.Max() - ys.Min();
                depth = zs.Max() - zs.Min();
            }

            sb.Append("<tr><td>").Append(E(s.Name ?? "")).Append("</td><td>").Append(s.Deck);
            sb.Append("</td><td>").Append(E(draw ?? "")).Append("</td><td>");
            sb.Append(w.ToString("0.##", CultureInfo.InvariantCulture)).Append(" × ");
            sb.Append(depth.ToString("0.##", CultureInfo.InvariantCulture)).Append(" m</td></tr>");
        }

        sb.Append("</table>");
        return sb.ToString();
    }

    private static string StructureSection(JsonElement lockRoot, CadDocument doc, SkinMassResult? skinMass)
    {
        var frames = doc.Entities.Count(e => e.Name?.StartsWith("frame-", StringComparison.OrdinalIgnoreCase) == true);
        var stringers = doc.Entities.Count(e => e.Name?.StartsWith("stringer-", StringComparison.OrdinalIgnoreCase) == true);
        var sb = new StringBuilder();
        sb.Append("<ul><li>Frame-first: closed transverse octagon <strong>ring webs</strong> on the inner mold (8 edges per station), with athwart gaps at corridors/crossing/stairs and vertical door band at shell.</li>");
        sb.Append("<li>Ring web solids: ").Append(doc.Entities.Count(e => e.Name?.Contains("-web-", StringComparison.OrdinalIgnoreCase) == true)).Append("; shell corner brackets tie web to plate.</li>");
        sb.Append("<li>Deck longitudinals (keel + corridor girders) and transverse deck beams at each frame station.</li>");
        sb.Append("<li>Frame solids: ").Append(frames).Append("; stringer bays: ").Append(stringers).Append(".</li>");
        if (lockRoot.TryGetProperty("frames", out var fr))
        {
            if (fr.TryGetProperty("webM", out var web))
                sb.Append("<li>Lock web thickness: ").Append(E(web.ToString())).Append(" m.</li>");
            if (fr.TryGetProperty("stationIds", out var ids) && ids.ValueKind == JsonValueKind.Array)
            {
                var list = ids.EnumerateArray().Select(e => e.GetString()).Where(s => s is not null);
                sb.Append("<li>Station set: ").Append(E(string.Join(", ", list!))).Append(".</li>");
            }
        }

        sb.Append("</ul>");
        if (skinMass is not null)
        {
            sb.Append("<h3>OML skin mass (manufacturer area rollup)</h3><table><tr><th>Item</th><th>Value</th></tr>");
            AppendRow(sb, "Outer skin area", skinMass.OuterSkinAreaM2.ToString("0.###", CultureInfo.InvariantCulture) + " m²");
            AppendRow(sb, "Thickness", (skinMass.ThicknessM * 1000).ToString("0.#", CultureInfo.InvariantCulture) + " mm");
            AppendRow(sb, "Mass", skinMass.MassT.ToString("0.###", CultureInfo.InvariantCulture) + " t");
            sb.Append("</table><p class=\"meta\">Lightweight / displacement and LCG require outfit and machinery mass books — not in this package.</p>");
        }

        return sb.ToString();
    }

    private static string OpeningsTable(CadDocument doc)
    {
        var sb = new StringBuilder();
        sb.Append("<table><tr><th>Name</th><th>Vacuum</th><th>Leaf</th><th>Connects</th></tr>");
        foreach (var o in ShipCad.Openings(doc).OrderBy(o => o.Name, StringComparer.OrdinalIgnoreCase))
        {
            var vac = ShipCad.IsVacuumAssisted(o) ? "yes" : "";
            var leaf = ShipCad.GetLeafState(o).ToString();
            var conn = string.Join(", ", ShipCad.GetOpeningConnects(o));
            sb.Append("<tr><td>").Append(E(o.Name ?? "")).Append("</td><td>").Append(vac);
            sb.Append("</td><td>").Append(E(leaf)).Append("</td><td>").Append(E(conn)).Append("</td></tr>");
        }

        sb.Append("</table>");
        return sb.ToString();
    }

    private static string CheckTable(IReadOnlyList<(string Name, bool Ok, string Detail)> checks)
    {
        var sb = new StringBuilder();
        sb.Append("<table><tr><th>Check</th><th>Result</th><th>Detail</th></tr>");
        foreach (var c in checks)
        {
            sb.Append("<tr><td>").Append(E(c.Name)).Append("</td><td class=\"");
            sb.Append(c.Ok ? "ok\">OK" : "fail\">FAIL").Append("</td><td>").Append(E(c.Detail)).Append("</td></tr>");
        }

        sb.Append("</table>");
        return sb.ToString();
    }

    private static void AppendRow(StringBuilder sb, string a, string b)
    {
        sb.Append("<tr><td>").Append(E(a)).Append("</td><td>").Append(E(b)).Append("</td></tr>");
    }

    private static void AppendRow3(StringBuilder sb, string a, string b, string c)
    {
        sb.Append("<tr><td>").Append(E(a)).Append("</td><td>").Append(E(b)).Append("</td><td>").Append(E(c)).Append("</td></tr>");
    }

    private static string E(string? s) => WebUtility.HtmlEncode(s ?? "");

    private static string ResolveLockPath()
    {
        var proj = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", ".."));
        foreach (var p in new[]
                 {
                     Path.Combine(AppContext.BaseDirectory, "lock", "CAL-INT-GA-001.json"),
                     Path.Combine(proj, "docs", "internals", "CAL-INT-GA-001.json"),
                     Path.Combine(@"d:\novolis\novolis-lab\labs\cad\CalypsoCad\docs\internals", "CAL-INT-GA-001.json"),
                     Path.Combine(
                         Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                         "Novolis", "CalypsoCad", "generated", "lock", "CAL-INT-GA-001.json"),
                 })
        {
            if (File.Exists(p))
                return p;
        }

        throw new FileNotFoundException("CAL-INT-GA-001.json not found for DNV review package.");
    }
}
