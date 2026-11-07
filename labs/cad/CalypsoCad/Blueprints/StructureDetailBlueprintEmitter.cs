using System.Globalization;
using System.Text;
using System.Text.Json;
using CalypsoCad.Generation;
using Novolis.Cad.Primitives;
using CalypsoCad.Host;
using CalypsoCad.Rendering;
using CalypsoCad.Export;

namespace CalypsoCad.Blueprints;

/// <summary>
/// Modular 2D detail pack for agents: per-BH crops, frame samples, major space plans.
/// HTML+SVG only (C#). Output under docs/internals/details/.
/// </summary>
internal static class StructureDetailBlueprintEmitter
{
    private const float Loa = 69f;
    private const float Beam = 20f;
    private const float Oah = 12f;

    public static string Emit(string? cadPath = null, string? outputDirectory = null)
    {
        var outDir = outputDirectory ?? Path.Combine(
            CalypsoProjectPaths.ProjectRoot, "docs", "internals", "details");
        Directory.CreateDirectory(outDir);

        cadPath ??= Path.Combine(CalypsoLockGenerator.DefaultOutputDirectory, "calypso.cadjson");
        if (!File.Exists(cadPath))
            CalypsoLockGenerator.Generate();

        var doc = JsonSerializer.Deserialize<CadDocument>(File.ReadAllText(cadPath!), CadJson.Options)
                  ?? throw new InvalidOperationException("Failed to deserialize calypso.cadjson");

        var index = new List<(string File, string Title)>();

        foreach (var (id, zStem, _) in CalypsoBulkheads.BuildSchedule(Loa))
        {
            var file = $"BH-{id}.html";
            var html = BuildBhSheet(doc, id, zStem);
            Write(outDir, file, html);
            index.Add((file, $"Bulkhead {id} @ z={zStem:0.##}m from stem"));
        }

        // Sample every 5th frame bay if present
        var frameZs = doc.Entities
            .Where(e => e.Name?.StartsWith("frame-F", StringComparison.OrdinalIgnoreCase) == true
                        && e.Points is { Count: >= 1 })
            .Select(e =>
            {
                var c = SvgCoords.FromArray(e.Points![0]);
                return MathF.Round(Loa * 0.5f - c.Z, 1);
            })
            .Distinct()
            .OrderBy(z => z)
            .ToList();
        for (var i = 0; i < frameZs.Count; i += 5)
        {
            var z = frameZs[i];
            var file = $"FRAME-z{z.ToString("0.#", CultureInfo.InvariantCulture).Replace('.', '_')}.html";
            Write(outDir, file, BuildFrameSheet(doc, z));
            index.Add((file, $"Frame web sample @ z={z:0.#}m from stem"));
        }

        foreach (var space in new[] { "BRIDGE", "ENG", "HOLD", "CORR_P", "CORR_S", "CROSSING" })
        {
            var file = $"SPACE-{space}.html";
            Write(outDir, file, BuildSpaceSheet(doc, space));
            index.Add((file, $"Deck-plan crop — {space}"));
        }

        Write(outDir, "index.html", BuildIndex(index));
        Console.WriteLine($"Wrote {index.Count + 1} structure detail sheets → {outDir}");
        return outDir;
    }

    private static void Write(string dir, string name, string html) =>
        File.WriteAllText(Path.Combine(dir, name), html, new UTF8Encoding(false));

