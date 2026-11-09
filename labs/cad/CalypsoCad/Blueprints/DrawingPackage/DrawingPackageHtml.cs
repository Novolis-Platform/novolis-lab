using System.Globalization;
using System.Net;
using System.Text;

namespace CalypsoCad.Blueprints.DrawingPackage;

/// <summary>ISO 7200 / class-society style HTML chrome for the drawing package.</summary>
internal static class DrawingPackageHtml
{
    private const string Rev = PackageDrawingChrome.PackageRev;
    private const string StdNote = PackageDrawingChrome.ClassStyleDisclaimer;

    /// <param name="nestDepth">
    /// Directory depth of this HTML under the package root (0 = <c>index.html</c>,
    /// 1 = <c>100-general/CAL-100-001-…</c>). Prefixes relative hrefs with <c>../</c>.
    /// </param>
    public static string PackageIndex(
        IReadOnlyList<DrawingEntry> drawings,
        IReadOnlyList<DrawingPackageBomLine> bom,
        int nestDepth = 0)
    {
        var prefix = nestDepth <= 0
            ? ""
            : string.Concat(Enumerable.Repeat("../", nestDepth));
        var sb = new StringBuilder();
        Head(sb, "CAL-100-001 — Calypso drawing package");
        sb.AppendLine("<body>");
        sb.AppendLine("<header class=\"hero\">");
        sb.AppendLine("<p class=\"eyebrow\">CALYPSO · TRAMP FREIGHTER · DRAWING PACKAGE</p>");
        sb.AppendLine("<h1>CAL-100-001 Drawing package</h1>");
        sb.AppendLine("<p class=\"lede\">Class-style numbered register (elevated toward ISO 7200 / 5457 / 128-15): structure " +
                      "(bulkheads, frames, deck plates, members) and people-movement volumes.</p>");
        sb.AppendLine($"<p class=\"meta\">Rev {Rev} · {drawings.Count} drawings · {bom.Count} BOM lines · {StdNote}</p>");
        sb.AppendLine("<p class=\"meta\"><a href=\"smart/GA-001.html\">Smart GA companion (CadBlueprint)</a> · " +
                      "<a href=\"calypso.cadblueprint.json\">calypso.cadblueprint.json</a></p>");
        sb.AppendLine("</header>");

        sb.AppendLine("<nav class=\"cats\">");
        foreach (var cat in DrawingPackageCategories.All)
        {
            var count = drawings.Count(d => d.Category == cat.Id);
            sb.AppendLine(
                $"<a href=\"{WebUtility.HtmlEncode(prefix + cat.FolderName)}/index.html\"><strong>{cat.SystemCode}</strong> {WebUtility.HtmlEncode(cat.Title)} <span>{count}</span></a>");
        }

        sb.AppendLine("</nav>");

        sb.AppendLine("<section><h2>Drawing list</h2>");
        sb.AppendLine("<table><thead><tr><th>No.</th><th>Label</th><th>Title</th><th>Category</th><th>Volume</th></tr></thead><tbody>");
        foreach (var d in drawings.OrderBy(d => (int)d.Category).ThenBy(d => d.Sequence))
        {
            var cat = DrawingPackageCategories.Get(d.Category);
            sb.AppendLine(
                $"<tr><td><a href=\"{WebUtility.HtmlEncode(prefix + d.RelativePath)}\">{WebUtility.HtmlEncode(d.DrawingNumber)}</a></td>" +
                $"<td>{WebUtility.HtmlEncode(d.Label)}</td>" +
                $"<td>{WebUtility.HtmlEncode(d.Title)}</td>" +
                $"<td>{WebUtility.HtmlEncode(cat.Title)}</td>" +
                $"<td>{WebUtility.HtmlEncode(VolumeCirculation.DisplayName(d.VolumeKind))}</td></tr>");
        }

        sb.AppendLine("</tbody></table></section>");
        Foot(sb);
        return sb.ToString();
    }

