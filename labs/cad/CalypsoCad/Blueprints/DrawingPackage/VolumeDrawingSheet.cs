using System.Globalization;
using System.Text;
using Novolis.Cad.Primitives;

namespace CalypsoCad.Blueprints.DrawingPackage;

/// <summary>
/// People-movement volume only (shirt-sleeves / airlock anteroom / airlock / hybrid)
/// + hull refs — from <c>calypso.cadjson</c> spaces.
/// </summary>
internal static class VolumeDrawingSheet
{
    public static string Build(DrawingEntry entry, CadDocument cad, string spaceId, VolumeCirculationKind kind)
    {
        var ship = PackageDrawingChrome.ReadShip(cad);
        if (!PackageDrawingChrome.TryReadCompartment(
                cad, spaceId,
                out var z0, out var z1, out var y0, out var y1, out var u0, out var u1,
                out var deckTag, out var planRing))
        {
            var miss = PackageDrawingChrome.BeginHtml(
                entry, spaceId,
                [("Space", spaceId)],
                "space missing from calypso.cadjson");
            PackageDrawingChrome.Label(miss, 40, 80, $"No CAD space id={spaceId}");
            return PackageDrawingChrome.EndHtml(miss);
        }

        var zMid = (float)((z0 + z1) * 0.5);
        var kindName = VolumeCirculation.DisplayName(kind);
        var fill = kind switch
        {
            VolumeCirculationKind.ShirtSleeves => "#4a8ec4",
            VolumeCirculationKind.AirlockAnteroom => "#c4a04a",
            VolumeCirculationKind.Airlock => "#c46a4a",
            VolumeCirculationKind.Hybrid => "#7a6ac4",
            _ => "#888",
        };

        var sb = PackageDrawingChrome.BeginHtml(
            entry, spaceId,
            [("Space", spaceId),
             ("Class", kindName),
             ("Deck", deckTag ?? "—"),
             ("z", $"{z0.ToString("0.##", CultureInfo.InvariantCulture)}–{z1.ToString("0.##", CultureInfo.InvariantCulture)} m")],
            $"{kindName} volume only · people movement · ISO 128-15");

        PackageDrawingChrome.Label(sb, 28, 32, $"A — PLAN · {spaceId} only");
        DrawVolumePlan(sb, 28, 42, 520, 280, ship, z0, z1, y0, y1, planRing, fill, spaceId);

        PackageDrawingChrome.Label(sb, 560, 32, "B — TRANSVERSE @ mid");
        DrawVolumeTransverse(sb, 560, 42, 260, 200, ship, zMid, y0, y1, u0, u1, fill);

        PackageDrawingChrome.Label(sb, 28, 340, "C — PROFILE REF · volume extent");
        PackageDrawingChrome.DrawProfileHull(sb, 28, 352, 520, 200, ship, showDecks: true, (ox, keelY, scale) =>
        {
            var x0 = LockDraftGeometry.ShipX(ox, scale, ship.Loa, z1);
            var x1 = LockDraftGeometry.ShipX(ox, scale, ship.Loa, z0);
            var ua = keelY - u1 * scale;
            var ub = keelY - u0 * scale;
            sb.AppendLine(
                $"<rect x=\"{PackageDrawingChrome.F(Math.Min(x0, x1))}\" y=\"{PackageDrawingChrome.F(Math.Min(ua, ub))}\" width=\"{PackageDrawingChrome.F(Math.Abs(x1 - x0))}\" height=\"{PackageDrawingChrome.F(Math.Abs(ub - ua))}\" fill=\"{fill}\" fill-opacity=\"0.45\" stroke=\"{fill}\" stroke-width=\"1.4\"/>");
            sb.AppendLine($"<text x=\"{PackageDrawingChrome.F((x0 + x1) * 0.5)}\" y=\"{PackageDrawingChrome.F(Math.Min(ua, ub) - 4)}\" text-anchor=\"middle\" font-size=\"10\" fill=\"#222\">{PackageDrawingChrome.Esc(spaceId)}</text>");
        });

        PackageDrawingChrome.NoteLines(sb, 560, 260,
            $"{kindName}.",
            "A/B: this volume only.",
            "C: extent on CL profile.");

        return PackageDrawingChrome.EndHtml(sb);
    }

    private static void DrawVolumePlan(
        StringBuilder sb, double x, double y, double w, double h,
        PackageDrawingChrome.ShipBasics ship,
        double z0, double z1, double y0, double y1,
        List<(double Y, double Z)>? planRing,
        string fill, string spaceId)
    {
        sb.AppendLine($"<rect x=\"{PackageDrawingChrome.F(x)}\" y=\"{PackageDrawingChrome.F(y)}\" width=\"{PackageDrawingChrome.F(w)}\" height=\"{PackageDrawingChrome.F(h)}\" fill=\"#fff\" stroke=\"#333\"/>");
        var scale = LockDraftGeometry.SharedPlanScale(ship.Loa, ship.Beam, w, h);
        var ox = x + LockDraftGeometry.PlanPadL
                 + ((w - LockDraftGeometry.PlanPadL - LockDraftGeometry.PlanPadR) - ship.Loa * scale) * 0.5;
        var oy = y + LockDraftGeometry.PlanPadTop
                 + (h - LockDraftGeometry.PlanPadTop - LockDraftGeometry.PlanPadBot) * 0.5;

        sb.AppendLine($"<path d=\"{PackageDrawingChrome.HullPlanPath(ship.Loft, ship.Loa, ox, oy, scale)}\" fill=\"none\" stroke=\"#bbb\" stroke-width=\"0.8\" stroke-dasharray=\"3 2\"/>");
        sb.AppendLine($"<line x1=\"{PackageDrawingChrome.F(ox)}\" y1=\"{PackageDrawingChrome.F(oy)}\" x2=\"{PackageDrawingChrome.F(ox + ship.Loa * scale)}\" y2=\"{PackageDrawingChrome.F(oy)}\" stroke=\"#0645ad\" stroke-dasharray=\"4 2\" stroke-width=\"0.55\"/>");

        if (planRing is { Count: >= 3 })
        {
            var d = new StringBuilder("M");
            for (var i = 0; i < planRing.Count; i++)
            {
                var (py, pz) = planRing[i];
                var sx = LockDraftGeometry.ShipX(ox, scale, ship.Loa, pz);
                var sy = oy - py * scale;
                d.Append(i == 0 ? $" {PackageDrawingChrome.F(sx)} {PackageDrawingChrome.F(sy)}" : $" L {PackageDrawingChrome.F(sx)} {PackageDrawingChrome.F(sy)}");
            }

            d.Append(" Z");
            sb.AppendLine($"<path d=\"{d}\" fill=\"{fill}\" fill-opacity=\"0.55\" stroke=\"#1a1f24\" stroke-width=\"1.3\"/>");
        }
        else
        {
            var x0 = LockDraftGeometry.ShipX(ox, scale, ship.Loa, z1);
            var x1 = LockDraftGeometry.ShipX(ox, scale, ship.Loa, z0);
            var ya = oy - y1 * scale;
            var yb = oy - y0 * scale;
            sb.AppendLine(
                $"<rect x=\"{PackageDrawingChrome.F(Math.Min(x0, x1))}\" y=\"{PackageDrawingChrome.F(Math.Min(ya, yb))}\" width=\"{PackageDrawingChrome.F(Math.Abs(x1 - x0))}\" height=\"{PackageDrawingChrome.F(Math.Abs(yb - ya))}\" fill=\"{fill}\" fill-opacity=\"0.55\" stroke=\"#1a1f24\" stroke-width=\"1.3\"/>");
        }

        sb.AppendLine($"<text x=\"{PackageDrawingChrome.F(LockDraftGeometry.ShipX(ox, scale, ship.Loa, (z0 + z1) * 0.5))}\" y=\"{PackageDrawingChrome.F(oy - ((y0 + y1) * 0.5) * scale)}\" text-anchor=\"middle\" font-size=\"11\" font-weight=\"600\" fill=\"#111\">{PackageDrawingChrome.Esc(spaceId)}</text>");
        PackageDrawingChrome.DimH(sb,
            LockDraftGeometry.ShipX(ox, scale, ship.Loa, z1),
            oy + ship.Beam * 0.5 * scale + 10,
            LockDraftGeometry.ShipX(ox, scale, ship.Loa, z0),
            $"F–A {PackageDrawingChrome.Nice(z1 - z0)}");
        sb.AppendLine($"<text x=\"{PackageDrawingChrome.F(x + w * 0.5)}\" y=\"{PackageDrawingChrome.F(y + 14)}\" text-anchor=\"middle\" font-size=\"11\" fill=\"#222\">{LockDraftGeometry.ScaleNote(scale)}</text>");
    }

    private static void DrawVolumeTransverse(
        StringBuilder sb, double x, double y, double w, double h,
        PackageDrawingChrome.ShipBasics ship, float zMid,
        double y0, double y1, double u0, double u1, string fill)
    {
        sb.AppendLine($"<rect x=\"{PackageDrawingChrome.F(x)}\" y=\"{PackageDrawingChrome.F(y)}\" width=\"{PackageDrawingChrome.F(w)}\" height=\"{PackageDrawingChrome.F(h)}\" fill=\"#fff\" stroke=\"#333\"/>");
        var outer = LockDraftGeometry.StationVertsAt(ship.Loft, zMid);
        LockDraftGeometry.SampleLoftUpRange(ship.Loft, zMid, ship.Oah, out var upMin, out var upMax);
        var halfB = Math.Max(0.2, LockDraftGeometry.LoftHalfBeam(ship.Loft, zMid));
        var height = Math.Max(0.2, upMax - upMin);
        const double padL = 28, padR = 28, padTop = 22, padBot = 28;
        var scale = Math.Min((w - padL - padR) / (halfB * 2), (h - padTop - padBot) / height) * 0.88;
        var cx = x + padL + (w - padL - padR) * 0.5;
        var keelY = y + h - padBot;

        if (outer.Count >= 3)
            sb.AppendLine($"<path d=\"{PackageDrawingChrome.PathYu(outer, cx, keelY, scale)}\" fill=\"none\" stroke=\"#bbb\" stroke-width=\"0.9\" stroke-dasharray=\"3 2\"/>");

        var xa = cx - Math.Max(y0, y1) * scale;
        var xb = cx - Math.Min(y0, y1) * scale;
        var ua = keelY - u1 * scale;
        var ub = keelY - u0 * scale;
        sb.AppendLine(
            $"<rect x=\"{PackageDrawingChrome.F(Math.Min(xa, xb))}\" y=\"{PackageDrawingChrome.F(Math.Min(ua, ub))}\" width=\"{PackageDrawingChrome.F(Math.Abs(xb - xa))}\" height=\"{PackageDrawingChrome.F(Math.Abs(ub - ua))}\" fill=\"{fill}\" fill-opacity=\"0.6\" stroke=\"#1a1f24\" stroke-width=\"1.2\"/>");

        sb.AppendLine($"<line x1=\"{PackageDrawingChrome.F(cx)}\" y1=\"{PackageDrawingChrome.F(keelY - upMax * scale)}\" x2=\"{PackageDrawingChrome.F(cx)}\" y2=\"{PackageDrawingChrome.F(keelY - upMin * scale)}\" stroke=\"#0645ad\" stroke-dasharray=\"4 2\" stroke-width=\"0.7\"/>");
        sb.AppendLine($"<text x=\"{PackageDrawingChrome.F(cx)}\" y=\"{PackageDrawingChrome.F(y + 14)}\" text-anchor=\"middle\" font-size=\"10\" fill=\"#222\">z={zMid.ToString("0.##", CultureInfo.InvariantCulture)} m</text>");
    }
}
