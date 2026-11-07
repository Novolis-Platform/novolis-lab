using System.Globalization;
using System.Text;
using System.Text.Json;
using CalypsoCad.Session;
using CalypsoCad.Structure;

namespace CalypsoCad.Blueprints;

/// <summary>
/// Modular 2D CAD detail pack: hull loft + compartments from lock; sandwich plates from cadjson.
/// ISO 128-15 stern-left, same convention as <see cref="InternalsBlueprintEmitter"/>.
/// </summary>
internal static class StructureDetailBlueprintEmitter
{
    public static string Emit(string? lockJsonPath = null, string? outputDirectory = null, string? cadJsonPath = null)
    {
        var lockPath = lockJsonPath ?? ResolveLockPath();
        var outDir = outputDirectory ?? Path.Combine(
            CalypsoProjectPaths.ProjectRoot, "docs", "internals", "details");
        Directory.CreateDirectory(outDir);

        // Clear prior junk sheets (voxel-era BH/FRAME/SPACE dumps).
        foreach (var stale in Directory.EnumerateFiles(outDir, "*.html"))
            File.Delete(stale);

        using var doc = JsonDocument.Parse(File.ReadAllText(lockPath));
        var L = LockDraft.From(doc.RootElement);
        var cad = CadJsonStructureQuery.Load(cadJsonPath);
        var sandwiches = CadJsonStructureQuery.SandwichPanels(cad, (float)L.Loa);
        if (sandwiches.Count == 0)
            throw new InvalidOperationException(
                "calypso.cadjson has no deck-sandwich-* panels — regenerate structure before blueprints.");

        var index = new List<(string File, string Title)>();

        foreach (var (id, zStem, _) in L.BhSchedule)
        {
            var file = $"BH-{id}.html";
            Write(outDir, file, BuildBhSheet(L, id, zStem, sandwiches));
            index.Add((file, $"Bulkhead {id} @ z={zStem:0.##} m from stem — plan + section + profile"));
        }

        var bayM = L.FrameBayM > 0.5f ? L.FrameBayM : 4f;
        // Dense midbody + hold samples (passage portals live on F24–F48 CORR track).
        var frameZs = new HashSet<float>();
        foreach (var mult in new[] { 0, 3, 5, 6, 7, 8, 10, 12, 13, 14, 15 })
        {
            var z = bayM * mult;
            if (z <= (float)L.Loa - 0.5f)
                frameZs.Add(z);
        }

        foreach (var z in frameZs.OrderBy(z => z))
        {
            var file = $"FRAME-z{z.ToString("0", CultureInfo.InvariantCulture)}.html";
            Write(outDir, file, BuildFrameSheet(L, z, sandwiches));
            index.Add((file, $"Transverse web station @ z={z:0} m — section + plan tick"));
        }

        foreach (var space in new[]
                 {
                     "BRIDGE", "ENG", "HOLD", "CORR_P", "CORR_S", "CROSSING", "ACCESS",
                     "GALLEY", "LOUNGE", "CREW_1", "CREW_3", "PAX_1", "INFIRMARY",
                     "AIRLOCK_A_port", "AIRLOCK_B_stbd",
                 })
        {
            var file = $"SPACE-{space}.html";
            Write(outDir, file, BuildSpaceSheet(L, space, sandwiches));
            index.Add((file, $"Deck-plan crop — {space}"));
        }

        Write(outDir, "index.html", BuildIndex(index));
        Console.WriteLine($"Wrote {index.Count + 1} cadjson+lock detail sheets → {outDir}");
        return outDir;
    }

    private static void Write(string dir, string name, string html) =>
        File.WriteAllText(Path.Combine(dir, name), html, new UTF8Encoding(false));

    private static string ResolveLockPath()
    {
        foreach (var p in new[]
                 {
                     Path.Combine(CalypsoProjectPaths.ProjectRoot, "docs", "internals", "CAL-INT-GA-001.json"),
                     @"d:\novolis\novolis-lab\labs\cad\CalypsoCad\docs\internals\CAL-INT-GA-001.json",
                 })
        {
            if (File.Exists(p))
                return p;
        }

        throw new FileNotFoundException("CAL-INT-GA-001.json not found");
    }