    public static string CategoryIndex(DrawingCategorySpec cat, IReadOnlyList<DrawingEntry> drawings)
    {
        var sb = new StringBuilder();
        var depth = cat.FolderName.Count(c => c == '/') + 1;
        var root = string.Concat(Enumerable.Repeat("../", depth));
        Head(sb, $"{cat.SystemCode} — {cat.Title}");
        sb.AppendLine("<body>");
        sb.AppendLine($"<p class=\"crumb\"><a href=\"{root}index.html\">Package</a> / {WebUtility.HtmlEncode(cat.Title)}</p>");
        sb.AppendLine($"<h1>CAL-{cat.SystemCode} · {WebUtility.HtmlEncode(cat.Title)}</h1>");
        sb.AppendLine($"<p class=\"lede\">{WebUtility.HtmlEncode(cat.Description)}</p>");
        sb.AppendLine($"<p class=\"meta\">Discipline: {WebUtility.HtmlEncode(cat.Discipline)} · Rev {Rev}</p>");
        sb.AppendLine("<table><thead><tr><th>No.</th><th>Label</th><th>Title</th></tr></thead><tbody>");
        foreach (var d in drawings.Where(d => d.Category == cat.Id).OrderBy(d => d.Sequence))
        {
            var file = Path.GetFileName(d.RelativePath);
            sb.AppendLine(
                $"<tr><td><a href=\"{WebUtility.HtmlEncode(file)}\">{WebUtility.HtmlEncode(d.DrawingNumber)}</a></td>" +
                $"<td>{WebUtility.HtmlEncode(d.Label)}</td>" +
                $"<td>{WebUtility.HtmlEncode(d.Title)}</td></tr>");
        }

        sb.AppendLine("</tbody></table>");
        Foot(sb);
        return sb.ToString();
    }

    public static string DrawingSheet(
        DrawingEntry entry,
        string? detailHref,
        IReadOnlyList<string>? memberChunk = null)
    {
        var cat = DrawingPackageCategories.Get(entry.Category);
        var depth = entry.RelativePath.Count(c => c == '/') ;
        var root = string.Concat(Enumerable.Repeat("../", depth));
        var sb = new StringBuilder();
        Head(sb, $"{entry.DrawingNumber} — {entry.Label}");
        sb.AppendLine("<body>");
        sb.AppendLine("<div class=\"sheet\">");
        TitleBlock(sb, entry, cat);
        sb.AppendLine($"<p class=\"crumb\"><a href=\"{root}index.html\">Package</a> / " +
                      $"<a href=\"index.html\">{WebUtility.HtmlEncode(cat.Title)}</a> / {WebUtility.HtmlEncode(entry.DrawingNumber)}</p>");
        sb.AppendLine($"<h1>{WebUtility.HtmlEncode(entry.Title)}</h1>");
        sb.AppendLine("<dl class=\"meta-grid\">");
        Meta(sb, "Drawing number", entry.DrawingNumber);
        Meta(sb, "Label", entry.Label);
        Meta(sb, "Category", $"{cat.SystemCode} · {cat.Title}");
        Meta(sb, "Volume class", VolumeCirculation.DisplayName(entry.VolumeKind));
        Meta(sb, "Subject id", entry.SubjectId ?? "—");
        Meta(sb, "Tags", string.Join(", ", entry.Tags));
        sb.AppendLine("</dl>");

        if (memberChunk is { Count: > 0 })
        {
            sb.AppendLine("<h2>Member register</h2><ol class=\"members\">");
            foreach (var name in memberChunk)
                sb.AppendLine($"<li><code>{WebUtility.HtmlEncode(name)}</code></li>");
            sb.AppendLine("</ol>");
        }

        if (!string.IsNullOrWhiteSpace(detailHref))
        {
            sb.AppendLine("<h2>Geometry sheet</h2>");
            sb.AppendLine(
                $"<p>Authoritative SVG detail: <a href=\"{WebUtility.HtmlEncode(detailHref)}\">{WebUtility.HtmlEncode(Path.GetFileName(detailHref))}</a></p>");
            sb.AppendLine(
                $"<iframe class=\"detail\" title=\"detail\" src=\"{WebUtility.HtmlEncode(detailHref)}\"></iframe>");
        }
        else if (entry.Category == DrawingCategoryId.Bom)
        {
            sb.AppendLine("<p class=\"lede\">See sibling BOM HTML/JSON in this folder for line items.</p>");
        }

        sb.AppendLine($"<p class=\"std\">{WebUtility.HtmlEncode(StdNote)}</p>");
        sb.AppendLine("</div>");
        Foot(sb);
        return sb.ToString();
    }

