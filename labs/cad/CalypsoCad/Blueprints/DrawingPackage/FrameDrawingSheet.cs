using System.Globalization;
using System.Text;
using Novolis.Cad.Primitives;

namespace CalypsoCad.Blueprints.DrawingPackage;

/// <summary>
/// Projects CAD frame annulus mesh (hull-attached plate-web) plus chord/deckbeam boxes.
/// </summary>
internal static class FrameDrawingSheet
{
    public static string Build(DrawingEntry entry, CadDocument cad, float zStem, string? frameId = null)
    {
        var ship = PackageDrawingChrome.ReadShip(cad);
        frameId ??= $"frame-F{zStem.ToString("0", CultureInfo.InvariantCulture)}";
        var parts = CadJsonStructureQuery.FramePartsAt(cad, frameId);
        var hasAnnulus = CadJsonStructureQuery.TryFrameAnnulusMesh(cad, frameId, out var annulusVerts, out var annulusIndices);
        var label = $"F @ z={zStem.ToString("0", CultureInfo.InvariantCulture)}";

        var sb = PackageDrawingChrome.BeginHtml(
            entry, label,
            [("Frame", frameId),
             ("Station", $"z = {zStem.ToString("0.##", CultureInfo.InvariantCulture)} m from stem"),
             ("CAD", hasAnnulus
                 ? $"annulus mesh + {parts.Count} boxes"
                 : $"{parts.Count} boxes (annulus MISSING)")],
            "Ordinary frame · ISO 128-15 · CAD annulus + chord projection (not BH)");

        PackageDrawingChrome.Label(sb, 28, 32, "A — TRANSVERSE · CAD frame (looking aft)");
        DrawCadFrame(sb, 28, 42, 520, 360, ship, zStem, parts, hasAnnulus, annulusVerts, annulusIndices);

        PackageDrawingChrome.Label(sb, 560, 32, "B — PLAN REF (stern ←)");
        PackageDrawingChrome.DrawPlanHull(sb, 560, 42, 260, 160, ship, (ox, oy, scale) =>
        {
            foreach (var bh in ship.Bulkheads)
                PackageDrawingChrome.MarkAthwartStation(sb, ship, ox, oy, scale, bh.ZStem, null, focus: false, profile: false);
            PackageDrawingChrome.MarkAthwartStation(sb, ship, ox, oy, scale, zStem, label, focus: true, profile: false);
        });

        PackageDrawingChrome.Label(sb, 560, 220, "C — PROFILE REF CL (stern ←)");
        PackageDrawingChrome.DrawProfileHull(sb, 560, 230, 260, 172, ship, showDecks: true, (ox, keelY, scale) =>
        {
            foreach (var bh in ship.Bulkheads)
                PackageDrawingChrome.MarkAthwartStation(sb, ship, ox, keelY, scale, bh.ZStem, null, focus: false, profile: true);
            PackageDrawingChrome.MarkAthwartStation(sb, ship, ox, keelY, scale, zStem, label, focus: true, profile: true);
        });

        PackageDrawingChrome.NoteLines(sb, 560, 420,
            hasAnnulus
                ? "Fill = annular plate-web (hull attach)."
                : "WARNING: no frame-*-annulus mesh.",
            $"{parts.Count} CAD boxes (chords / deck beams).",
            "Ghost loft = hull context only.");

        return PackageDrawingChrome.EndHtml(sb);
    }

    private static void DrawCadFrame(
        StringBuilder sb, double x, double y, double w, double h,
        PackageDrawingChrome.ShipBasics ship, float zStem,
        IReadOnlyList<CadJsonStructureQuery.FramePartBox> parts,
        bool hasAnnulus,
        IReadOnlyList<float[]> annulusVerts,
        IReadOnlyList<int> annulusIndices)
    {
        sb.AppendLine($"<rect x=\"{PackageDrawingChrome.F(x)}\" y=\"{PackageDrawingChrome.F(y)}\" width=\"{PackageDrawingChrome.F(w)}\" height=\"{PackageDrawingChrome.F(h)}\" fill=\"#fff\" stroke=\"#333\"/>");
        LockDraftGeometry.SampleLoftUpRange(ship.Loft, zStem, ship.Oah, out var upMin, out var upMax);
        var loft = LockDraftGeometry.StationVertsAt(ship.Loft, zStem);
        var halfB = loft.Count > 0 ? Math.Max(0.2, loft.Max(v => Math.Abs(v.Y))) : 10.0;
        var height = Math.Max(0.2, upMax - upMin);
        const double padL = 36, padR = 44, padTop = 28, padBot = 36;
        var scale = Math.Min((w - padL - padR) / (halfB * 2), (h - padTop - padBot) / height) * 0.9;
        var cx = x + padL + (w - padL - padR) * 0.5;
        var keelY = y + h - padBot;

        if (loft.Count >= 3)
            sb.AppendLine($"<path d=\"{PackageDrawingChrome.PathYu(loft, cx, keelY, scale)}\" fill=\"none\" stroke=\"#bbb\" stroke-width=\"0.7\" stroke-dasharray=\"3 2\"/>");

        if (!hasAnnulus && parts.Count == 0)
        {
            sb.AppendLine($"<text x=\"{PackageDrawingChrome.F(cx)}\" y=\"{PackageDrawingChrome.F(y + h * 0.5)}\" text-anchor=\"middle\" fill=\"#c00\">no CAD frame parts at this station</text>");
            return;
        }

        // Annular plate-web first — diagonals + verticals that meet the shell.
        if (hasAnnulus)
        {
            for (var i = 0; i + 2 < annulusIndices.Count; i += 3)
            {
                var i0 = annulusIndices[i];
                var i1 = annulusIndices[i + 1];
                var i2 = annulusIndices[i + 2];
                if (i0 < 0 || i1 < 0 || i2 < 0
                    || i0 >= annulusVerts.Count || i1 >= annulusVerts.Count || i2 >= annulusVerts.Count)
                    continue;
                var a = annulusVerts[i0];
                var b = annulusVerts[i1];
                var c = annulusVerts[i2];
                if (a.Length < 2 || b.Length < 2 || c.Length < 2)
                    continue;
                var ax = cx - a[0] * scale;
                var ay = keelY - a[1] * scale;
                var bx = cx - b[0] * scale;
                var by = keelY - b[1] * scale;
                var dx = cx - c[0] * scale;
                var dy = keelY - c[1] * scale;
                sb.AppendLine(
                    $"<polygon points=\"{PackageDrawingChrome.F(ax)},{PackageDrawingChrome.F(ay)} {PackageDrawingChrome.F(bx)},{PackageDrawingChrome.F(by)} {PackageDrawingChrome.F(dx)},{PackageDrawingChrome.F(dy)}\" fill=\"#6e7a86\" fill-opacity=\"0.88\" stroke=\"#24303a\" stroke-width=\"0.4\"/>");
            }
        }

        foreach (var p in parts)
        {
            var xa = cx - p.Y1 * scale;
            var xb = cx - p.Y0 * scale;
            var ua = keelY - p.Up1 * scale;
            var ub = keelY - p.Up0 * scale;
            var fill = p.Name.Contains("-chord-", StringComparison.OrdinalIgnoreCase) ? "#4a6a86"
                : p.Name.Contains("-deckbeam-", StringComparison.OrdinalIgnoreCase) ? "#8a9096"
                : p.Name.Contains("-ring-", StringComparison.OrdinalIgnoreCase) ? "#6e7a86"
                : "#8a9096";
            sb.AppendLine(
                $"<rect x=\"{PackageDrawingChrome.F(Math.Min(xa, xb))}\" y=\"{PackageDrawingChrome.F(Math.Min(ua, ub))}\" width=\"{PackageDrawingChrome.F(Math.Max(1.2, Math.Abs(xb - xa)))}\" height=\"{PackageDrawingChrome.F(Math.Max(1.2, Math.Abs(ub - ua)))}\" fill=\"{fill}\" fill-opacity=\"0.9\" stroke=\"#24303a\" stroke-width=\"0.55\"/>");
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
        var tris = hasAnnulus ? annulusIndices.Count / 3 : 0;
        sb.AppendLine($"<text x=\"{PackageDrawingChrome.F(cx)}\" y=\"{PackageDrawingChrome.F(y + 16)}\" text-anchor=\"middle\" font-size=\"11\" fill=\"#222\">z={zStem.ToString("0.##", CultureInfo.InvariantCulture)} m · annulus {tris} tris · {parts.Count} boxes · {LockDraftGeometry.ScaleNote(scale)}</text>");
    }
}
