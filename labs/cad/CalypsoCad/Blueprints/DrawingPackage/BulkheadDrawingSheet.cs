using System.Globalization;
using System.Text;
using Novolis.Cad.Primitives;

namespace CalypsoCad.Blueprints.DrawingPackage;

/// <summary>Projects the CAD BH plate mesh (openings already cut) — not a loft fill + fake holes.</summary>
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
        var hasMesh = CadJsonStructureQuery.TryBhPlateMesh(cad, bhId, out var verts, out var indices);

        var sb = PackageDrawingChrome.BeginHtml(
            entry, bhId,
            [("Bulkhead", bhId),
             ("Station", $"z = {zStem.ToString("0.##", CultureInfo.InvariantCulture)} m from stem"),
             ("CAD", hasMesh ? $"bh-{bhId}-plate mesh" : "MISSING plate mesh")],
            $"{note} · ISO 128-15 · CAD plate projection");

        PackageDrawingChrome.Label(sb, 28, 32, "A — TRANSVERSE · CAD plate mesh (looking aft)");
        DrawCadPlate(sb, 28, 42, 520, 360, ship, zStem, hasMesh, verts, indices);

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
            "Fill = triangles from bh-*-plate mesh.",
            "Openings are mesh voids (not overlaid rects).",
            "B/C: station location on hull only.");

        return PackageDrawingChrome.EndHtml(sb);
    }

    private static void DrawCadPlate(
        StringBuilder sb, double x, double y, double w, double h,
        PackageDrawingChrome.ShipBasics ship, float zStem,
        bool hasMesh,
        IReadOnlyList<float[]> verts,
        IReadOnlyList<int> indices)
    {
        sb.AppendLine($"<rect x=\"{PackageDrawingChrome.F(x)}\" y=\"{PackageDrawingChrome.F(y)}\" width=\"{PackageDrawingChrome.F(w)}\" height=\"{PackageDrawingChrome.F(h)}\" fill=\"#fff\" stroke=\"#333\"/>");
        if (!hasMesh || verts.Count < 3 || indices.Count < 3)
        {
            sb.AppendLine($"<text x=\"{PackageDrawingChrome.F(x + w * 0.5)}\" y=\"{PackageDrawingChrome.F(y + h * 0.5)}\" text-anchor=\"middle\" fill=\"#c00\">no CAD bh-*-plate mesh</text>");
            return;
        }

        LockDraftGeometry.SampleLoftUpRange(ship.Loft, zStem, ship.Oah, out var upMin, out var upMax);
        var loft = LockDraftGeometry.StationVertsAt(ship.Loft, zStem);
        var halfB = loft.Count > 0 ? Math.Max(0.2, loft.Max(v => Math.Abs(v.Y))) : 10.0;
        var height = Math.Max(0.2, upMax - upMin);
        const double padL = 36, padR = 44, padTop = 28, padBot = 36;
        var scale = Math.Min((w - padL - padR) / (halfB * 2), (h - padTop - padBot) / height) * 0.9;
        var cx = x + padL + (w - padL - padR) * 0.5;
        var keelY = y + h - padBot;

        // Ghost loft for context only.
        if (loft.Count >= 3)
            sb.AppendLine($"<path d=\"{PackageDrawingChrome.PathYu(loft, cx, keelY, scale)}\" fill=\"none\" stroke=\"#bbb\" stroke-width=\"0.7\" stroke-dasharray=\"3 2\"/>");

        for (var i = 0; i + 2 < indices.Count; i += 3)
        {
            var i0 = indices[i];
            var i1 = indices[i + 1];
            var i2 = indices[i + 2];
            if (i0 < 0 || i1 < 0 || i2 < 0 || i0 >= verts.Count || i1 >= verts.Count || i2 >= verts.Count)
                continue;
            var a = verts[i0];
            var b = verts[i1];
            var c = verts[i2];
            if (a.Length < 2 || b.Length < 2 || c.Length < 2)
                continue;
            var ax = cx - a[0] * scale;
            var ay = keelY - a[1] * scale;
            var bx = cx - b[0] * scale;
            var by = keelY - b[1] * scale;
            var dx = cx - c[0] * scale;
            var dy = keelY - c[1] * scale;
            sb.AppendLine(
                $"<polygon points=\"{PackageDrawingChrome.F(ax)},{PackageDrawingChrome.F(ay)} {PackageDrawingChrome.F(bx)},{PackageDrawingChrome.F(by)} {PackageDrawingChrome.F(dx)},{PackageDrawingChrome.F(dy)}\" fill=\"#8a6a4a\" fill-opacity=\"0.92\" stroke=\"#3a2a18\" stroke-width=\"0.35\"/>");
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
        sb.AppendLine($"<text x=\"{PackageDrawingChrome.F(cx)}\" y=\"{PackageDrawingChrome.F(y + 16)}\" text-anchor=\"middle\" font-size=\"11\" fill=\"#222\">z={zStem.ToString("0.##", CultureInfo.InvariantCulture)} m · {indices.Count / 3} tris · {LockDraftGeometry.ScaleNote(scale)}</text>");
        sb.AppendLine($"<text x=\"{PackageDrawingChrome.F(cx - halfB * scale)}\" y=\"{PackageDrawingChrome.F(y + h - 8)}\" font-size=\"10\" fill=\"#444\">P</text>");
        sb.AppendLine($"<text x=\"{PackageDrawingChrome.F(cx + halfB * scale)}\" y=\"{PackageDrawingChrome.F(y + h - 8)}\" text-anchor=\"end\" font-size=\"10\" fill=\"#444\">S</text>");
    }
}
