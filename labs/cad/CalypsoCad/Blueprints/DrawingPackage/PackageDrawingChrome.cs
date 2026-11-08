using System.Globalization;
using System.Net;
using System.Text;
using CalypsoCad.Structure;
using Novolis.Cad.Primitives;

namespace CalypsoCad.Blueprints.DrawingPackage;

/// <summary>Shared ISO chrome + SVG primitives for focused package drawings (CAD model only).</summary>
internal static class PackageDrawingChrome
{
    public sealed record ShipBasics(
        double Loa,
        double Beam,
        double Oah,
        double DkM1,
        double Dk0,
        double Dk1,
        IReadOnlyList<LockDraftGeometry.LoftStation> Loft,
        IReadOnlyList<CalypsoBulkheads.Station> Bulkheads);

    public static ShipBasics ReadShip(CadDocument cad)
    {
        CadJsonStructureQuery.EnsureHullGeometry(cad);
        var loa = CadJsonStructureQuery.Loa(cad);
        var decks = CadJsonStructureQuery.Decks(cad);
        return new ShipBasics(
            loa,
            CadJsonStructureQuery.Beam(cad),
            CadJsonStructureQuery.Oah(cad),
            decks.M1,
            decks.D0,
            decks.D1,
            CadJsonStructureQuery.HullLoft(cad),
            CalypsoBulkheads.BuildSchedule(loa, CadJsonStructureQuery.StationMap(cad)));
    }