    public static string BomSheet(
        string drawingNumber,
        string title,
        IReadOnlyList<DrawingPackageBomLine> lines)
    {
        var sb = new StringBuilder();
        Head(sb, $"{drawingNumber} — {title}");
        sb.AppendLine("<body>");
        sb.AppendLine($"<p class=\"crumb\"><a href=\"../index.html\">Package</a> / BOM / {WebUtility.HtmlEncode(drawingNumber)}</p>");
        sb.AppendLine($"<h1>{WebUtility.HtmlEncode(drawingNumber)} · {WebUtility.HtmlEncode(title)}</h1>");
        sb.AppendLine($"<p class=\"meta\">Rev {Rev} · {lines.Count} lines · deck mass = CAD steel box volumes × density (faces+peri+ribs); unresolved masses marked explicitly</p>");
        sb.AppendLine("<table><thead><tr>" +
                      "<th>Line</th><th>Category</th><th>Id</th><th>Description</th><th>Qty</th>" +
                      "<th>Area m²</th><th>Mass kg</th><th>Material</th><th>Drawing</th>" +
                      "</tr></thead><tbody>");
        foreach (var l in lines)
        {
            sb.AppendLine(
                $"<tr><td>{WebUtility.HtmlEncode(l.LineId)}</td>" +
                $"<td>{WebUtility.HtmlEncode(l.Category)}</td>" +
                $"<td><code>{WebUtility.HtmlEncode(l.PartOrSpaceId)}</code></td>" +
                $"<td>{WebUtility.HtmlEncode(l.Description)}</td>" +
                $"<td>{l.Qty.ToString("0.###", CultureInfo.InvariantCulture)}</td>" +
                $"<td>{(l.AreaM2 is { } a ? a.ToString("0.##", CultureInfo.InvariantCulture) : "—")}</td>" +
                $"<td>{(l.MassKg is { } m ? m.ToString("0.#", CultureInfo.InvariantCulture) : "—")}</td>" +
                $"<td>{WebUtility.HtmlEncode(l.Material ?? "—")}</td>" +
                $"<td>{WebUtility.HtmlEncode(l.DrawingNumber ?? "—")}</td></tr>");
        }

        sb.AppendLine("</tbody></table>");
        Foot(sb);
        return sb.ToString();
    }

    private static void TitleBlock(StringBuilder sb, DrawingEntry entry, DrawingCategorySpec cat)
    {
        sb.AppendLine("<aside class=\"tb\">");
        sb.AppendLine("<table>");
        sb.AppendLine($"<tr><th>Owner</th><td>Novolis · CalypsoCad</td><th>Rev</th><td>{Rev}</td></tr>");
        sb.AppendLine($"<tr><th>Drawing</th><td colspan=\"3\">{WebUtility.HtmlEncode(entry.DrawingNumber)}</td></tr>");
        sb.AppendLine($"<tr><th>Title</th><td colspan=\"3\">{WebUtility.HtmlEncode(entry.Title)}</td></tr>");
        sb.AppendLine($"<tr><th>System</th><td>{cat.SystemCode}</td><th>Label</th><td>{WebUtility.HtmlEncode(entry.Label)}</td></tr>");
        sb.AppendLine("</table></aside>");
    }