    private static string BuildIndex(List<(string File, string Title)> items)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html><html><head><meta charset=\"utf-8\"/>");
        sb.AppendLine("<title>Calypso structure detail pack</title>");
        sb.AppendLine("<style>body{font:14px/1.4 system-ui;margin:2rem;background:#f4f4f0;color:#222}");
        sb.AppendLine("a{color:#0645ad} li{margin:0.35rem 0}</style></head><body>");
        sb.AppendLine("<h1>Calypso structure detail pack</h1>");
        sb.AppendLine("<p>ISO stern-left / bow-right. Agents: consume these + structure-audit.json before Silk FPS.</p>");
        sb.AppendLine("<ol>");
        foreach (var (file, title) in items)
            sb.AppendLine($"<li><a href=\"{file}\">{title}</a></li>");
        sb.AppendLine("</ol></body></html>");
        return sb.ToString();
    }

    private static string BuildBhSheet(CadDocument doc, string id, float zStem)
    {
        var cells = doc.Entities.Where(e =>
            e.Kind == "box"
            && e.Name?.StartsWith($"bh-{id}", StringComparison.OrdinalIgnoreCase) == true
            && e.Points is { Count: >= 2 }).ToList();
        var zw = Loa * 0.5f - zStem;
        var sb = SheetHead($"BH {id}", $"zFromStem={zStem:0.##}m  worldZ={zw:0.##}  cells={cells.Count}");
        sb.AppendLine(SvgOpen(820, 520));
        // Plan crop (top view): X athwart horizontal, Z longitudinal (stern left)
        sb.AppendLine(Label(40, 28, "PLAN (stern left)"));
        sb.AppendLine(DrawPlanCrop(cells, 40, 40, 360, 200, zw));
        // Transverse section at station
        sb.AppendLine(Label(440, 28, "TRANSVERSE (looking aft)"));
        sb.AppendLine(DrawTransverse(cells, 440, 40, 340, 200));
        // Profile tick
        sb.AppendLine(Label(40, 280, "PROFILE tick (CL)"));
        sb.AppendLine(DrawProfileTick(zStem, 40, 300, 740, 160));
        sb.AppendLine("</svg></body></html>");
        return sb.ToString();
    }

    private static string BuildFrameSheet(CadDocument doc, float zStem)
    {
        var zw = Loa * 0.5f - zStem;
        var cells = doc.Entities.Where(e =>
        {
            if (e.Kind != "box" || e.Name is null || e.Points is not { Count: >= 2 })
                return false;
            if (!e.Name.StartsWith("frame-", StringComparison.OrdinalIgnoreCase))
                return false;
            var c = SvgCoords.FromArray(e.Points[0]);
            return MathF.Abs(c.Z - zw) < 0.6f;
        }).ToList();

        var sb = SheetHead($"Frame @ z={zStem:0.#}m", $"cells={cells.Count}");
        sb.AppendLine(SvgOpen(820, 420));
        sb.AppendLine(Label(40, 28, "TRANSVERSE WEB OUTLINE"));
        sb.AppendLine(DrawTransverse(cells, 40, 40, 740, 340));
        sb.AppendLine("</svg></body></html>");
        return sb.ToString();
    }

    private static string BuildSpaceSheet(CadDocument doc, string spaceName)
    {
        var spaces = doc.Entities.Where(e =>
            string.Equals(e.Kind, "space", StringComparison.OrdinalIgnoreCase)
            && string.Equals(e.Name, spaceName, StringComparison.OrdinalIgnoreCase)
            && e.Points is { Count: >= 3 }).ToList();

        var sb = SheetHead($"Space {spaceName}", $"footprints={spaces.Count}");
        sb.AppendLine(SvgOpen(820, 420));
        sb.AppendLine(Label(40, 28, "DECK PLAN CROP (stern left)"));
        sb.AppendLine(DrawSpacePlan(spaces, 40, 40, 740, 340));
        // BH station marks
        foreach (var (_, zStem, _) in CalypsoBulkheads.BuildSchedule(Loa))
        {
            var x = MapZ(zStem, 40, 740);
            sb.AppendLine($"<line x1=\"{F(x)}\" y1=\"40\" x2=\"{F(x)}\" y2=\"380\" stroke=\"#c82828\" stroke-width=\"1.5\"/>");
        }

        sb.AppendLine("</svg></body></html>");
        return sb.ToString();
    }

    private static StringBuilder SheetHead(string title, string sub)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html><html><head><meta charset=\"utf-8\"/>");
        sb.AppendLine($"<title>{title}</title>");
        sb.AppendLine("<style>body{margin:0;background:#ebe6dc;font:12px Consolas,monospace;color:#222}");
        sb.AppendLine("h1{font:600 16px system-ui;margin:12px 16px 4px} p{margin:0 16px 8px;color:#444}</style></head><body>");
        sb.AppendLine($"<h1>{title}</h1><p>{sub} — ISO 128-15 stern left / bow right</p>");
        return sb;
    }

    private static string SvgOpen(int w, int h) =>
        $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{w}\" height=\"{h}\" viewBox=\"0 0 {w} {h}\">" +
        $"<rect width=\"{w}\" height=\"{h}\" fill=\"#f7f3ea\" stroke=\"#333\"/>";

    private static string Label(double x, double y, string t) =>
        $"<text x=\"{F(x)}\" y=\"{F(y)}\" fill=\"#333\" font-size=\"11\">{Esc(t)}</text>";

    private static string DrawPlanCrop(List<CadEntity> cells, double ox, double oy, double w, double h, float focusZ)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"<rect x=\"{F(ox)}\" y=\"{F(oy)}\" width=\"{F(w)}\" height=\"{F(h)}\" fill=\"#fff\" stroke=\"#888\"/>");
        // X: -beam/2..beam/2 → ox..ox+w; Z near focus → oy..oy+h
        foreach (var e in cells)
        {
            var c = SvgCoords.FromArray(e.Points![0]);
            var he = SvgCoords.FromArray(e.Points[1]);
            var x0 = ox + (c.X - MathF.Abs(he.X) + Beam * 0.5f) / Beam * w;
            var x1 = ox + (c.X + MathF.Abs(he.X) + Beam * 0.5f) / Beam * w;
            var z0 = oy + (focusZ - c.Z - MathF.Abs(he.Z) + 4f) / 8f * h;
            var z1 = oy + (focusZ - c.Z + MathF.Abs(he.Z) + 4f) / 8f * h;
            sb.AppendLine($"<rect x=\"{F(Math.Min(x0, x1))}\" y=\"{F(Math.Min(z0, z1))}\" width=\"{F(Math.Abs(x1 - x0))}\" height=\"{F(Math.Max(1, Math.Abs(z1 - z0)))}\" fill=\"#c8c8c8\" stroke=\"#c82828\" stroke-width=\"0.6\"/>");
        }

        // CL
        sb.AppendLine($"<line x1=\"{F(ox + w * 0.5)}\" y1=\"{F(oy)}\" x2=\"{F(ox + w * 0.5)}\" y2=\"{F(oy + h)}\" stroke=\"#0645ad\" stroke-dasharray=\"4 3\"/>");
        return sb.ToString();
    }

    private static string DrawTransverse(List<CadEntity> cells, double ox, double oy, double w, double h)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"<rect x=\"{F(ox)}\" y=\"{F(oy)}\" width=\"{F(w)}\" height=\"{F(h)}\" fill=\"#fff\" stroke=\"#888\"/>");
        foreach (var e in cells)
        {
            var c = SvgCoords.FromArray(e.Points![0]);
            var he = SvgCoords.FromArray(e.Points[1]);
            var x0 = ox + (c.X - MathF.Abs(he.X) + Beam * 0.5f) / Beam * w;
            var x1 = ox + (c.X + MathF.Abs(he.X) + Beam * 0.5f) / Beam * w;
            // SVG Y down: up=OAH at top
            var y0 = oy + (Oah - (c.Y + MathF.Abs(he.Y))) / Oah * h;
            var y1 = oy + (Oah - (c.Y - MathF.Abs(he.Y))) / Oah * h;
            sb.AppendLine($"<rect x=\"{F(Math.Min(x0, x1))}\" y=\"{F(Math.Min(y0, y1))}\" width=\"{F(Math.Abs(x1 - x0))}\" height=\"{F(Math.Max(1, Math.Abs(y1 - y0)))}\" fill=\"#b0b0b0\" stroke=\"#444\" stroke-width=\"0.5\"/>");
        }

        // Spine crosshair
        sb.AppendLine($"<line x1=\"{F(ox + w * 0.5)}\" y1=\"{F(oy)}\" x2=\"{F(ox + w * 0.5)}\" y2=\"{F(oy + h)}\" stroke=\"#0645ad\" stroke-dasharray=\"3 2\"/>");
        sb.AppendLine($"<line x1=\"{F(ox)}\" y1=\"{F(oy + h * 0.5)}\" x2=\"{F(ox + w)}\" y2=\"{F(oy + h * 0.5)}\" stroke=\"#0645ad\" stroke-dasharray=\"3 2\"/>");
        return sb.ToString();
    }

    private static string DrawProfileTick(float zStem, double ox, double oy, double w, double h)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"<rect x=\"{F(ox)}\" y=\"{F(oy)}\" width=\"{F(w)}\" height=\"{F(h)}\" fill=\"#fff\" stroke=\"#888\"/>");
        // Hull box
        sb.AppendLine($"<rect x=\"{F(ox + 8)}\" y=\"{F(oy + 20)}\" width=\"{F(w - 16)}\" height=\"{F(h - 40)}\" fill=\"none\" stroke=\"#666\"/>");
        var x = MapZ(zStem, ox + 8, w - 16);
        sb.AppendLine($"<line x1=\"{F(x)}\" y1=\"{F(oy + 20)}\" x2=\"{F(x)}\" y2=\"{F(oy + h - 20)}\" stroke=\"#c82828\" stroke-width=\"2\"/>");
        sb.AppendLine($"<text x=\"{F(x + 4)}\" y=\"{F(oy + 36)}\" fill=\"#c82828\" font-size=\"10\">z={zStem:0.##}</text>");
        // OAH mid
        sb.AppendLine($"<line x1=\"{F(ox + 8)}\" y1=\"{F(oy + 20 + (h - 40) * 0.5)}\" x2=\"{F(ox + w - 8)}\" y2=\"{F(oy + 20 + (h - 40) * 0.5)}\" stroke=\"#0645ad\" stroke-dasharray=\"4 3\"/>");
        return sb.ToString();
    }

    private static string DrawSpacePlan(List<CadEntity> spaces, double ox, double oy, double w, double h)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"<rect x=\"{F(ox)}\" y=\"{F(oy)}\" width=\"{F(w)}\" height=\"{F(h)}\" fill=\"#fff\" stroke=\"#888\"/>");
        foreach (var space in spaces)
        {
            var pts = space.Points!;
            if (pts.Count < 3)
                continue;
            var poly = new StringBuilder();
            for (var i = 0; i < pts.Count; i++)
            {
                var p = SvgCoords.FromArray(pts[i]);
                var x = ox + (p.X + Beam * 0.5f) / Beam * w;
                // Space footprints store world Z (stern negative-ish / midbody); stern-left sheet.
                var y = oy + (Loa * 0.5f - p.Z) / Loa * h;
                poly.Append(i == 0 ? "M" : "L").Append(F(x)).Append(' ').Append(F(y)).Append(' ');
            }

            poly.Append('Z');
            sb.AppendLine($"<path d=\"{poly}\" fill=\"#d8e0e8\" stroke=\"#333\" stroke-width=\"1\" fill-opacity=\"0.7\"/>");
        }

        return sb.ToString();
    }

    private static double MapZ(float zFromStem, double ox, double w) =>
        ox + (Loa - zFromStem) / Loa * w; // stern left

    private static string F(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);
    private static string Esc(string s) => s.Replace("&", "&amp;").Replace("<", "&lt;");
}