    public static StringBuilder BeginHtml(DrawingEntry entry, string subject, IReadOnlyList<(string Key, string Value)> blockRows, string subtitle)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html><html lang=\"en\"><head><meta charset=\"utf-8\"/>");
        sb.AppendLine($"<title>{Esc(entry.DrawingNumber)} — {Esc(subject)}</title>");
        sb.AppendLine("""
            <style>
            body{margin:0;background:#ebe6dc;font:13px/1.35 "Segoe UI",system-ui,sans-serif;color:#1a1f24}
            .wrap{padding:1rem 1.25rem 1.5rem}
            .tb{float:right;width:300px;border:1px solid #111;background:#fff;margin:0 0 1rem 1rem}
            .tb table{width:100%;border-collapse:collapse}
            .tb th,.tb td{padding:.35rem .45rem;border-bottom:1px solid #ccc;text-align:left;font-size:12px}
            .tb th{width:34%;background:#e8eee9}
            h1{font:700 1.35rem/1.2 system-ui;margin:0 0 .35rem}
            .meta{color:#5a6570;font-size:12px;margin:0 0 .8rem;clear:both}
            svg{display:block;width:100%;height:auto;background:#fff;border:1px solid #333}
            </style>
            """);
        sb.AppendLine("</head><body><div class=\"wrap\">");
        sb.AppendLine("<aside class=\"tb\"><table>");
        sb.AppendLine($"<tr><th>Drawing</th><td>{Esc(entry.DrawingNumber)}</td></tr>");
        sb.AppendLine($"<tr><th>Label</th><td>{Esc(entry.Label)}</td></tr>");
        foreach (var (k, v) in blockRows)
            sb.AppendLine($"<tr><th>{Esc(k)}</th><td>{Esc(v)}</td></tr>");
        sb.AppendLine("<tr><th>Rev</th><td>A</td></tr>");
        sb.AppendLine("</table></aside>");
        sb.AppendLine($"<h1>{Esc(entry.Title)}</h1>");
        sb.AppendLine($"<p class=\"meta\">{Esc(subtitle)}</p>");
        sb.AppendLine("<svg viewBox=\"0 0 841 594\" xmlns=\"http://www.w3.org/2000/svg\">");
        sb.AppendLine("<rect x=\"10\" y=\"10\" width=\"821\" height=\"574\" fill=\"none\" stroke=\"#111\" stroke-width=\"1.2\"/>");
        return sb;
    }

    public static string EndHtml(StringBuilder sb)
    {
        sb.AppendLine("</svg></div></body></html>");
        return sb.ToString();
    }

    public static void Label(StringBuilder sb, double x, double y, string text) =>
        sb.AppendLine($"<text x=\"{F(x)}\" y=\"{F(y)}\" font-size=\"11\" font-weight=\"600\" fill=\"#222\">{Esc(text)}</text>");

    public static void NoteLines(StringBuilder sb, double x, double y, params string[] lines)
    {
        Label(sb, x, y, "NOTES");
        for (var i = 0; i < lines.Length; i++)
            sb.AppendLine($"<text x=\"{F(x)}\" y=\"{F(y + 20 + i * 14)}\" font-size=\"10\" fill=\"#333\">{Esc(lines[i])}</text>");
    }

    public static void DimH(StringBuilder sb, double x0, double y, double x1, string label)
    {
        sb.AppendLine($"<line x1=\"{F(x0)}\" y1=\"{F(y)}\" x2=\"{F(x1)}\" y2=\"{F(y)}\" stroke=\"#333\" stroke-width=\"0.6\"/>");
        sb.AppendLine($"<text x=\"{F((x0 + x1) * 0.5)}\" y=\"{F(y + 10)}\" text-anchor=\"middle\" font-size=\"9\" fill=\"#333\">{Esc(label)}</text>");
    }

    public static void DimV(StringBuilder sb, double x, double y0, double y1, string label)
    {
        sb.AppendLine($"<line x1=\"{F(x)}\" y1=\"{F(y0)}\" x2=\"{F(x)}\" y2=\"{F(y1)}\" stroke=\"#333\" stroke-width=\"0.6\"/>");
        sb.AppendLine($"<text x=\"{F(x + 4)}\" y=\"{F((y0 + y1) * 0.5)}\" font-size=\"9\" fill=\"#333\">{Esc(label)}</text>");
    }

    public static void DrawPlanHull(
        StringBuilder sb, double x, double y, double w, double h, ShipBasics ship,
        Action<double, double, double>? markStations)
    {
        sb.AppendLine($"<rect x=\"{F(x)}\" y=\"{F(y)}\" width=\"{F(w)}\" height=\"{F(h)}\" fill=\"#fff\" stroke=\"#333\"/>");
        var scale = LockDraftGeometry.SharedPlanScale(ship.Loa, ship.Beam, w, h);
        var ox = x + LockDraftGeometry.PlanPadL
                 + ((w - LockDraftGeometry.PlanPadL - LockDraftGeometry.PlanPadR) - ship.Loa * scale) * 0.5;
        var oy = y + LockDraftGeometry.PlanPadTop
                 + (h - LockDraftGeometry.PlanPadTop - LockDraftGeometry.PlanPadBot) * 0.5;
        sb.AppendLine($"<path d=\"{HullPlanPath(ship.Loft, ship.Loa, ox, oy, scale)}\" fill=\"#f4f6f8\" stroke=\"#111\" stroke-width=\"1.1\"/>");
        sb.AppendLine($"<line x1=\"{F(ox)}\" y1=\"{F(oy)}\" x2=\"{F(ox + ship.Loa * scale)}\" y2=\"{F(oy)}\" stroke=\"#0645ad\" stroke-dasharray=\"4 2\" stroke-width=\"0.6\"/>");
        markStations?.Invoke(ox, oy, scale);
        sb.AppendLine($"<text x=\"{F(ox)}\" y=\"{F(y + h - 5)}\" font-size=\"8\" fill=\"#555\">STERN</text>");
        sb.AppendLine($"<text x=\"{F(ox + ship.Loa * scale)}\" y=\"{F(y + h - 5)}\" text-anchor=\"end\" font-size=\"8\" fill=\"#555\">BOW</text>");
    }

    public static void DrawProfileHull(
        StringBuilder sb, double x, double y, double w, double h, ShipBasics ship,
        bool showDecks, Action<double, double, double>? markStations)
    {
        sb.AppendLine($"<rect x=\"{F(x)}\" y=\"{F(y)}\" width=\"{F(w)}\" height=\"{F(h)}\" fill=\"#fff\" stroke=\"#333\"/>");
        const double padL = 28, padR = 12, padTop = 12, padBot = 18;
        var scale = Math.Min((w - padL - padR) / ship.Loa, (h - padTop - padBot) / ship.Oah) * 0.9;
        var ox = x + padL + ((w - padL - padR) - ship.Loa * scale) * 0.5;
        var keelY = y + padTop + ((h - padTop - padBot) + ship.Oah * scale) * 0.5;

        if (ship.Loft.Count >= 2)
        {
            var top = new StringBuilder("M");
            var bot = new StringBuilder();
            for (var i = 0; i < ship.Loft.Count; i++)
            {
                var st = ship.Loft[i];
                var sx = LockDraftGeometry.ShipX(ox, scale, ship.Loa, st.Z);
                LockDraftGeometry.SampleLoftUpRange(ship.Loft, st.Z, ship.Oah, out var u0, out var u1);
                top.Append(i == 0 ? $" {F(sx)} {F(keelY - u1 * scale)}" : $" L {F(sx)} {F(keelY - u1 * scale)}");
                bot.Insert(0, $" L {F(sx)} {F(keelY - u0 * scale)}");
            }

            sb.AppendLine($"<path d=\"{top}{bot} Z\" fill=\"#f4f6f8\" stroke=\"#111\" stroke-width=\"1.05\"/>");
        }

        if (showDecks)
        {
            foreach (var elev in new[] { ship.DkM1, ship.Dk0, ship.Dk1 })
                sb.AppendLine($"<line x1=\"{F(ox)}\" y1=\"{F(keelY - elev * scale)}\" x2=\"{F(ox + ship.Loa * scale)}\" y2=\"{F(keelY - elev * scale)}\" stroke=\"#aaa\" stroke-width=\"0.5\" stroke-dasharray=\"3 2\"/>");
        }

        markStations?.Invoke(ox, keelY, scale);
        sb.AppendLine($"<text x=\"{F(ox)}\" y=\"{F(y + h - 5)}\" font-size=\"8\" fill=\"#555\">STERN</text>");
        sb.AppendLine($"<text x=\"{F(ox + ship.Loa * scale)}\" y=\"{F(y + h - 5)}\" text-anchor=\"end\" font-size=\"8\" fill=\"#555\">BOW</text>");
    }

    public static void MarkAthwartStation(
        StringBuilder sb, ShipBasics ship, double ox, double oyOrKeel, double scale,
        float zStem, string? label, bool focus, bool profile)
    {
        var sx = LockDraftGeometry.ShipX(ox, scale, ship.Loa, zStem);
        LockDraftGeometry.SampleLoftUpRange(ship.Loft, zStem, ship.Oah, out var u0, out var u1);
        var stroke = focus ? "#c82828" : "#ccc";
        var sw = focus ? "2.2" : "0.7";
        if (profile)
        {
            sb.AppendLine($"<line x1=\"{F(sx)}\" y1=\"{F(oyOrKeel - u1 * scale)}\" x2=\"{F(sx)}\" y2=\"{F(oyOrKeel - u0 * scale)}\" stroke=\"{stroke}\" stroke-width=\"{sw}\"/>");
            if (focus && label is not null)
                sb.AppendLine($"<text x=\"{F(sx + 3)}\" y=\"{F(oyOrKeel - u1 * scale - 3)}\" font-size=\"9\" fill=\"#c82828\">{Esc(label)}</text>");
        }
        else
        {
            var hb = LockDraftGeometry.LoftHalfBeam(ship.Loft, zStem) * scale;
            sb.AppendLine($"<line x1=\"{F(sx)}\" y1=\"{F(oyOrKeel - hb)}\" x2=\"{F(sx)}\" y2=\"{F(oyOrKeel + hb)}\" stroke=\"{stroke}\" stroke-width=\"{sw}\"/>");
            if (focus && label is not null)
                sb.AppendLine($"<text x=\"{F(sx + 3)}\" y=\"{F(oyOrKeel - hb - 3)}\" font-size=\"9\" fill=\"#c82828\">{Esc(label)}</text>");
        }
    }

    public static List<(double Y0, double Y1, double U0, double U1)> PassageOpeningsAtStation(
        CadDocument cad, float zStem)
    {
        var list = new List<(double, double, double, double)>();
        foreach (var sp in CadJsonStructureQuery.Spaces(cad))
        {
            if (!VolumeCirculation.IsShirtSleeves(sp.Id)
                && !sp.Id.StartsWith("AIRLOCK_", StringComparison.OrdinalIgnoreCase))
                continue;
            if (zStem < sp.Z0 - 0.05f || zStem > sp.Z1 + 0.05f)
                continue;

            var sill = Math.Max(sp.Up0, Math.Min(sp.Up0 + 0.15f, sp.Up1 - 0.5f));
            var head = Math.Min(sp.Up1, sill + 2.15f);
            if (head - sill < 0.8f)
                continue;
            list.Add((Math.Min(sp.Y0, sp.Y1), Math.Max(sp.Y0, sp.Y1), sill, head));
        }

        return list;
    }

    public static bool TryReadCompartment(
        CadDocument cad, string id,
        out double z0, out double z1, out double y0, out double y1, out double u0, out double u1,
        out string? deckTag, out List<(double Y, double Z)>? planRing)
    {
        z0 = z1 = y0 = y1 = u0 = u1 = 0;
        deckTag = null;
        planRing = null;
        if (!CadJsonStructureQuery.TryGetSpace(cad, id, out var sp))
            return false;

        z0 = sp.Z0;
        z1 = sp.Z1;
        y0 = sp.Y0;
        y1 = sp.Y1;
        u0 = sp.Up0;
        u1 = sp.Up1;
        deckTag = sp.Deck.ToString(CultureInfo.InvariantCulture);
        if (sp.PlanRing.Count >= 3)
            planRing = sp.PlanRing.Select(p => ((double)p.Y, (double)p.ZStem)).ToList();
        return true;
    }

    public static string HullPlanPath(
        IReadOnlyList<LockDraftGeometry.LoftStation> loft, double loa, double ox, double oy, double scale)
    {
        if (loft.Count == 0)
            return $"M {F(ox)} {F(oy)} L {F(ox + loa * scale)} {F(oy)}";

        var sb = new StringBuilder("M");
        for (var i = 0; i < loft.Count; i++)
        {
            var st = loft[i];
            var sx = LockDraftGeometry.ShipX(ox, scale, loa, st.Z);
            var hb = st.HalfBeam * scale;
            sb.Append(i == 0 ? $" {F(sx)} {F(oy - hb)}" : $" L {F(sx)} {F(oy - hb)}");
        }

        for (var i = loft.Count - 1; i >= 0; i--)
        {
            var st = loft[i];
            var sx = LockDraftGeometry.ShipX(ox, scale, loa, st.Z);
            var hb = st.HalfBeam * scale;
            sb.Append($" L {F(sx)} {F(oy + hb)}");
        }

        sb.Append(" Z");
        return sb.ToString();
    }

    public static string PathYu(IReadOnlyList<(double Y, double Up)> verts, double cx, double keelY, double scale)
    {
        var sb = new StringBuilder("M");
        for (var i = 0; i < verts.Count; i++)
        {
            var (vy, vu) = verts[i];
            sb.Append(i == 0 ? " " : " L ");
            sb.Append($"{F(cx - vy * scale)} {F(keelY - vu * scale)}");
        }

        sb.Append(" Z");
        return sb.ToString();
    }

    public static List<(double Y, double Up)> ConvexHullYu(List<(double Y, double Up)> pts)
    {
        var sorted = pts.Distinct().OrderBy(p => p.Y).ThenBy(p => p.Up).ToList();
        if (sorted.Count <= 2)
            return sorted;

        static double Cross((double Y, double Up) o, (double Y, double Up) a, (double Y, double Up) b) =>
            (a.Y - o.Y) * (b.Up - o.Up) - (a.Up - o.Up) * (b.Y - o.Y);

        var lower = new List<(double Y, double Up)>();
        foreach (var p in sorted)
        {
            while (lower.Count >= 2 && Cross(lower[^2], lower[^1], p) <= 0)
                lower.RemoveAt(lower.Count - 1);
            lower.Add(p);
        }

        var upper = new List<(double Y, double Up)>();
        for (var i = sorted.Count - 1; i >= 0; i--)
        {
            var p = sorted[i];
            while (upper.Count >= 2 && Cross(upper[^2], upper[^1], p) <= 0)
                upper.RemoveAt(upper.Count - 1);
            upper.Add(p);
        }

        lower.RemoveAt(lower.Count - 1);
        upper.RemoveAt(upper.Count - 1);
        lower.AddRange(upper);
        return lower;
    }

    public static string Nice(double v) =>
        v.ToString(Math.Abs(v) >= 10 ? "0.0" : "0.##", CultureInfo.InvariantCulture) + " m";

    public static string F(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);

    public static string Esc(string s) => WebUtility.HtmlEncode(s);
}