    private static string BuildIndex(List<(string File, string Title)> items)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html><html><head><meta charset=\"utf-8\"/>");
        sb.AppendLine("<title>Calypso structure detail pack</title>");
        sb.AppendLine("<style>body{font:14px/1.45 \"IBM Plex Mono\",Consolas,monospace;margin:2rem;background:#f4f1ea;color:#222}");
        sb.AppendLine("a{color:#0645ad} li{margin:0.35rem 0} .note{color:#555;max-width:42rem}</style></head><body>");
        sb.AppendLine("<h1>Calypso structure detail pack</h1>");
        sb.AppendLine("<p class=\"note\">ISO 128-15 stern left / bow right. Hull loft from lock; sandwich plates from <code>calypso.cadjson</code>. Pair with GA/DK sheets.</p>");
        sb.AppendLine("<ol>");
        foreach (var (file, title) in items)
            sb.AppendLine($"<li><a href=\"{file}\">{Esc(title)}</a></li>");
        sb.AppendLine("</ol></body></html>");
        return sb.ToString();
    }

    // PLAN viewport is the orthographic scale authority (LOA × BEAM isotropic).
    private static double SharedPlanScale(LockDraft L, double planW, double planH) =>
        LockDraftGeometry.SharedPlanScale(L.Loa, L.Beam, planW, planH);

    private static string BuildBhSheet(
        LockDraft L, string id, float zStem, IReadOnlyList<CadJsonStructureQuery.SandwichPanel> sandwiches)
    {
        const double planW = 380, planH = 200;
        var scale = SharedPlanScale(L, planW, planH);
        var sb = SheetHead($"BH {id}", $"zFromStem={zStem:0.##} m · structural pressure bulkhead · lock + cadjson");
        sb.AppendLine(SvgOpen(841, 1100));
        sb.AppendLine(Label(40, 24, $"PLAN — top-down (stern left) · {ScaleNote(scale)}"));
        DrawPlanCrop(sb, L, 40, 36, planW, planH, zStem, highlight: null, deck: 0, scale);
        sb.AppendLine(Label(440, 24, "TRANSVERSE — front-to-back @ station"));
        DrawTransverse(sb, L, 440, 36, 360, 200, zStem, pressureBh: true);
        sb.AppendLine(Label(40, 255, $"PROFILE — side-on CL (stern left) · {ScaleNote(scale)}"));
        DrawProfile(sb, L, 40, 268, 760, 160, zStem, scale, sandwiches);
        sb.AppendLine(Label(40, 445, $"DECK-BY-DECK — same scale as plan · {ScaleNote(scale)}"));
        DrawDeckStackAlongSpine(sb, L, 40, 458, 760, 620, zStem, scale, sandwiches);
        sb.AppendLine("</svg></body></html>");
        return sb.ToString();
    }

    private static string BuildFrameSheet(
        LockDraft L, float zStem, IReadOnlyList<CadJsonStructureQuery.SandwichPanel> sandwiches)
    {
        const double planW = 360, planH = 200;
        var scale = SharedPlanScale(L, planW, planH);
        var sb = SheetHead($"Frame bay z={zStem:0} m", "transverse web station · lock hull + cadjson plates");
        sb.AppendLine(SvgOpen(841, 1100));
        sb.AppendLine(Label(40, 24, "TRANSVERSE — front-to-back @ station"));
        DrawTransverse(sb, L, 40, 36, 380, 200, zStem, pressureBh: false);
        sb.AppendLine(Label(440, 24, $"PLAN — top-down (stern left) · {ScaleNote(scale)}"));
        DrawPlanCrop(sb, L, 440, 36, planW, planH, zStem, highlight: null, deck: 0, scale);
        sb.AppendLine(Label(40, 255, $"PROFILE — side-on CL (stern left) · {ScaleNote(scale)}"));
        DrawProfile(sb, L, 40, 268, 760, 160, zStem, scale, sandwiches);
        sb.AppendLine(Label(40, 445, $"DECK-BY-DECK — same scale as plan · {ScaleNote(scale)}"));
        DrawDeckStackAlongSpine(sb, L, 40, 458, 760, 620, zStem, scale, sandwiches);
        sb.AppendLine("</svg></body></html>");
        return sb.ToString();
    }

    private static string BuildSpaceSheet(
        LockDraft L, string spaceId, IReadOnlyList<CadJsonStructureQuery.SandwichPanel> sandwiches)
    {
        var space = L.Compartments.FirstOrDefault(c =>
            string.Equals(c.Id, spaceId, StringComparison.OrdinalIgnoreCase));
        var zMid = space is null ? (float)(L.Loa * 0.5) : (float)((space.Z0 + space.Z1) * 0.5);
        var deck = space?.DeckNum ?? 0;
        const double planW = 380, planH = 200;
        var scale = SharedPlanScale(L, planW, planH);
        var sb = SheetHead($"Space {spaceId}", space is null
            ? "compartment not in lock"
            : $"z={space.Z0:0.##}–{space.Z1:0.##} · y={space.Y0:0.##}–{space.Y1:0.##} · deck={space.DeckTag ?? deck.ToString()}");
        sb.AppendLine(SvgOpen(841, 1100));
        sb.AppendLine(Label(40, 24, $"PLAN — top-down (stern left) · {ScaleNote(scale)}"));
        DrawPlanCrop(sb, L, 40, 36, planW, planH, zMid, highlight: spaceId, deck: deck, scale);
        sb.AppendLine(Label(440, 24, "TRANSVERSE — front-to-back @ mid"));
        DrawTransverse(sb, L, 440, 36, 360, 200, zMid, pressureBh: false);
        sb.AppendLine(Label(40, 255, $"PROFILE — side-on CL (stern left) · {ScaleNote(scale)}"));
        DrawProfile(sb, L, 40, 268, 760, 160, zMid, scale, sandwiches);
        sb.AppendLine(Label(40, 445, $"DECK-BY-DECK — same scale as plan · {ScaleNote(scale)}"));
        DrawDeckStackAlongSpine(sb, L, 40, 458, 760, 620, zMid, scale, sandwiches);
        sb.AppendLine("</svg></body></html>");
        return sb.ToString();
    }

    private static string ScaleNote(double scalePxPerM) => LockDraftGeometry.ScaleNote(scalePxPerM);

    private static string PlateThickLabel(LockDraft L, bool notToScale) =>
        notToScale
            ? $"t={(L.TPlate * 1000).ToString("0", CultureInfo.InvariantCulture)} mm sandwich (not to scale)"
            : $"t={(L.TPlate * 1000).ToString("0", CultureInfo.InvariantCulture)} mm sandwich";

    private static void DrawPlanCrop(
        StringBuilder sb, LockDraft L, double x, double y, double w, double h,
        float focusZ, string? highlight, int deck, double scale)
    {
        sb.AppendLine($"<rect x=\"{F(x)}\" y=\"{F(y)}\" width=\"{F(w)}\" height=\"{F(h)}\" fill=\"#fff\" stroke=\"#333\"/>");
        var ox = x + LockDraftGeometry.PlanPadL
                 + ((w - LockDraftGeometry.PlanPadL - LockDraftGeometry.PlanPadR) - L.Loa * scale) * 0.5;
        var oy = y + LockDraftGeometry.PlanPadTop
                 + (h - LockDraftGeometry.PlanPadTop - LockDraftGeometry.PlanPadBot) * 0.5;

        sb.AppendLine($"<path d=\"{HullPlanPath(L, ox, oy, scale)}\" fill=\"none\" stroke=\"#111\" stroke-width=\"1.1\"/>");
        sb.AppendLine($"<line x1=\"{F(ox)}\" y1=\"{F(oy)}\" x2=\"{F(ox + L.Loa * scale)}\" y2=\"{F(oy)}\" stroke=\"#0645ad\" stroke-dasharray=\"6 3\" stroke-width=\"0.7\"/>");

        foreach (var c in L.Compartments.Where(c => MatchesDeck(c, deck)))
        {
            var isHi = highlight is not null
                       && string.Equals(c.Id, highlight, StringComparison.OrdinalIgnoreCase);
            var fill = isHi ? "#cfe0f0" : SpaceFill(c);
            var stroke = isHi ? "#c82828" : "#333";
            var sw = isHi ? "1.4" : "0.6";
            DrawPlanFootprint(sb, L, c, ox, oy, scale, scale, fill, stroke, sw, deck);
            if (c.Id is not null && (isHi || ShouldLabel(c.Id)))
            {
                var (cx, cy) = FootprintLabelPoint(L, c, ox, oy, scale, scale, deck);
                sb.AppendLine($"<text x=\"{F(cx)}\" y=\"{F(cy + 3)}\" text-anchor=\"middle\" font-size=\"9\" fill=\"#111\">{Esc(ShortId(c.Id))}</text>");
            }
        }

        foreach (var (bhId, zStem, _) in L.BhSchedule)
        {
            var sx = ShipX(ox, scale, L.Loa, zStem);
            var hb = LoftHalfBeam(L, zStem) * scale;
            sb.AppendLine($"<line x1=\"{F(sx)}\" y1=\"{F(oy - hb)}\" x2=\"{F(sx)}\" y2=\"{F(oy + hb)}\" stroke=\"#c82828\" stroke-width=\"1.6\"/>");
            if (Math.Abs(zStem - focusZ) < 0.6f)
                sb.AppendLine($"<text x=\"{F(sx + 3)}\" y=\"{F(oy - hb - 4)}\" font-size=\"9\" fill=\"#c82828\">{Esc(bhId)}</text>");
        }

        var fx = ShipX(ox, scale, L.Loa, focusZ);
        sb.AppendLine($"<line x1=\"{F(fx)}\" y1=\"{F(y + 4)}\" x2=\"{F(fx)}\" y2=\"{F(y + h - 4)}\" stroke=\"#c82828\" stroke-dasharray=\"3 2\" stroke-width=\"0.8\" opacity=\"0.7\"/>");

        AnnotatePlanDims(sb, L, ox, oy, scale, y, h, highlight);

        sb.AppendLine($"<text x=\"{F(ox)}\" y=\"{F(y + h - 4)}\" font-size=\"9\" fill=\"#444\">STERN</text>");
        sb.AppendLine($"<text x=\"{F(ox + L.Loa * scale)}\" y=\"{F(y + h - 4)}\" text-anchor=\"end\" font-size=\"9\" fill=\"#444\">BOW</text>");
    }

    private static void AnnotatePlanDims(
        StringBuilder sb, LockDraft L, double ox, double oy, double scale, double viewY, double viewH,
        string? highlight)
    {
        // Overall LOA under keel
        var yLoa = oy + L.Beam * 0.5 * scale + 10;
        if (yLoa > viewY + viewH - 6)
            yLoa = viewY + viewH - 8;
        DimH(sb, ox, yLoa, ox + L.Loa * scale, yLoa, $"LOA {Nice(L.Loa)}");

        // Midbody beam from loft
        var zMid = L.Loa * 0.5;
        var xBeam = ShipX(ox, scale, L.Loa, zMid);
        var hb = LoftHalfBeam(L, zMid);
        DimV(sb, xBeam + 10, oy - hb * scale, xBeam + 10, oy + hb * scale, $"BEAM {Nice(hb * 2)}");

        // BH bay chain along top
        var bhs = L.BhSchedule;
        var yBh = oy - L.Beam * 0.5 * scale - 8;
        if (yBh < viewY + 8)
            yBh = viewY + 10;
        for (var i = 0; i < bhs.Count - 1; i++)
        {
            var zA = bhs[i].ZStem;
            var zB = bhs[i + 1].ZStem;
            DimH(sb, ShipX(ox, scale, L.Loa, zB), yBh, ShipX(ox, scale, L.Loa, zA), yBh,
                Nice(zB - zA));
        }

        if (highlight is null)
            return;
        var hi = L.Compartments.FirstOrDefault(c =>
            string.Equals(c.Id, highlight, StringComparison.OrdinalIgnoreCase));
        if (hi is null)
            return;

        var yFa = oy + L.Beam * 0.5 * scale + 18;
        DimH(sb, ShipX(ox, scale, L.Loa, hi.Z1), yFa,
            ShipX(ox, scale, L.Loa, hi.Z0), yFa,
            $"{ShortId(hi.Id!)} F–A {Nice(hi.Z1 - hi.Z0)}");
        var xClr = ShipX(ox, scale, L.Loa, (hi.Z0 + hi.Z1) * 0.5);
        DimV(sb, xClr - 8, oy - hi.Y1 * scale, xClr - 8, oy - hi.Y0 * scale,
            $"CLR {Nice(Math.Abs(hi.Y1 - hi.Y0))}");
    }

    private static void DrawTransverse(
        StringBuilder sb, LockDraft L, double x, double y, double w, double h, float zStem, bool pressureBh)
    {
        sb.AppendLine($"<rect x=\"{F(x)}\" y=\"{F(y)}\" width=\"{F(w)}\" height=\"{F(h)}\" fill=\"#fff\" stroke=\"#333\"/>");
        var padL = 28.0;
        var padR = 40.0;
        var padTop = 22.0;
        var padBot = 28.0;
        var halfB = LoftHalfBeam(L, zStem);
        SampleLoftUpRange(L, zStem, out var upMin, out var upMax);
        var height = Math.Max(0.2, upMax - upMin);
        var scale = Math.Min((w - padL - padR) / Math.Max(0.2, halfB * 2), (h - padTop - padBot) / height) * 0.88;
        var cx = x + padL + (w - padL - padR) * 0.5;
        var keelY = y + h - padBot;
        var crownY = keelY - upMax * scale;
        var sectionKeelY = keelY - upMin * scale;

        var left = cx - halfB * scale;
        var right = cx + halfB * scale;
        var midUp = keelY - (upMin + upMax) * 0.5 * scale;

        // Section outline = interpolated loft station verts (lock SoT), not a fake chamfer box.
        var verts = StationVertsAt(L, zStem);
        if (verts.Count >= 3)
        {
            var d = new StringBuilder("M");
            for (var i = 0; i < verts.Count; i++)
            {
                var (vy, vu) = verts[i];
                d.Append(i == 0 ? " " : " L ");
                d.Append($"{F(cx - vy * scale)} {F(keelY - vu * scale)}");
            }

            d.Append(" Z");
            sb.AppendLine($"<path d=\"{d}\" fill=\"#eef2f6\" stroke=\"#111\" stroke-width=\"1.1\"/>");
        }

        // Spaces: clip Y to waterline AND Up to loft range (no poke-through).
        foreach (var c in L.Compartments.Where(c => c.Z0 - 0.02 <= zStem && zStem <= c.Z1 + 0.02))
        {
            var upLo = Math.Max(c.Up0, upMin);
            var upHi = Math.Min(c.Up1, upMax);
            if (upHi - upLo < 0.15)
                continue;
            var midSpaceUp = (upLo + upHi) * 0.5;
            var localHb = LoftWaterlineHalfBeam(L, zStem, midSpaceUp);
            if (localHb < 0.05)
                continue;
            var yLo = Math.Max(c.Y0, -localHb);
            var yHi = Math.Min(c.Y1, localHb);
            if (yHi - yLo < 0.05)
                continue;
            var x0 = cx - yHi * scale;
            var x1 = cx - yLo * scale;
            var u0 = keelY - upHi * scale;
            var u1 = keelY - upLo * scale;
            if (Math.Abs(u1 - u0) < 0.5)
                continue;
            sb.AppendLine(
                $"<rect x=\"{F(Math.Min(x0, x1))}\" y=\"{F(Math.Min(u0, u1))}\" width=\"{F(Math.Abs(x1 - x0))}\" height=\"{F(Math.Abs(u1 - u0))}\" fill=\"{SpaceFill(c)}\" fill-opacity=\"0.85\" stroke=\"#333\" stroke-width=\"0.55\"/>");
            if (c.Id is not null && ShouldLabel(c.Id) && Math.Abs(x1 - x0) > 14 && Math.Abs(u1 - u0) > 8)
            {
                sb.AppendLine(
                    $"<text x=\"{F((x0 + x1) * 0.5)}\" y=\"{F((u0 + u1) * 0.5 + 3)}\" text-anchor=\"middle\" font-size=\"7\" fill=\"#111\">{Esc(ShortId(c.Id))}</text>");
            }
        }

        var truePlatePx = L.TPlate * scale;
        var plateNts = truePlatePx < 1.0;
        var plateH = plateNts ? 3.5 : truePlatePx;
        foreach (var (name, dElev) in new[] { ("−1", L.DkM1), ("0", L.Dk0), ("+1", L.Dk1) })
        {
            if (dElev < upMin - 0.05 || dElev > upMax + 0.05)
                continue;
            var plateTop = keelY - dElev * scale;
            var hb = LoftWaterlineHalfBeam(L, zStem, dElev) * scale;
            sb.AppendLine(
                $"<rect x=\"{F(cx - hb)}\" y=\"{F(plateTop)}\" width=\"{F(hb * 2)}\" height=\"{F(plateH)}\" fill=\"#6a7078\" stroke=\"#222\" stroke-width=\"0.45\"/>");
            sb.AppendLine($"<text x=\"{F(cx + hb + 3)}\" y=\"{F(plateTop + 3)}\" font-size=\"8\" fill=\"#444\">DK {name}</text>");
        }

        sb.AppendLine($"<line x1=\"{F(cx)}\" y1=\"{F(crownY)}\" x2=\"{F(cx)}\" y2=\"{F(sectionKeelY)}\" stroke=\"#0645ad\" stroke-dasharray=\"4 3\" stroke-width=\"0.8\"/>");
        sb.AppendLine($"<line x1=\"{F(left)}\" y1=\"{F(midUp)}\" x2=\"{F(right)}\" y2=\"{F(midUp)}\" stroke=\"#0645ad\" stroke-dasharray=\"4 3\" stroke-width=\"0.8\"/>");

        if (pressureBh)
            sb.AppendLine($"<rect x=\"{F(left + 2)}\" y=\"{F(crownY + 2)}\" width=\"{F(right - left - 4)}\" height=\"{F(sectionKeelY - crownY - 4)}\" fill=\"none\" stroke=\"#c82828\" stroke-width=\"2\"/>");

        DimH(sb, left, sectionKeelY + 8, right, sectionKeelY + 8, $"B {Nice(halfB * 2)}");
        DimV(sb, right + 12, sectionKeelY, right + 12, crownY, $"H {Nice(height)}");
        if (L.Dk0 >= upMin - 0.05 && L.Dk1 <= upMax + 0.05)
            DimV(sb, left - 10, keelY - L.Dk0 * scale, left - 10, keelY - L.Dk1 * scale, Nice(L.Dk1 - L.Dk0));

        sb.AppendLine($"<text x=\"{F(cx)}\" y=\"{F(y + 14)}\" text-anchor=\"middle\" font-size=\"10\" fill=\"#333\">z={zStem:0.##} m · halfB={halfB:0.##} · H={height:0.##} · {ScaleNote(scale)}</text>");
        if (plateNts)
            sb.AppendLine($"<text x=\"{F(cx)}\" y=\"{F(y + h - 18)}\" text-anchor=\"middle\" font-size=\"7.5\" fill=\"#666\">{Esc(PlateThickLabel(L, true))}</text>");
        sb.AppendLine($"<text x=\"{F(left)}\" y=\"{F(y + h - 6)}\" font-size=\"9\" fill=\"#444\">P</text>");
        sb.AppendLine($"<text x=\"{F(right)}\" y=\"{F(y + h - 6)}\" text-anchor=\"end\" font-size=\"9\" fill=\"#444\">S</text>");
    }

    /// <summary>
    /// Side-on CL profile (stern left). Uses the sheet's shared plan scale for LOA and OAH
    /// (isotropic — same m/px as PLAN / DECK-BY-DECK). Does not stretch to fill the box.
    /// </summary>
    private static void DrawProfile(
        StringBuilder sb, LockDraft L, double x, double y, double w, double h, float zStem, double scale,
        IReadOnlyList<CadJsonStructureQuery.SandwichPanel> sandwiches)
    {
        sb.AppendLine($"<rect x=\"{F(x)}\" y=\"{F(y)}\" width=\"{F(w)}\" height=\"{F(h)}\" fill=\"#fff\" stroke=\"#333\"/>");
        var padL = 36.0;
        var padR = 16.0;
        var padTop = 14.0;
        var padBot = 22.0;
        var drawLoa = L.Loa * scale;
        var drawOah = L.Oah * scale;
        if (drawLoa > w - padL - padR || drawOah > h - padTop - padBot)
            throw new InvalidOperationException(
                $"Profile box {w}×{h} cannot hold LOA/OAH at shared scale {scale:0.###} px/m");
        var ox = x + padL + ((w - padL - padR) - drawLoa) * 0.5;
        var keelY = y + padTop + ((h - padTop - padBot) + drawOah) * 0.5;

        sb.AppendLine($"<path d=\"{HullProfilePath(L, ox, keelY, scale)}\" fill=\"#eef2f6\" stroke=\"none\"/>");
        // CL profile collapses athwart: one rect per unique (z0,z1,up0,up1); label first id only.
        var profileDrawn = new HashSet<string>(StringComparer.Ordinal);
        foreach (var c in L.Compartments.Where(c =>
                     MatchesDeck(c, 0) || c.DeckTag is "atrium" or "cargo" or "all" || c.Id is "HOLD" or "ENG" or "BRIDGE"))
        {
            var key = $"{c.Z0:0.###}|{c.Z1:0.###}|{c.Up0:0.###}|{c.Up1:0.###}";
            if (!profileDrawn.Add(key))
                continue;

            var midUp = (c.Up0 + c.Up1) * 0.5;
            var zLo = Math.Min(c.Z0, c.Z1);
            var zHi = Math.Max(c.Z0, c.Z1);
            const double step = 0.25;
            while (zLo < zHi - step)
            {
                SampleLoftUpRange(L, zLo, out var u0, out var u1);
                if (midUp >= u0 - 0.05 && midUp <= u1 + 0.05)
                    break;
                zLo += step;
            }

            while (zHi > zLo + step)
            {
                SampleLoftUpRange(L, zHi, out var u0, out var u1);
                if (midUp >= u0 - 0.05 && midUp <= u1 + 0.05)
                    break;
                zHi -= step;
            }

            if (zHi - zLo < 0.3)
                continue;

            var x0 = ShipX(ox, scale, L.Loa, zHi);
            var x1 = ShipX(ox, scale, L.Loa, zLo);
            var y0 = keelY - c.Up1 * scale;
            var y1 = keelY - c.Up0 * scale;
            var focus = c.Z0 - 0.05 <= zStem && zStem <= c.Z1 + 0.05;
            sb.AppendLine(
                $"<rect x=\"{F(Math.Min(x0, x1))}\" y=\"{F(Math.Min(y0, y1))}\" width=\"{F(Math.Abs(x1 - x0))}\" height=\"{F(Math.Abs(y1 - y0))}\" fill=\"{SpaceFill(c)}\" stroke=\"{(focus ? "#c82828" : "#333")}\" stroke-width=\"{(focus ? "1.1" : "0.5")}\"/>");
            if (c.Id is not null && ShouldLabel(c.Id) && Math.Abs(x1 - x0) > 28 && Math.Abs(y1 - y0) > 8)
                sb.AppendLine(
                    $"<text x=\"{F((x0 + x1) * 0.5)}\" y=\"{F((y0 + y1) * 0.5 + 3)}\" text-anchor=\"middle\" font-size=\"8\" fill=\"#111\">{Esc(ShortId(c.Id))}</text>");
        }

        sb.AppendLine($"<path d=\"{HullProfilePath(L, ox, keelY, scale)}\" fill=\"none\" stroke=\"#111\" stroke-width=\"1.15\"/>");
        // Sandwich deckplates from cadjson entities (finished floor = plate TOP).
        var truePlatePx = L.TPlate * scale;
        var plateNts = truePlatePx < 1.0;
        var plateHPx = plateNts ? 3.5 : truePlatePx;
        foreach (var (name, deckNum) in new[] { ("−1", -1), ("0", 0), ("+1", 1) })
        {
            var upTop = CadJsonStructureQuery.PlateTopForDeck(sandwiches, deckNum) ?? DeckElevation(L, deckNum);
            var yTop = keelY - upTop * scale;
            foreach (var (z0, z1) in CadJsonStructureQuery.MergedPlateSpans(sandwiches, deckNum))
                DrawProfilePlateBand(sb, ox, scale, L.Loa, z0, z1, yTop, plateHPx);
            sb.AppendLine($"<text x=\"{F(ox - 4)}\" y=\"{F(yTop + 3)}\" text-anchor=\"end\" font-size=\"8\" fill=\"#444\">DK {name}</text>");
        }

        DimV(sb, ox + drawLoa * 0.42, keelY - L.Dk0 * scale,
            ox + drawLoa * 0.42, keelY - L.Dk0 * scale + plateHPx,
            PlateThickLabel(L, plateNts));
        sb.AppendLine($"<text x=\"{F(ox + 4)}\" y=\"{F(keelY - L.Dk0 * scale - 4)}\" font-size=\"7\" fill=\"#666\">plate extent from deck-sandwich-* cadjson</text>");
        var midY = keelY - L.Oah * 0.5 * scale;
        sb.AppendLine($"<line x1=\"{F(ox)}\" y1=\"{F(midY)}\" x2=\"{F(ox + drawLoa)}\" y2=\"{F(midY)}\" stroke=\"#0645ad\" stroke-dasharray=\"2 3\" stroke-width=\"0.55\" opacity=\"0.7\"/>");
        sb.AppendLine($"<text x=\"{F(ox + drawLoa - 2)}\" y=\"{F(midY - 2)}\" text-anchor=\"end\" font-size=\"7.5\" fill=\"#0645ad\">CL mid (OAH/2)</text>");

        foreach (var (bhId, z, _) in L.BhSchedule)
        {
            var sx = ShipX(ox, scale, L.Loa, z);
            SampleLoftUpRange(L, z, out var upMin, out var upMax);
            var focus = Math.Abs(z - zStem) < 0.05f;
            sb.AppendLine(
                $"<line x1=\"{F(sx)}\" y1=\"{F(keelY - upMax * scale)}\" x2=\"{F(sx)}\" y2=\"{F(keelY - upMin * scale)}\" stroke=\"{(focus ? "#c82828" : "#886666")}\" stroke-width=\"{(focus ? "2.2" : "1.0")}\"/>");
            if (focus)
                sb.AppendLine($"<text x=\"{F(sx + 3)}\" y=\"{F(keelY - upMax * scale + 11)}\" font-size=\"9\" fill=\"#c82828\">{Esc(bhId)}</text>");
        }

        DimH(sb, ox, keelY + 10, ox + drawLoa, keelY + 10, $"LOA {Nice(L.Loa)}");
        DimV(sb, ox + drawLoa + 10, keelY, ox + drawLoa + 10, keelY - drawOah, $"OAH {Nice(L.Oah)}");
        sb.AppendLine($"<text x=\"{F(ox)}\" y=\"{F(y + 12)}\" font-size=\"9\" fill=\"#444\">STERN</text>");
        sb.AppendLine($"<text x=\"{F(ox + drawLoa)}\" y=\"{F(y + 12)}\" text-anchor=\"end\" font-size=\"9\" fill=\"#444\">BOW</text>");
    }

    /// <summary>
    /// Three plan strips at the sheet's shared plan scale (same m/px as PLAN — never re-fit).
    /// </summary>
    private static void DrawDeckStackAlongSpine(
        StringBuilder sb, LockDraft L, double x, double y, double w, double h, float zStem, double scale,
        IReadOnlyList<CadJsonStructureQuery.SandwichPanel> sandwiches)
    {
        sb.AppendLine($"<rect x=\"{F(x)}\" y=\"{F(y)}\" width=\"{F(w)}\" height=\"{F(h)}\" fill=\"#fff\" stroke=\"#333\"/>");
        var decks = new (string Name, int Num)[] { ("DK +1", 1), ("DK 0", 0), ("DK −1", -1) };
        var labelW = 48.0;
        var gap = 10.0;
        var padX = 12.0;
        var padBand = 18.0;
        var bandH = (h - 12 - gap * (decks.Length - 1)) / decks.Length;
        var drawLoa = L.Loa * scale;
        var drawBeam = L.Beam * scale;
        if (drawLoa > w - labelW - padX * 2 || drawBeam > bandH - padBand * 2)
            throw new InvalidOperationException(
                $"Deck stack band cannot hold LOA×BEAM at shared scale {scale:0.###} px/m " +
                $"(need {drawLoa:0.#}×{drawBeam:0.#} px, have {w - labelW - padX * 2:0.#}×{bandH - padBand * 2:0.#})");

        for (var i = 0; i < decks.Length; i++)
        {
            var (name, deckNum) = decks[i];
            var by = y + 6 + i * (bandH + gap);
            sb.AppendLine($"<rect x=\"{F(x + 2)}\" y=\"{F(by)}\" width=\"{F(w - 4)}\" height=\"{F(bandH)}\" fill=\"{(i % 2 == 0 ? "#faf7f0" : "#f3efe6")}\" stroke=\"#bbb\" stroke-width=\"0.4\"/>");
            sb.AppendLine($"<text x=\"{F(x + 6)}\" y=\"{F(by + 14)}\" font-size=\"10\" font-weight=\"600\" fill=\"#222\">{Esc(name)}</text>");
            var plateTop = CadJsonStructureQuery.PlateTopForDeck(sandwiches, deckNum) ?? DeckElevation(L, deckNum);
            sb.AppendLine($"<text x=\"{F(x + 6)}\" y=\"{F(by + 26)}\" font-size=\"7.5\" fill=\"#666\">plate top={Nice(plateTop)} m · cadjson sandwich</text>");
            sb.AppendLine($"<text x=\"{F(x + 6)}\" y=\"{F(by + 37)}\" font-size=\"7\" fill=\"#888\">hull lip = loft waterline</text>");

            var ox = x + labelW + padX + ((w - labelW - padX * 2) - drawLoa) * 0.5;
            var oy = by + bandH * 0.5;

            // Hull lip at this deck elevation (beam taper + height cut-off)
            DrawDeckHullLip(sb, L, ox, oy, scale, deckNum);
            sb.AppendLine($"<line x1=\"{F(ox)}\" y1=\"{F(oy)}\" x2=\"{F(ox + drawLoa)}\" y2=\"{F(oy)}\" stroke=\"#0645ad\" stroke-dasharray=\"5 2\" stroke-width=\"0.6\"/>");

            foreach (var c in L.Compartments.Where(c => MatchesDeck(c, deckNum)))
            {
                var focus = c.Z0 - 0.05 <= zStem && zStem <= c.Z1 + 0.05;
                DrawPlanFootprint(sb, L, c, ox, oy, scale, scale, SpaceFill(c),
                    focus ? "#c82828" : "#333", focus ? "1.15" : "0.5", deckNum);
                if (c.Id is not null && ShouldLabel(c.Id))
                {
                    var (cx, cy) = FootprintLabelPoint(L, c, ox, oy, scale, scale, deckNum);
                    if (Math.Abs(c.Z1 - c.Z0) * scale > 18 && Math.Abs(c.Y1 - c.Y0) * scale > 5)
                        sb.AppendLine(
                            $"<text x=\"{F(cx)}\" y=\"{F(cy + 3)}\" text-anchor=\"middle\" font-size=\"7.5\" fill=\"#111\">{Esc(ShortId(c.Id))}</text>");
                }
            }

            foreach (var (bhId, z, _) in L.BhSchedule)
            {
                if (!DeckExistsAt(L, deckNum, z))
                    continue;
                var sx = ShipX(ox, scale, L.Loa, z);
                var hb = DeckHalfBeamAt(L, deckNum, z) * scale;
                var focus = Math.Abs(z - zStem) < 0.05f;
                sb.AppendLine(
                    $"<line x1=\"{F(sx)}\" y1=\"{F(oy - hb)}\" x2=\"{F(sx)}\" y2=\"{F(oy + hb)}\" stroke=\"{(focus ? "#c82828" : "#aa7777")}\" stroke-width=\"{(focus ? "1.8" : "0.9")}\"/>");
                if (focus && i == 0)
                    sb.AppendLine($"<text x=\"{F(sx + 3)}\" y=\"{F(oy - hb - 3)}\" font-size=\"9\" fill=\"#c82828\">{Esc(bhId)}</text>");
            }

            var fx = ShipX(ox, scale, L.Loa, zStem);
            sb.AppendLine(
                $"<line x1=\"{F(fx)}\" y1=\"{F(by + 2)}\" x2=\"{F(fx)}\" y2=\"{F(by + bandH - 2)}\" stroke=\"#c82828\" stroke-dasharray=\"3 2\" stroke-width=\"0.7\" opacity=\"0.65\"/>");

            DimV(sb, ox + drawLoa + 6, oy - drawBeam * 0.5, ox + drawLoa + 6, oy + drawBeam * 0.5, Nice(L.Beam));

            if (i == 0)
            {
                var bhs = L.BhSchedule;
                var yBh = oy - drawBeam * 0.5 - 6;
                for (var j = 0; j < bhs.Count - 1; j++)
                {
                    DimH(sb, ShipX(ox, scale, L.Loa, bhs[j + 1].ZStem), yBh,
                        ShipX(ox, scale, L.Loa, bhs[j].ZStem), yBh,
                        Nice(bhs[j + 1].ZStem - bhs[j].ZStem));
                }
            }

            if (i == decks.Length - 1)
            {
                DimH(sb, ox, oy + drawBeam * 0.5 + 8, ox + drawLoa, oy + drawBeam * 0.5 + 8,
                    $"LOA {Nice(L.Loa)} · {ScaleNote(scale)}");
                sb.AppendLine($"<text x=\"{F(ox)}\" y=\"{F(by + 12)}\" font-size=\"8\" fill=\"#444\">STERN</text>");
                sb.AppendLine($"<text x=\"{F(ox + drawLoa)}\" y=\"{F(by + 12)}\" text-anchor=\"end\" font-size=\"8\" fill=\"#444\">BOW</text>");
            }
        }
    }

    private static void DimH(StringBuilder sb, double x1, double y, double x2, double y2, string label)
    {
        sb.AppendLine($"<line x1=\"{F(x1)}\" y1=\"{F(y)}\" x2=\"{F(x2)}\" y2=\"{F(y2)}\" stroke=\"#222\" stroke-width=\"0.45\"/>");
        sb.AppendLine($"<line x1=\"{F(x1)}\" y1=\"{F(y - 2.2)}\" x2=\"{F(x1)}\" y2=\"{F(y + 2.2)}\" stroke=\"#222\" stroke-width=\"0.45\"/>");
        sb.AppendLine($"<line x1=\"{F(x2)}\" y1=\"{F(y - 2.2)}\" x2=\"{F(x2)}\" y2=\"{F(y + 2.2)}\" stroke=\"#222\" stroke-width=\"0.45\"/>");
        sb.AppendLine(
            $"<text x=\"{F((x1 + x2) * 0.5)}\" y=\"{F(y - 2.5)}\" text-anchor=\"middle\" font-size=\"7.5\" fill=\"#111\">{Esc(label)}</text>");
    }

    private static void DimV(StringBuilder sb, double x, double y1, double x2, double y2, string label)
    {
        sb.AppendLine($"<line x1=\"{F(x)}\" y1=\"{F(y1)}\" x2=\"{F(x2)}\" y2=\"{F(y2)}\" stroke=\"#222\" stroke-width=\"0.45\"/>");
        sb.AppendLine($"<line x1=\"{F(x - 2.2)}\" y1=\"{F(y1)}\" x2=\"{F(x + 2.2)}\" y2=\"{F(y1)}\" stroke=\"#222\" stroke-width=\"0.45\"/>");
        sb.AppendLine($"<line x1=\"{F(x - 2.2)}\" y1=\"{F(y2)}\" x2=\"{F(x + 2.2)}\" y2=\"{F(y2)}\" stroke=\"#222\" stroke-width=\"0.45\"/>");
        sb.AppendLine(
            $"<text x=\"{F(x + 3)}\" y=\"{F((y1 + y2) * 0.5 + 2)}\" font-size=\"7.5\" fill=\"#111\">{Esc(label)}</text>");
    }

    private static string Nice(double v)
    {
        var q = Math.Round(v * 4) / 4;
        return Math.Abs(v - q) < 1e-6
            ? q.ToString("0.##", CultureInfo.InvariantCulture)
            : v.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private static string SpaceFill(LockComp c) => c.Id switch
    {
        "BRIDGE" => "#cfe0f0",
        "ENG" => "#b8c8d8",
        "HOLD" => "#d4c4a8",
        "CROSSING" => "#e8e0d0",
        "LOUNGE" => "#e4d8c8",
        "FUEL" => "#d0c8b8",
        "GALLEY" or "INFIRMARY" => "#f2efe6",
        _ when c.Id?.StartsWith("CORR", StringComparison.Ordinal) == true => "#dce8f4",
        _ when c.Id?.StartsWith("AIRLOCK", StringComparison.Ordinal) == true => "#e8eef5",
        _ when c.Id?.StartsWith("CREW_", StringComparison.Ordinal) == true => "#f2efe6",
        _ when c.Id?.StartsWith("PAX_", StringComparison.Ordinal) == true => "#ebe4d6",
        _ when c.DeckNum == 1 => "#efe8dc",
        _ when c.DeckNum == -1 => "#e2ddd4",
        _ => "#f7f4ea",
    };

    /// <summary>
    /// Plan footprint as a tapered polygon — never an AABB box through the fore taper.
    /// Samples Z and clips Y to the deck's local half-beam (planRing preferred as seed).
    /// </summary>
    private static void DrawPlanFootprint(
        StringBuilder sb, LockDraft L, LockComp c,
        double ox, double oy, double scaleZ, double scaleY,
        string fill, string stroke, string strokeWidth,
        int? deckNum = null)
    {
        var ring = BuildTaperedFootprint(L, c, deckNum);
        if (ring.Count < 3)
            return;
        var d = new StringBuilder("M");
        for (var i = 0; i < ring.Count; i++)
        {
            var (yy, zz) = ring[i];
            d.Append(i == 0 ? " " : " L ");
            d.Append($"{F(ShipX(ox, scaleZ, L.Loa, zz))},{F(oy - yy * scaleY)}");
        }

        d.Append(" Z");
        sb.AppendLine(
            $"<path d=\"{d}\" fill=\"{fill}\" stroke=\"{stroke}\" stroke-width=\"{strokeWidth}\"/>");
    }

    private static (double X, double Y) FootprintLabelPoint(
        LockDraft L, LockComp c, double ox, double oy, double scaleZ, double scaleY,
        int? deckNum = null)
    {
        var ring = BuildTaperedFootprint(L, c, deckNum);
        if (ring.Count >= 3)
        {
            var sx = 0.0;
            var sy = 0.0;
            foreach (var (yy, zz) in ring)
            {
                sx += ShipX(ox, scaleZ, L.Loa, zz);
                sy += oy - yy * scaleY;
            }

            return (sx / ring.Count, sy / ring.Count);
        }

        return (
            ShipX(ox, scaleZ, L.Loa, (c.Z0 + c.Z1) * 0.5),
            oy - (c.Y0 + c.Y1) * 0.5 * scaleY);
    }

    /// <summary>
    /// Footprint SoT: lock <c>planRing</c>, clipped to hullLoft waterline on the drawing deck
    /// (same nest rule as CAD <c>HalfBeamInside</c> — never invent a new taper).
    /// No planRing → sample AABB against <see cref="DeckHalfBeamAt"/> from hullLoft verts.
    /// </summary>
    private static List<(double Y, double Z)> BuildTaperedFootprint(
        LockDraft L, LockComp c, int? deckNum, int samples = 32)
    {
        if (c.PlanRing is { Count: >= 3 })
        {
            if (deckNum is not int d)
                return c.PlanRing.ToList();

            var ring = new List<(double Y, double Z)>(c.PlanRing.Count);
            foreach (var (y, z) in c.PlanRing)
            {
                if (!DeckExistsAt(L, d, z))
                    continue;
                var hb = DeckHalfBeamAt(L, d, z);
                if (hb < 0.12)
                    continue;
                ring.Add((Math.Clamp(y, -hb, hb), z));
            }

            if (ring.Count >= 3)
                return ring;
            // Fall through to AABB loft clip if ring collapsed outside the loft.
        }

        var z0 = Math.Min(c.Z0, c.Z1);
        var z1 = Math.Max(c.Z0, c.Z1);
        if (deckNum is int deck)
        {
            const double step = 0.1;
            while (z0 < z1 - step && !DeckExistsAt(L, deck, z0))
                z0 += step;
            while (z1 > z0 + step && !DeckExistsAt(L, deck, z1))
                z1 -= step;
            if (z1 - z0 < 0.2 || !DeckExistsAt(L, deck, (z0 + z1) * 0.5))
                return [];
        }

        var portEdge = new List<(double Y, double Z)>();
        var stbdEdge = new List<(double Y, double Z)>();
        for (var i = 0; i <= samples; i++)
        {
            var z = z0 + (z1 - z0) * i / samples;
            if (deckNum is int d && !DeckExistsAt(L, d, z))
                continue;
            var hb = deckNum is int dn ? DeckHalfBeamAt(L, dn, z) : LoftHalfBeam(L, z);
            var yHi = Math.Min(c.Y1, hb);
            var yLo = Math.Max(c.Y0, -hb);
            if (yHi < yLo + 0.02)
                continue;

            portEdge.Add((yHi, z));
            stbdEdge.Add((yLo, z));
        }

        if (portEdge.Count < 2)
            return [];

        var built = new List<(double Y, double Z)>(portEdge.Count * 2);
        built.AddRange(portEdge);
        for (var i = stbdEdge.Count - 1; i >= 0; i--)
            built.Add(stbdEdge[i]);
        return built;
    }

    private static void DrawProfilePlateBand(
        StringBuilder sb, double ox, double scale, double loa,
        double zFromStemA, double zFromStemB, double yTop, double plateHPx)
    {
        var x0 = ShipX(ox, scale, loa, zFromStemB);
        var x1 = ShipX(ox, scale, loa, zFromStemA);
        var x = Math.Min(x0, x1);
        var w = Math.Abs(x1 - x0);
        if (w < 0.5)
            return;
        // yTop = finished floor (plate top); band draws downward (toward keel).
        sb.AppendLine(
            $"<rect x=\"{F(x)}\" y=\"{F(yTop)}\" width=\"{F(w)}\" height=\"{F(plateHPx)}\" fill=\"#6a7078\" stroke=\"#222\" stroke-width=\"0.4\" opacity=\"0.92\"/>");
    }

    private static double DeckElevation(LockDraft L, int deck) => deck switch
    {
        -1 => L.DkM1,
        1 => L.Dk1,
        _ => L.Dk0,
    };

    /// <summary>Deck floor lies inside loft station up-range at this z (from hullLoft verts).</summary>
    private static bool DeckExistsAt(LockDraft L, int deck, double zFromStem)
    {
        var up = DeckElevation(L, deck);
        SampleLoftUpRange(L, zFromStem, out var upMin, out var upMax);
        return up >= upMin - 0.05 && up <= upMax + 0.05 && DeckHalfBeamAt(L, deck, zFromStem) >= 0.2;
    }

    /// <summary>Half-beam at deck waterline from interpolated hullLoft station polygons.</summary>
    private static double DeckHalfBeamAt(LockDraft L, int deck, double zFromStem) =>
        LoftWaterlineHalfBeam(L, zFromStem, DeckElevation(L, deck));

    private static void DrawDeckHullLip(StringBuilder sb, LockDraft L, double ox, double oy, double scale, int deck)
    {
        // Sample at every lock loft station + dense fill — same Z set as the CAD loft.
        var zs = new SortedSet<double>();
        foreach (var st in L.Loft)
            zs.Add(st.Z);
        for (var z = 0.0; z <= L.Loa + 1e-6; z += 0.5)
            zs.Add(z);

        var usable = zs.Where(z => DeckExistsAt(L, deck, z)).ToList();
        if (usable.Count < 2)
            return;

        var d = new StringBuilder("M");
        foreach (var z in usable)
            d.Append($" {F(ShipX(ox, scale, L.Loa, z))},{F(oy - DeckHalfBeamAt(L, deck, z) * scale)}");
        for (var i = usable.Count - 1; i >= 0; i--)
            d.Append($" {F(ShipX(ox, scale, L.Loa, usable[i]))},{F(oy + DeckHalfBeamAt(L, deck, usable[i]) * scale)}");
        d.Append(" Z");
        sb.AppendLine($"<path d=\"{d}\" fill=\"none\" stroke=\"#666\" stroke-width=\"0.7\"/>");
    }

    private static StringBuilder SheetHead(string title, string sub)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html><html><head><meta charset=\"utf-8\"/>");
        sb.AppendLine($"<title>{Esc(title)}</title>");
        sb.AppendLine("<style>body{margin:0;background:#ebe6dc;font:12px \"IBM Plex Mono\",Consolas,monospace;color:#222}");
        sb.AppendLine("h1{font:600 16px system-ui;margin:12px 16px 4px} p{margin:0 16px 8px;color:#444}</style></head><body>");
        sb.AppendLine($"<h1>{Esc(title)}</h1><p>{Esc(sub)} — ISO 128-15 stern left / bow right</p>");
        return sb;
    }

    private static string SvgOpen(int w, int h) =>
        $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{w}\" height=\"{h}\" viewBox=\"0 0 {w} {h}\">" +
        $"<rect width=\"{w}\" height=\"{h}\" fill=\"#f7f3ea\" stroke=\"#333\"/>";

    private static string Label(double x, double y, string t) =>
        $"<text x=\"{F(x)}\" y=\"{F(y)}\" fill=\"#333\" font-size=\"11\" font-weight=\"600\">{Esc(t)}</text>";

    private static double ShipX(double ox, double scale, double loa, double zFromStem) =>
        ox + (loa - zFromStem) * scale;

    private static string HullPlanPath(LockDraft L, double ox, double oy, double scale)
    {
        var zs = L.Loft.Select(s => s.Z).ToList();
        if (zs.Count < 2)
            zs = [0, L.Loa];
        var sb = new StringBuilder("M");
        foreach (var z in zs)
            sb.Append($" {F(ShipX(ox, scale, L.Loa, z))},{F(oy - LoftHalfBeam(L, z) * scale)}");
        for (var i = zs.Count - 1; i >= 0; i--)
            sb.Append($" {F(ShipX(ox, scale, L.Loa, zs[i]))},{F(oy + LoftHalfBeam(L, zs[i]) * scale)}");
        sb.Append(" Z");
        return sb.ToString();
    }

    /// <summary>Profile outline from loft verts: crown (max up) then keel (min up), absolute from keel datum.</summary>
    private static string HullProfilePath(LockDraft L, double ox, double keelY, double scale)
    {
        var zs = L.Loft.Select(s => s.Z).ToList();
        if (zs.Count < 2)
            zs = [0, L.Loa];
        var sb = new StringBuilder("M");
        foreach (var z in zs)
        {
            SampleLoftUpRange(L, z, out _, out var upMax);
            sb.Append($" {F(ShipX(ox, scale, L.Loa, z))},{F(keelY - upMax * scale)}");
        }

        for (var i = zs.Count - 1; i >= 0; i--)
        {
            SampleLoftUpRange(L, zs[i], out var upMin, out _);
            sb.Append($" {F(ShipX(ox, scale, L.Loa, zs[i]))},{F(keelY - upMin * scale)}");
        }

        sb.Append(" Z");
        return sb.ToString();
    }

    private static double LoftHalfBeam(LockDraft L, double z) =>
        LockDraftGeometry.LoftHalfBeam(L.Loft, z);

    private static List<(double Y, double Up)> StationVertsAt(LockDraft L, double z) =>
        LockDraftGeometry.StationVertsAt(L.Loft, z);

    private static double LoftWaterlineHalfBeam(LockDraft L, double z, double up) =>
        LockDraftGeometry.LoftWaterlineHalfBeam(L.Loft, z, up);

    private static void SampleLoftUpRange(LockDraft L, double z, out double upMin, out double upMax) =>
        LockDraftGeometry.SampleLoftUpRange(L.Loft, z, L.Oah, out upMin, out upMax);

    private static bool MatchesDeck(LockComp c, int deck)
    {
        if (c.DeckTag is "all" or "atrium" or "cargo")
            return true;
        return c.DeckNum == deck;
    }

    private static bool ShouldLabel(string id) =>
        id is "BRIDGE" or "ENG" or "HOLD" or "GALLEY" or "INFIRMARY" or "CROSSING" or "LOUNGE" or "ACCESS"
        || id.StartsWith("CORR", StringComparison.Ordinal)
        || id.StartsWith("CREW_", StringComparison.Ordinal)
        || id.StartsWith("PAX_", StringComparison.Ordinal)
        || id.StartsWith("AIRLOCK", StringComparison.Ordinal);

    private static string ShortId(string id) => id switch
    {
        "INFIRMARY" => "INFIRM",
        "CROSSING" => "CROSS",
        _ => id.Length <= 10 ? id : id[..10],
    };

    private static string F(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);
    private static string Esc(string s) =>
        s.Replace("&", "&amp;", StringComparison.Ordinal).Replace("<", "&lt;", StringComparison.Ordinal);

    private sealed class LockDraft
    {
        public double Loa { get; init; }
        public double Beam { get; init; }
        public double Oah { get; init; }
        public double DkM1 { get; init; }
        public double Dk0 { get; init; }
        public double Dk1 { get; init; }
        /// <summary>Sandwich deckplate thickness (m). Finished floor = plate top.</summary>
        public double TPlate { get; init; } = 0.1;
        public double RoomH { get; init; } = 3.2;
        public float FrameBayM { get; init; } = 4f;
        public List<(string Id, float ZStem, float ThicknessScale)> BhSchedule { get; init; } = [];
        public List<LockComp> Compartments { get; init; } = [];
        public List<LockDraftGeometry.LoftStation> Loft { get; init; } = [];

        public static LockDraft From(JsonElement root)
        {
            var env = root.GetProperty("envelope");
            var decks = root.GetProperty("decks");
            var comps = new List<LockComp>();
            foreach (var c in root.GetProperty("compartments").EnumerateArray())
                comps.Add(LockComp.From(c));

            var loft = LockDraftGeometry.ParseLoft(root);
            var tPlate = decks.TryGetProperty("tPlate", out var tp) ? tp.GetDouble() : 0.1;
            var roomH = decks.TryGetProperty("roomH", out var rh) ? rh.GetDouble() : 3.2;
            var loa = env.GetProperty("LOA").GetDouble();
            var bayM = 4f;
            if (root.TryGetProperty("frames", out var fr) && fr.TryGetProperty("bayM", out var bay))
                bayM = (float)bay.GetDouble();

            var stations = CalypsoBulkheads.StationsFromLock(root);
            var bh = CalypsoBulkheads.BuildSchedule((float)loa, stations);

            return new LockDraft
            {
                Loa = loa,
                Beam = env.GetProperty("BEAM").GetDouble(),
                Oah = env.GetProperty("OAH").GetDouble(),
                DkM1 = decks.GetProperty("m1").GetDouble(),
                Dk0 = decks.GetProperty("d0").GetDouble(),
                Dk1 = decks.GetProperty("d1").GetDouble(),
                TPlate = tPlate > 0 ? tPlate : 0.1,
                RoomH = roomH > 0 ? roomH : 3.2,
                FrameBayM = bayM > 0 ? bayM : 4f,
                BhSchedule = bh.Select(s => (s.Id, s.ZStem, s.ThicknessScale)).ToList(),
                Compartments = comps,
                Loft = loft,
            };
        }
    }

    private sealed class LockComp
    {
        public string? Id { get; init; }
        public int? DeckNum { get; init; }
        public string? DeckTag { get; init; }
        public double Z0 { get; init; }
        public double Z1 { get; init; }
        public double Y0 { get; init; }
        public double Y1 { get; init; }
        public double Up0 { get; init; }
        public double Up1 { get; init; }
        public List<(double Y, double Z)>? PlanRing { get; init; }

        public static LockComp From(JsonElement c)
        {
            int? deckNum = null;
            string? deckTag = null;
            var deck = c.GetProperty("deck");
            if (deck.ValueKind == JsonValueKind.Number)
                deckNum = deck.GetInt32();
            else
                deckTag = deck.GetString();

            List<(double Y, double Z)>? ring = null;
            if (c.TryGetProperty("planRing", out var pr) && pr.ValueKind == JsonValueKind.Array)
            {
                ring = [];
                foreach (var pt in pr.EnumerateArray())
                {
                    var arr = pt.EnumerateArray().ToArray();
                    if (arr.Length >= 2)
                        ring.Add((arr[0].GetDouble(), arr[1].GetDouble()));
                }
            }

            return new LockComp
            {
                Id = c.GetProperty("id").GetString(),
                DeckNum = deckNum,
                DeckTag = deckTag,
                Z0 = c.GetProperty("z0").GetDouble(),
                Z1 = c.GetProperty("z1").GetDouble(),
                Y0 = c.GetProperty("y0").GetDouble(),
                Y1 = c.GetProperty("y1").GetDouble(),
                Up0 = c.GetProperty("up0").GetDouble(),
                Up1 = c.GetProperty("up1").GetDouble(),
                PlanRing = ring is { Count: >= 3 } ? ring : null,
            };
        }
    }
}
