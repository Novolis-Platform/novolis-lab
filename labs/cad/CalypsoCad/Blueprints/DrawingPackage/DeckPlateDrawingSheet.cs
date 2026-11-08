using System.Globalization;
using System.Text;
using Novolis.Cad.Primitives;

namespace CalypsoCad.Blueprints.DrawingPackage;

/// <summary>Plan footprints from CAD face-top boxes + cellular steel mass note.</summary>
internal static class DeckPlateDrawingSheet
{
    public static string Build(DrawingEntry entry, CadDocument cad, int deckNum, string deckLabel)
    {
        var ship = PackageDrawingChrome.ReadShip(cad);
        var panels = CadJsonStructureQuery.SandwichPanels(cad, (float)ship.Loa)
            .Where(p => p.DeckNum == deckNum)
            .OrderBy(p => p.Z0)
            .ThenBy(p => p.Y0)
            .ToList();
        var steel = CadJsonStructureQuery.SandwichSteelMass(cad, deckNum);
        var elev = deckNum switch { -1 => ship.DkM1, 1 => ship.Dk1, _ => ship.Dk0 };

        var sb = PackageDrawingChrome.BeginHtml(
            entry, deckLabel,
            [("Deck", deckLabel),
             ("Face-top bays", panels.Count.ToString(CultureInfo.InvariantCulture)),
             ("Steel parts", steel.PartCount.ToString(CultureInfo.InvariantCulture)),
             ("Steel mass", $"{(steel.MassKg / 1000f).ToString("0.#", CultureInfo.InvariantCulture)} t"),
             ("Plate elev", $"{elev.ToString("0.##", CultureInfo.InvariantCulture)} m up")],
            "CAD face-top footprints · cellular steel mass (faces+peri+ribs) · ISO 128-15");

        PackageDrawingChrome.Label(sb, 28, 32, $"A — PLAN · {deckLabel} face-top boxes from CAD");
        DrawDeckPlan(sb, 28, 42, 520, 360, ship, panels);

        PackageDrawingChrome.Label(sb, 560, 32, "B — PLAN REF (hull)");
        PackageDrawingChrome.DrawPlanHull(sb, 560, 42, 260, 160, ship, (ox, oy, scale) =>
        {
            foreach (var p in panels)
                DrawPanelRect(sb, ship, ox, oy, scale, p, "#c82828", 0.25, 0.8);
        });

        PackageDrawingChrome.Label(sb, 560, 220, "C — PROFILE REF · this deck bold");
        PackageDrawingChrome.DrawProfileHull(sb, 560, 230, 260, 172, ship, showDecks: false, (ox, keelY, scale) =>
        {
            foreach (var e in new[] { ship.DkM1, ship.Dk0, ship.Dk1 })
            {
                var focus = Math.Abs(e - elev) < 0.05;
                sb.AppendLine(
                    $"<line x1=\"{PackageDrawingChrome.F(ox)}\" y1=\"{PackageDrawingChrome.F(keelY - e * scale)}\" x2=\"{PackageDrawingChrome.F(ox + ship.Loa * scale)}\" y2=\"{PackageDrawingChrome.F(keelY - e * scale)}\" stroke=\"{(focus ? "#c82828" : "#ccc")}\" stroke-width=\"{(focus ? "2.2" : "0.6")}\"/>");
            }

            sb.AppendLine($"<text x=\"{PackageDrawingChrome.F(ox + 4)}\" y=\"{PackageDrawingChrome.F(keelY - elev * scale - 4)}\" font-size=\"9\" fill=\"#c82828\">{PackageDrawingChrome.Esc(deckLabel)}</text>");
        });

        PackageDrawingChrome.NoteLines(sb, 560, 420,
            $"{panels.Count} face-top bays from CAD boxes.",
            $"Steel V={steel.SteelVolumeM3.ToString("0.###", CultureInfo.InvariantCulture)} m³ · {(steel.MassKg / 1000f).ToString("0.#", CultureInfo.InvariantCulture)} t",
            "Hollow core not counted as solid plate.");

        return PackageDrawingChrome.EndHtml(sb);
    }

    private static void DrawDeckPlan(
        StringBuilder sb, double x, double y, double w, double h,
        PackageDrawingChrome.ShipBasics ship,
        IReadOnlyList<CadJsonStructureQuery.SandwichPanel> panels)
    {
        sb.AppendLine($"<rect x=\"{PackageDrawingChrome.F(x)}\" y=\"{PackageDrawingChrome.F(y)}\" width=\"{PackageDrawingChrome.F(w)}\" height=\"{PackageDrawingChrome.F(h)}\" fill=\"#fff\" stroke=\"#333\"/>");
        var scale = LockDraftGeometry.SharedPlanScale(ship.Loa, ship.Beam, w, h);
        var ox = x + LockDraftGeometry.PlanPadL
                 + ((w - LockDraftGeometry.PlanPadL - LockDraftGeometry.PlanPadR) - ship.Loa * scale) * 0.5;
        var oy = y + LockDraftGeometry.PlanPadTop
                 + (h - LockDraftGeometry.PlanPadTop - LockDraftGeometry.PlanPadBot) * 0.5;

        sb.AppendLine($"<path d=\"{PackageDrawingChrome.HullPlanPath(ship.Loft, ship.Loa, ox, oy, scale)}\" fill=\"none\" stroke=\"#999\" stroke-width=\"0.8\" stroke-dasharray=\"3 2\"/>");
        sb.AppendLine($"<line x1=\"{PackageDrawingChrome.F(ox)}\" y1=\"{PackageDrawingChrome.F(oy)}\" x2=\"{PackageDrawingChrome.F(ox + ship.Loa * scale)}\" y2=\"{PackageDrawingChrome.F(oy)}\" stroke=\"#0645ad\" stroke-dasharray=\"4 2\" stroke-width=\"0.6\"/>");

        var i = 0;
        foreach (var p in panels)
        {
            i++;
            DrawPanelRect(sb, ship, ox, oy, scale, p, "#9aa3ad", 0.85, 0.9);
            var x0 = LockDraftGeometry.ShipX(ox, scale, ship.Loa, p.Z1);
            var x1 = LockDraftGeometry.ShipX(ox, scale, ship.Loa, p.Z0);
            if (Math.Abs(x1 - x0) > 28)
            {
                var midY = oy - ((p.Y0 + p.Y1) * 0.5) * scale;
                sb.AppendLine($"<text x=\"{PackageDrawingChrome.F((x0 + x1) * 0.5)}\" y=\"{PackageDrawingChrome.F(midY + 3)}\" text-anchor=\"middle\" font-size=\"8\" fill=\"#111\">{i}</text>");
            }
        }

        PackageDrawingChrome.DimH(sb, ox, oy + ship.Beam * 0.5 * scale + 12, ox + ship.Loa * scale, $"LOA {PackageDrawingChrome.Nice(ship.Loa)}");
        sb.AppendLine($"<text x=\"{PackageDrawingChrome.F(ox)}\" y=\"{PackageDrawingChrome.F(y + h - 6)}\" font-size=\"9\" fill=\"#555\">STERN · face-top bays from CAD</text>");
        sb.AppendLine($"<text x=\"{PackageDrawingChrome.F(x + w * 0.5)}\" y=\"{PackageDrawingChrome.F(y + 16)}\" text-anchor=\"middle\" font-size=\"11\" fill=\"#222\">{panels.Count} bays · {LockDraftGeometry.ScaleNote(scale)}</text>");
    }

    private static void DrawPanelRect(
        StringBuilder sb,
        PackageDrawingChrome.ShipBasics ship,
        double ox, double oy, double scale,
        CadJsonStructureQuery.SandwichPanel p,
        string fill, double opacity, double strokeW)
    {
        var x0 = LockDraftGeometry.ShipX(ox, scale, ship.Loa, p.Z1);
        var x1 = LockDraftGeometry.ShipX(ox, scale, ship.Loa, p.Z0);
        var y0 = oy - p.Y1 * scale;
        var y1 = oy - p.Y0 * scale;
        sb.AppendLine(
            $"<rect x=\"{PackageDrawingChrome.F(Math.Min(x0, x1))}\" y=\"{PackageDrawingChrome.F(Math.Min(y0, y1))}\" width=\"{PackageDrawingChrome.F(Math.Abs(x1 - x0))}\" height=\"{PackageDrawingChrome.F(Math.Abs(y1 - y0))}\" fill=\"{fill}\" fill-opacity=\"{PackageDrawingChrome.F(opacity)}\" stroke=\"#2a3238\" stroke-width=\"{PackageDrawingChrome.F(strokeW)}\"/>");
    }
}