    private static void Meta(StringBuilder sb, string k, string v) =>
        sb.AppendLine($"<div><dt>{WebUtility.HtmlEncode(k)}</dt><dd>{WebUtility.HtmlEncode(v)}</dd></div>");

    private static void Head(StringBuilder sb, string title)
    {
        sb.AppendLine("<!DOCTYPE html><html lang=\"en\"><head><meta charset=\"utf-8\"/>");
        sb.AppendLine($"<title>{WebUtility.HtmlEncode(title)}</title>");
        sb.AppendLine("""
            <style>
            :root { --ink:#1a1f24; --muted:#5a6570; --line:#c9d0d6; --paper:#f7f5f1; --accent:#2f5d50; --card:#fff; }
            * { box-sizing:border-box; }
            body { margin:0; font:15px/1.45 "Segoe UI",system-ui,sans-serif; color:var(--ink); background:
              radial-gradient(1200px 600px at 10% -10%, #d9e6e1 0%, transparent 55%),
              linear-gradient(180deg, #ebe6dc, var(--paper)); }
            .hero { padding:2.5rem 2rem 1.5rem; border-bottom:1px solid var(--line); background:rgba(255,255,255,.55); }
            .eyebrow { letter-spacing:.14em; text-transform:uppercase; font-size:11px; color:var(--accent); margin:0 0 .5rem; }
            h1 { font:700 1.85rem/1.15 "Segoe UI Semibold",system-ui,sans-serif; margin:0 0 .6rem; }
            .lede { max-width:62rem; color:var(--muted); }
            .meta,.crumb,.std { color:var(--muted); font-size:12px; }
            .cats { display:grid; grid-template-columns:repeat(auto-fill,minmax(220px,1fr)); gap:.6rem; padding:1.2rem 2rem; }
            .cats a { display:flex; justify-content:space-between; gap:.5rem; padding:.85rem 1rem; background:var(--card);
              border:1px solid var(--line); text-decoration:none; color:var(--ink); }
            .cats a:hover { border-color:var(--accent); }
            .cats span { color:var(--muted); }
            section { padding:0 2rem 2rem; }
            table { width:100%; border-collapse:collapse; background:var(--card); border:1px solid var(--line); }
            th,td { padding:.45rem .55rem; border-bottom:1px solid var(--line); text-align:left; vertical-align:top; font-size:13px; }
            th { background:#eef2f0; font-size:11px; letter-spacing:.04em; text-transform:uppercase; color:var(--muted); }
            a { color:var(--accent); }
            .sheet { padding:1.2rem 2rem 2rem; }
            .tb { float:right; width:min(360px,100%); margin:0 0 1rem 1rem; background:var(--card); border:1px solid var(--ink); }
            .tb table { border:0; }
            .tb th { width:28%; background:#e8eee9; color:var(--ink); text-transform:none; letter-spacing:0; }
            .meta-grid { display:grid; grid-template-columns:repeat(auto-fill,minmax(200px,1fr)); gap:.5rem 1rem; clear:both; }
            .meta-grid dt { font-size:11px; text-transform:uppercase; letter-spacing:.06em; color:var(--muted); }
            .meta-grid dd { margin:.1rem 0 0; font-weight:600; }
            iframe.detail { width:100%; height:72vh; border:1px solid var(--line); background:#fff; margin-top:.5rem; }
            .members { columns:2; gap:1.5rem; }
            code { font:12px/1.3 ui-monospace,Consolas,monospace; }
            footer { padding:1rem 2rem 2rem; color:var(--muted); font-size:12px; }
            body > h1, body > .lede, body > .meta, body > .crumb, body > table, body > p { margin-left:2rem; margin-right:2rem; }
            </style>
            """);
        sb.AppendLine("</head>");
    }

    private static void Foot(StringBuilder sb) =>
        sb.AppendLine("<footer>CalypsoCad drawing package · ISO 5457 / 7200 chrome · Novolis Platform</footer></body></html>");
}
