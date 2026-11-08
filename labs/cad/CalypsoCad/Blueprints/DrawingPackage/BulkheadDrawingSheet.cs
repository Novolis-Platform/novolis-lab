using System.Globalization;
using System.Text;
using Novolis.Cad.Primitives;

namespace CalypsoCad.Blueprints.DrawingPackage;

/// <summary>Bulkhead-only package drawing: plate face + plan/profile refs (CAD model).</summary>
internal static class BulkheadDrawingSheet
{
    public static string Build(
        DrawingEntry entry,
        CadDocument cad,
        string bhId,
        float zStem,
        string note)
    {
        var ship = PackageDrawingChrome.ReadShip(cad);
        var outer = TryPlateOuterFromCad(cad, bhId)
                    ?? LockDraftGeometry.StationVertsAt(ship.Loft, zStem);
        var openings = PackageDrawingChrome.PassageOpeningsAtStation(cad, zStem);

        var sb = PackageDrawingChrome.BeginHtml(
            entry, bhId,
            [("Bulkhead", bhId), ("Station", $"z = {zStem.ToString("0.##", CultureInfo.InvariantCulture)} m from stem")],
            $"{note} · ISO 128-15 · BH plate only");

        PackageDrawingChrome.Label(sb, 28, 32, "A — TRANSVERSE FACE (looking aft) · bulkhead plate only");
        DrawTransversePlate(sb, 28, 42, 520, 360, ship, zStem, outer, openings);

        PackageDrawingChrome.Label(sb, 560, 32, "B — PLAN REF (stern ←)");
        PackageDrawingChrome.DrawPlanHull(sb, 560, 42, 260, 160, ship, (ox, oy, scale) =>
        {
            foreach (var st in ship.Bulkheads)
                PackageDrawingChrome.MarkAthwartStation(
                    sb, ship, ox, oy, scale, st.ZStem, st.Id,
                    focus: string.Equals(st.Id, bhId, StringComparison.OrdinalIgnoreCase),
                    profile: false);
        });

        PackageDrawingChrome.Label(sb, 560, 220, "C — PROFILE REF CL (stern ←)");
        PackageDrawingChrome.DrawProfileHull(sb, 560, 230, 260, 172, ship, showDecks: true, (ox, keelY, scale) =>
        {
            foreach (var st in ship.Bulkheads)
                PackageDrawingChrome.MarkAthwartStation(
                    sb, ship, ox, keelY, scale, st.ZStem, st.Id,
                    focus: string.Equals(st.Id, bhId, StringComparison.OrdinalIgnoreCase),
                    profile: true);
        });

        PackageDrawingChrome.NoteLines(sb, 560, 420,
            "Plate fill = structural BH.",
            "White cutouts = passageway openings.",
            "B/C: hull + this station bold; others ghosted.");

        return PackageDrawingChrome.EndHtml(sb);
    }

    private static void DrawTransversePlate(
        StringBuilder sb, double x, double y, double w, double h,
        PackageDrawingChrome.ShipBasics ship, float zStem,
        IReadOnlyList<(double Y, double Up)> outer,
        IReadOnlyList<(double Y0, double Y1, double U0, double U1)> openings)
    {
        sb.AppendLine($"<rect x=\"{PackageDrawingChrome.F(x)}\" y=\"{PackageDrawingChrome.F(y)}\" width=\"{PackageDrawingChrome.F(w)}\" height=\"{PackageDrawingChrome.F(h)}\" fill=\"#fff\" stroke=\"#333\"/>");
        if (outer.Count < 3)
        {
            sb.AppendLine($"<text x=\"{PackageDrawingChrome.F(x + w * 0.5)}\" y=\"{PackageDrawingChrome.F(y + h * 0.5)}\" text-anchor=\"middle\" fill=\"#c00\">no loft/plate outline</text>");
            return;
        }

        LockDraftGeometry.SampleLoftUpRange(ship.Loft, zStem, ship.Oah, out var upMin, out var upMax);
        var halfB = Math.Max(0.2, outer.Max(v => Math.Abs(v.Y)));
        var height = Math.Max(0.2, upMax - upMin);
        const double padL = 36, padR = 44, padTop = 28, padBot = 36;
        var scale = Math.Min((w - padL - padR) / (halfB * 2), (h - padTop - padBot) / height) * 0.9;
        var cx = x + padL + (w - padL - padR) * 0.5;
        var keelY = y + h - padBot;

        sb.AppendLine($"<path d=\"{PackageDrawingChrome.PathYu(outer, cx, keelY, scale)}\" fill=\"#8a6a4a\" fill-opacity=\"0.92\" stroke=\"#3a2a18\" stroke-width=\"1.4\"/>");
        foreach (var (y0, y1, u0, u1) in openings)
        {
            var xa = cx - y1 * scale;
            var xb = cx - y0 * scale;
            var ua = keelY - u1 * scale;
            var ub = keelY - u0 * scale;
            sb.AppendLine(
                $"<rect x=\"{PackageDrawingChrome.F(Math.Min(xa, xb))}\" y=\"{PackageDrawingChrome.F(Math.Min(ua, ub))}\" width=\"{PackageDrawingChrome.F(Math.Abs(xb - xa))}\" height=\"{PackageDrawingChrome.F(Math.Abs(ub - ua))}\" fill=\"#fff\" stroke=\"#222\" stroke-width=\"0.9\"/>");
        }

        foreach (var (name, elev) in new[] { ("−1", ship.DkM1), ("0", ship.Dk0), ("+1", ship.Dk1) })
        {
            if (elev < upMin - 0.05 || elev > upMax + 0.05)
                continue;
            var uy = keelY - elev * scale;
            var hb = LockDraftGeometry.LoftWaterlineHalfBeam(ship.Loft, zStem, elev) * scale;
            sb.AppendLine($"<line x1=\"{PackageDrawingChrome.F(cx - hb)}\" y1=\"{PackageDrawingChrome.F(uy)}\" x2=\"{PackageDrawingChrome.F(cx + hb)}\" y2=\"{PackageDrawingChrome.F(uy)}\" stroke=\"#444\" stroke-width=\"0.7\" stroke-dasharray=\"4 2\"/>");
            sb.AppendLine($"<text x=\"{PackageDrawingChrome.F(cx + hb + 4)}\" y=\"{PackageDrawingChrome.F(uy + 3)}\" font-size=\"9\" fill=\"#555\">DK{name}</text>");
        }

        var crownY = keelY - upMax * scale;
        var keelLine = keelY - upMin * scale;
        sb.AppendLine($"<line x1=\"{PackageDrawingChrome.F(cx)}\" y1=\"{PackageDrawingChrome.F(crownY)}\" x2=\"{PackageDrawingChrome.F(cx)}\" y2=\"{PackageDrawingChrome.F(keelLine)}\" stroke=\"#0645ad\" stroke-dasharray=\"5 3\" stroke-width=\"0.85\"/>");
        PackageDrawingChrome.DimH(sb, cx - halfB * scale, keelLine + 10, cx + halfB * scale, $"B {PackageDrawingChrome.Nice(halfB * 2)}");
        PackageDrawingChrome.DimV(sb, cx + halfB * scale + 14, keelLine, crownY, $"H {PackageDrawingChrome.Nice(height)}");
        sb.AppendLine($"<text x=\"{PackageDrawingChrome.F(cx)}\" y=\"{PackageDrawingChrome.F(y + 16)}\" text-anchor=\"middle\" font-size=\"11\" fill=\"#222\">z={zStem.ToString("0.##", CultureInfo.InvariantCulture)} m · {LockDraftGeometry.ScaleNote(scale)}</text>");
        sb.AppendLine($"<text x=\"{PackageDrawingChrome.F(cx - halfB * scale)}\" y=\"{PackageDrawingChrome.F(y + h - 8)}\" font-size=\"10\" fill=\"#444\">P</text>");
        sb.AppendLine($"<text x=\"{PackageDrawingChrome.F(cx + halfB * scale)}\" y=\"{PackageDrawingChrome.F(y + h - 8)}\" text-anchor=\"end\" font-size=\"10\" fill=\"#444\">S</text>");
    }

    private static List<(double Y, double Up)>? TryPlateOuterFromCad(CadDocument cad, string bhId)
    {
        var needle = $"bh-{bhId}-plate";
        var ent = cad.Entities.FirstOrDefault(e =>
            e.Name is not null
            && e.Name.Equals(needle, StringComparison.OrdinalIgnoreCase)
            && e.MeshVertices is { Count: >= 3 });
        if (ent?.MeshVertices is null)
            return null;

        var pts = new List<(double Y, double Up)>();
        foreach (var v in ent.MeshVertices)
        {
            if (v.Length < 3)
                continue;
            pts.Add((v[0], v[1]));
        }

        return pts.Count < 3 ? null : PackageDrawingChrome.ConvexHullYu(pts);
    }
}
