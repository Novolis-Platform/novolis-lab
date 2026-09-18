using System.Globalization;
using System.Text;
using System.Text.Json;

namespace CalypsoCad.Generation.Blueprints;

/// <summary>
/// Emits ISO 5457 A1 landscape HTML+SVG general-arrangement sheets for Calypso internals.
/// Conventions: ISO 128-15 (stern left / bow right; profile above plan), ASME Y14 / ISO 7200 title block,
/// ISO 128-25 line weights. Reads CAL-INT-GA-001.json — does not invent geometry.
/// </summary>
internal static class InternalsBlueprintEmitter
{
    private const string Rev = "F";
    private const string StdNote = "INTERPRET PER ISO 128-15 / ISO 5457 / ISO 7200. UNITS: METRES. STERN LEFT, BOW RIGHT.";

    private enum DeckAnnotMode
    {
        /// <summary>CAD figured dimensions on the as-locked geometry.</summary>
        CadDimensions,

        /// <summary>Type C0n expected clear / with-walls overlays on the same plans.</summary>
        ExpectedOverlay,
    }

    // ISO 5457 A1 landscape trimmed (mm)
    private const double SheetW = 841;
    private const double SheetH = 594;
    private const double BorderL = 20;
    private const double Border = 10;
    private const double FrameX = BorderL;
    private const double FrameY = Border;
    private const double FrameW = SheetW - BorderL - Border; // 811
    private const double FrameH = SheetH - 2 * Border; // 574

    // ISO 7200-style title block (mm) — bottom-right of drawing space
    private const double TbW = 180;
    private const double TbH = 56;

    public static string Emit(string? lockJsonPath = null, string? outputDirectory = null)
    {
        var jsonPath = lockJsonPath ?? ResolveLockPath();
        var outDir = outputDirectory ?? Path.GetDirectoryName(jsonPath)
                     ?? throw new InvalidOperationException("No output directory");
        Directory.CreateDirectory(outDir);

        InternalsLockCabinPatch.Apply(jsonPath);
        InternalsLockCorridorPatch.Apply(jsonPath);

        using var doc = JsonDocument.Parse(File.ReadAllText(jsonPath));
        var root = doc.RootElement;
        var lockDoc = LockSnapshot.From(root);

        var written = new List<string>();
        written.Add(Write(outDir, "CAL-INT-GA-001.html", BuildGaSheet(lockDoc)));
        written.Add(Write(outDir, "CAL-INT-DK-001.html", BuildDeckSheet(lockDoc, DeckAnnotMode.CadDimensions)));
        written.Add(Write(outDir, "CAL-INT-DK-002.html", BuildDeckSheet(lockDoc, DeckAnnotMode.ExpectedOverlay)));
        written.Add(Write(outDir, "CAL-INT-SCH-001.html", BuildScheduleSheet(lockDoc)));

        // Retire prior web-style sheets so the package is not ambiguous.
        foreach (var stale in new[]
                 {
                     "CAL-INT-DK0-001.html", "CAL-INT-DKM1-001.html", "CAL-INT-DKP1-001.html",
                     "CAL-INT-PRF-001.html", "CAL-INT-SEC-001.html", "CAL-INT-HTC-001.html",
                     "CAL-INT-HOLD-001.html",
                 })
        {
            var p = Path.Combine(outDir, stale);
            if (File.Exists(p))
                File.Delete(p);
        }

        Console.WriteLine($"Wrote {written.Count} blueprint sheets (Rev {Rev}) → {outDir}");
        foreach (var w in written)
            Console.WriteLine($"  {Path.GetFileName(w)}");
        return outDir;
    }

    private static string Write(string dir, string name, string html)
    {
        var path = Path.Combine(dir, name);
        File.WriteAllText(path, html, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        return path;
    }

    private static string ResolveLockPath()
    {
        // Prefer the docs package (fabrication SoT), not a copied bin/lock artifact.
        foreach (var p in new[]
                 {
                     @"d:\novolis\novolis-lab\labs\cad\CalypsoCad\docs\internals\CAL-INT-GA-001.json",
                     Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "docs", "internals",
                         "CAL-INT-GA-001.json")),
                     Path.Combine(AppContext.BaseDirectory, "lock", "CAL-INT-GA-001.json"),
                 })
        {
            if (File.Exists(p))
                return p;
        }

        throw new FileNotFoundException("CAL-INT-GA-001.json not found");
    }

    // ─── Sheets ──────────────────────────────────────────────────────────────

    private static string BuildGaSheet(LockSnapshot L)
    {
        var drawX = FrameX + 8;
        var drawY = FrameY + 8;
        var drawW = FrameW - TbW - 16;
        var drawH = FrameH - 16;

        var profileH = drawH * 0.42;
        var planH = drawH * 0.52;
        var gap = drawH * 0.06;

        var sb = new StringBuilder(48_000);
        BeginSvg(sb, "CAL-INT-GA-001", "GENERAL ARRANGEMENT — PROFILE & DECK 0", L);
        SheetChrome(sb, "CAL-INT-GA-001", "GENERAL ARRANGEMENT", "PROFILE (CL) + DECK 0 PLAN", "1:200", L);

        // VIEW A — profile
        ViewLabel(sb, drawX, drawY + 10, "A — SIDE ELEVATION (CL)");
        DrawProfile(sb, L, drawX + 10, drawY + 18, drawW - 20, profileH - 28, out var sProf);
        ScaleCallout(sb, drawX + 10, drawY + profileH - 6, sProf, "PROFILE");

        // VIEW B — deck 0 plan
        var planY = drawY + profileH + gap;
        ViewLabel(sb, drawX, planY + 10, "B — DECK 0 PLAN");
        DrawDeckPlan(sb, L, 0, drawX + 10, planY + 18, drawW - 20, planH - 28, out var sPlan,
            DeckAnnotMode.CadDimensions);
        ScaleCallout(sb, drawX + 10, planY + planH - 6, sPlan, "PLAN");

        NotesBlock(sb, FrameX + 8, FrameY + FrameH - TbH - 78, 220, 70, new[]
        {
            "1. CLEAR DIMS INSIDE FINISHES; CABIN MODULE O/A 2.0 (= 1.92 CLEAR).",
            "2. HATCHES ARE OPENINGS (NOT SYMBOLS ON SOLID WALL).",
            "3. OUTER SKIN: CAL-HULL-GA-001 (SAME LOA).",
            "4. MATERIAL LINING REF: AISI 316L SHELL t=8 mm.",
            "5. DO NOT SCALE — USE FIGURED DIMENSIONS.",
        });

        EndSvg(sb);
        return WrapHtml("CAL-INT-GA-001", "General arrangement", sb.ToString());
    }

    private static string BuildDeckSheet(LockSnapshot L, DeckAnnotMode mode)
    {
        var drawX = FrameX + 8;
        var drawY = FrameY + 8;
        var drawW = FrameW - TbW - 16;
        var drawH = FrameH - 16;
        var rowH = (drawH - 24) / 3.0;

        var dwg = mode == DeckAnnotMode.CadDimensions ? "CAL-INT-DK-001" : "CAL-INT-DK-002";
        var title = mode == DeckAnnotMode.CadDimensions ? "DECK PLANS — DIMENSIONS" : "DECK PLANS — C0n OVERLAY";
        var subtitle = mode == DeckAnnotMode.CadDimensions
            ? "DECKS −1, 0, +1 · FIGURED CLEAR / MODULE"
            : "DECKS −1, 0, +1 · TYPE C0n EXPECTED SIZES";

        var sb = new StringBuilder(80_000);
        BeginSvg(sb, dwg, title, L);
        SheetChrome(sb, dwg, title, subtitle, "1:250", L);

        var decks = new (int d, string name)[] { (-1, "C — DECK −1"), (0, "D — DECK 0"), (1, "E — DECK +1") };
        for (var i = 0; i < decks.Length; i++)
        {
            var (d, name) = decks[i];
            var y = drawY + i * rowH;
            ViewLabel(sb, drawX, y + 10, name);
            DrawDeckPlan(sb, L, d, drawX + 10, y + 16, drawW - 20, rowH - 22, out _, mode);
        }

        var notes = mode == DeckAnnotMode.CadDimensions
            ? new[]
            {
                "1. FIGURED DIMS ARE CLEAR INSIDE FINISHES UNLESS MARKED “O/A”.",
                $"2. C0n MODULE O/A {InternalsLockCabinPatch.ModuleW:0.##} m (= CLEAR {InternalsLockCabinPatch.ClearW:0.##} + WALL {InternalsLockCabinPatch.WallT:0.##}).",
                $"3. CABIN CLEAR F–A {InternalsLockCabinPatch.ClearD:0.##} m; LEFTOVER EXTENDS INF / GAL / STORE.",
                $"4. SPINE CORRIDORS {InternalsLockCorridorPatch.CorrW:0.#} m CLEAR (TRAVERSABLE) — STACK ±{InternalsLockCabinPatch.Stack:0.#} = 5×{InternalsLockCabinPatch.ModuleW:0.#}.",
            }
            : new[]
            {
                "1. GREEN DASH = TYPE C0n CLEAR CELL (1.92 × 7.2).",
                "2. AMBER DASH = MODULE ENVELOPE WITH WALLS (2.0 m ATHWART).",
                "3. LOCK GEOMETRY SHOULD COINCIDE WITH OVERLAYS AFTER REV F PATCH.",
                $"4. CORRIDORS REMAIN {InternalsLockCorridorPatch.CorrW:0.#} m CLEAR — DO NOT GROW STACK PAST ±{InternalsLockCabinPatch.Stack:0.#}.",
            };

        NotesBlock(sb, FrameX + 8, FrameY + FrameH - TbH - 72, 280, 64, notes);

        EndSvg(sb);
        return WrapHtml(dwg, mode == DeckAnnotMode.CadDimensions ? "Deck plans — dimensions" : "Deck plans — C0n overlay",
            sb.ToString());
    }

    private static string BuildScheduleSheet(LockSnapshot L)
    {
        var sb = new StringBuilder(48_000);
        BeginSvg(sb, "CAL-INT-SCH-001", "HATCH SCHEDULE & HOLD PACK", L);
        SheetChrome(sb, "CAL-INT-SCH-001", "SCHEDULE & HOLD", "HATCH OPENINGS + CARGO HOLD", "AS NOTED", L);

        var tableX = FrameX + 12;
        var tableY = FrameY + 18;
        var tableW = FrameW - TbW - 28;
        HatchTable(sb, L, tableX, tableY, tableW, 310);

        var holdY = tableY + 318;
        ViewLabel(sb, tableX, holdY, "F — HOLD PACK (PLAN)  SCALE 1:150");
        DrawHoldPlan(sb, L, tableX + 10, holdY + 12, tableW - 20, 140, out double _);

        NotesBlock(sb, FrameX + 8, FrameY + FrameH - TbH - 70, 260, 62, new[]
        {
            "1. PERSONNEL LEAF CLEAR 1.0 × 2.1 UNLESS NOTED.",
            "2. CORRIDOR / WT LEAF CLEAR 2.0 × 2.1.",
            "3. AFT CARGO DOOR CLEAR 14.0 × 8.5; SILL 0.25.",
            "4. C40 GRID 5×1×3; APRON ≥ 12.25 AFT OF DOOR.",
        });

        EndSvg(sb);
        return WrapHtml("CAL-INT-SCH-001", "Hatch schedule and hold", sb.ToString());
    }

    // ─── Drawing primitives ──────────────────────────────────────────────────

    private static void DrawProfile(StringBuilder sb, LockSnapshot L, double x, double y, double w, double h, out double scale)
    {
        scale = Math.Min(w / L.Loa, h / L.Oah) * 0.92;
        var ox = x + (w - L.Loa * scale) * 0.5;
        var oy = y + h - 8; // keel baseline

        // CL long-dashed dotted (ISO centre line)
        CenterLine(sb, ox, oy - L.Oah * scale * 0.5, ox + L.Loa * scale, oy - L.Oah * scale * 0.5);

        // Hull outline (soft chamfer box silhouette at stations)
        sb.Append($"<path d=\"{HullProfilePath(L, ox, oy, scale)}\" class=\"ol\" fill=\"none\"/>");

        // Decks
        foreach (var (name, up) in new[] { ("−1", L.DkM1), ("0", L.Dk0), ("+1", L.Dk1) })
        {
            var yy = oy - up * scale;
            sb.Append($"<line x1=\"{F(ox)}\" y1=\"{F(yy)}\" x2=\"{F(ox + L.Loa * scale)}\" y2=\"{F(yy)}\" class=\"thin\"/>");
            sb.Append($"<text x=\"{F(ox - 4)}\" y=\"{F(yy + 1)}\" class=\"anno\" text-anchor=\"end\">DK {name}</text>");
        }

        // Key compartments as thin rectangles on CL band
        foreach (var c in L.Compartments.Where(c => MatchesDeck(c, 0) || c.DeckTag is "atrium" or "cargo"))
        {
            var x0 = ShipX(ox, scale, L.Loa, c.Z1); // aft edge (leftward = larger z)
            var x1 = ShipX(ox, scale, L.Loa, c.Z0);
            var y0 = oy - c.Up1 * scale;
            var y1 = oy - c.Up0 * scale;
            sb.Append(
                $"<rect x=\"{F(Math.Min(x0, x1))}\" y=\"{F(Math.Min(y0, y1))}\" width=\"{F(Math.Abs(x1 - x0))}\" height=\"{F(Math.Abs(y1 - y0))}\" class=\"space\"/>");
            if (c.Id is "BRIDGE" or "ENG" or "HOLD" or "CREW")
            {
                sb.Append(
                    $"<text x=\"{F((x0 + x1) * 0.5)}\" y=\"{F((y0 + y1) * 0.5 + 1.2)}\" class=\"room\" text-anchor=\"middle\">{Esc(c.Id)}</text>");
            }
        }

        // Overall LOA dimension
        DimH(sb, ox, oy + 6, ox + L.Loa * scale, oy + 6, $"LOA {Nice(L.Loa)}");
        DimV(sb, ox + L.Loa * scale + 8, oy, ox + L.Loa * scale + 8, oy - L.Oah * scale, $"OAH {Nice(L.Oah)}");

        // Orientation
        sb.Append($"<text x=\"{F(ox)}\" y=\"{F(y + 8)}\" class=\"anno\">STERN</text>");
        sb.Append($"<text x=\"{F(ox + L.Loa * scale)}\" y=\"{F(y + 8)}\" class=\"anno\" text-anchor=\"end\">BOW</text>");
    }

    private static void DrawDeckPlan(StringBuilder sb, LockSnapshot L, int deck, double x, double y, double w, double h,
        out double scale, DeckAnnotMode mode = DeckAnnotMode.CadDimensions)
    {
        scale = Math.Min(w / L.Loa, h / L.Beam) * 0.90;
        var ox = x + (w - L.Loa * scale) * 0.5;
        var oy = y + h * 0.5; // CL

        CenterLine(sb, ox, oy, ox + L.Loa * scale, oy);
        sb.Append(
            $"<line x1=\"{F(ox)}\" y1=\"{F(oy - L.Beam * 0.5 * scale)}\" x2=\"{F(ox + L.Loa * scale)}\" y2=\"{F(oy - L.Beam * 0.5 * scale)}\" class=\"thin\"/>");
        sb.Append(
            $"<line x1=\"{F(ox)}\" y1=\"{F(oy + L.Beam * 0.5 * scale)}\" x2=\"{F(ox + L.Loa * scale)}\" y2=\"{F(oy + L.Beam * 0.5 * scale)}\" class=\"thin\"/>");

        sb.Append($"<path d=\"{HullPlanPath(L, ox, oy, scale)}\" class=\"ol\" fill=\"none\"/>");

        foreach (var z in L.BhStations)
        {
            var sx = ShipX(ox, scale, L.Loa, z);
            var hb = HullBeamAt(L, z) * 0.5 * scale;
            sb.Append(
                $"<line x1=\"{F(sx)}\" y1=\"{F(oy - hb)}\" x2=\"{F(sx)}\" y2=\"{F(oy + hb)}\" class=\"bh\"/>");
        }

        foreach (var yFace in new[]
                 {
                     -InternalsLockCorridorPatch.CorrOuter, -InternalsLockCorridorPatch.CorrInner,
                     InternalsLockCorridorPatch.CorrInner, InternalsLockCorridorPatch.CorrOuter,
                 })
        {
            if (L.CorrZ1 <= L.CorrZ0)
                continue;
            var x0 = ShipX(ox, scale, L.Loa, L.CorrZ1);
            var x1 = ShipX(ox, scale, L.Loa, L.CorrZ0);
            var sy = oy - yFace * scale;
            sb.Append(
                $"<line x1=\"{F(Math.Min(x0, x1))}\" y1=\"{F(sy)}\" x2=\"{F(Math.Max(x0, x1))}\" y2=\"{F(sy)}\" class=\"bh\"/>");
        }

        var spaces = L.Compartments.Where(c => MatchesDeck(c, deck)).ToList();
        if (deck == 0)
            spaces.AddRange(L.Airlocks);

        foreach (var c in spaces)
            DrawSpaceFootprint(sb, L, c, ox, oy, scale);

        foreach (var hch in L.Hatches.Where(ht => ht.Deck == deck))
            DrawHatchOnBulkhead(sb, L, hch, ox, oy, scale);

        if (mode == DeckAnnotMode.CadDimensions && deck is 0 or 1)
            AnnotateCabinCadDims(sb, L, deck, ox, oy, scale, y, h);
        else if (mode == DeckAnnotMode.ExpectedOverlay && deck is 0 or 1)
            AnnotateCabinC0nOverlay(sb, L, deck, ox, oy, scale);

        sb.Append($"<text x=\"{F(ox)}\" y=\"{F(y + h - 2)}\" class=\"anno\">STERN</text>");
        sb.Append($"<text x=\"{F(ox + L.Loa * scale)}\" y=\"{F(y + h - 2)}\" class=\"anno\" text-anchor=\"end\">BOW</text>");
        sb.Append($"<text x=\"{F(ox + L.Loa * scale + 4)}\" y=\"{F(oy - L.Beam * 0.25 * scale)}\" class=\"anno\">P</text>");
        sb.Append($"<text x=\"{F(ox + L.Loa * scale + 4)}\" y=\"{F(oy + L.Beam * 0.25 * scale)}\" class=\"anno\">S</text>");
    }

    /// <summary>Figured dims for C0n band + corridors + extended INF/GAL/STORE.</summary>
    private static void AnnotateCabinCadDims(StringBuilder sb, LockSnapshot L, int deck, double ox, double oy,
        double scale, double viewY, double viewH)
    {
        var prefix = deck == 0 ? "CREW_" : "PAX_";
        var cabins = L.Compartments
            .Where(c => c.Id is not null && c.Id.StartsWith(prefix, StringComparison.Ordinal) && MatchesDeck(c, deck))
            .OrderBy(c => c.Y0)
            .ToList();
        if (cabins.Count == 0)
            return;

        var c0 = cabins[0];
        var cLast = cabins[^1];
        var stackY0 = -InternalsLockCabinPatch.Stack;
        var stackY1 = InternalsLockCabinPatch.Stack;

        // Athwart chain: module O/A ticks across mid stack (above plan)
        var yDim = oy - stackY1 * scale - 6;
        var xFore = ShipX(ox, scale, L.Loa, c0.Z0);
        for (var i = 0; i <= InternalsLockCabinPatch.CabinCount; i++)
        {
            var yFace = stackY0 + i * InternalsLockCabinPatch.ModuleW;
            var py = oy - yFace * scale;
            sb.Append($"<line x1=\"{F(xFore - 1)}\" y1=\"{F(py)}\" x2=\"{F(xFore + 3)}\" y2=\"{F(py)}\" class=\"dim\"/>");
        }

        for (var i = 0; i < InternalsLockCabinPatch.CabinCount; i++)
        {
            var yA = stackY0 + i * InternalsLockCabinPatch.ModuleW;
            var yB = yA + InternalsLockCabinPatch.ModuleW;
            DimV(sb, xFore + 5, oy - yA * scale, xFore + 5, oy - yB * scale,
                i == 2 ? $"O/A {Nice(InternalsLockCabinPatch.ModuleW)}" : Nice(InternalsLockCabinPatch.ModuleW));
        }

        // Clear width on mid cabin
        var mid = cabins[cabins.Count / 2];
        var xMid = ShipX(ox, scale, L.Loa, (mid.Z0 + mid.Z1) * 0.5);
        DimV(sb, xMid + 4, oy - mid.Y0 * scale, xMid + 4, oy - mid.Y1 * scale,
            $"CLR {Nice(mid.Y1 - mid.Y0)}");

        // Cabin clear F–A
        var yFa = oy - stackY0 * scale + 5;
        DimH(sb, ShipX(ox, scale, L.Loa, c0.Z1), yFa, ShipX(ox, scale, L.Loa, c0.Z0), yFa,
            $"CAB CLR F–A {Nice(c0.Z1 - c0.Z0)}");

        // Corridor clear (port)
        var corr = L.Compartments.FirstOrDefault(c => c.Id == "CORR_P" && MatchesDeck(c, deck));
        if (corr is not null)
        {
            var zCorr = (corr.Z0 + corr.Z1) * 0.5;
            var xc = ShipX(ox, scale, L.Loa, zCorr);
            DimV(sb, xc - 6, oy - corr.Y0 * scale, xc - 6, oy - corr.Y1 * scale,
                $"CORR CLR {Nice(Math.Abs(corr.Y1 - corr.Y0))}");
        }

        // Service pack F–A (INF/GAL or STORE)
        Comp? svc = deck == 0
            ? L.Compartments.FirstOrDefault(c => c.Id == "INFIRMARY")
            : L.Compartments.FirstOrDefault(c => c.Id == "STORE_P1");
        if (svc is not null)
        {
            var ySvc = oy + stackY1 * scale + 5;
            var label = deck == 0 ? "INF/GAL F–A" : "STORE F–A";
            DimH(sb, ShipX(ox, scale, L.Loa, svc.Z1), ySvc, ShipX(ox, scale, L.Loa, svc.Z0), ySvc,
                $"{label} {Nice(svc.Z1 - svc.Z0)}");
        }

        // Stack overall
        DimV(sb, ShipX(ox, scale, L.Loa, cLast.Z1) - 8, oy - stackY0 * scale, ShipX(ox, scale, L.Loa, cLast.Z1) - 8,
            oy - stackY1 * scale, $"STACK {Nice(stackY1 - stackY0)}");

        _ = viewY;
        _ = viewH;
        _ = yDim;
    }

    /// <summary>Ghost Type C0n clear + with-walls envelopes over lock cabins.</summary>
    private static void AnnotateCabinC0nOverlay(StringBuilder sb, LockSnapshot L, int deck, double ox, double oy,
        double scale)
    {
        var prefix = deck == 0 ? "CREW_" : "PAX_";
        var cabins = L.Compartments
            .Where(c => c.Id is not null && c.Id.StartsWith(prefix, StringComparison.Ordinal) && MatchesDeck(c, deck))
            .OrderBy(c => c.Y0)
            .ToList();

        for (var i = 0; i < InternalsLockCabinPatch.CabinCount; i++)
        {
            var outer0 = -InternalsLockCabinPatch.Stack + i * InternalsLockCabinPatch.ModuleW;
            var outer1 = outer0 + InternalsLockCabinPatch.ModuleW;
            var clearY0 = outer0 + InternalsLockCabinPatch.WallT / 2;
            var clearY1 = outer1 - InternalsLockCabinPatch.WallT / 2;
            var z0 = InternalsLockCabinPatch.CabinClearZ0;
            var z1 = InternalsLockCabinPatch.CabinClearZ1;

            // Module O/A (with walls)
            DrawOverlayRect(sb, L, ox, oy, scale, outer0, outer1, z0 - InternalsLockCabinPatch.WallT / 2,
                z1 + InternalsLockCabinPatch.WallT / 2, "expw");
            // Clear cell
            DrawOverlayRect(sb, L, ox, oy, scale, clearY0, clearY1, z0, z1, "exp");
        }

        if (cabins.Count > 0)
        {
            var mid = cabins[cabins.Count / 2];
            var cx = ShipX(ox, scale, L.Loa, (mid.Z0 + mid.Z1) * 0.5);
            var cy = oy - (mid.Y0 + mid.Y1) * 0.5 * scale;
            sb.Append(
                $"<text x=\"{F(cx)}\" y=\"{F(cy - 3)}\" class=\"expt\" text-anchor=\"middle\">C0n CLR {Nice(InternalsLockCabinPatch.ClearW)}×{Nice(InternalsLockCabinPatch.ClearD)}</text>");
            sb.Append(
                $"<text x=\"{F(cx)}\" y=\"{F(cy + 5)}\" class=\"expwt\" text-anchor=\"middle\">MOD O/A {Nice(InternalsLockCabinPatch.ModuleW)} W/WALLS</text>");
        }

        // Expected service fore edge (leftover → INF/GAL/STORE)
        var zMed0 = InternalsLockCabinPatch.MedClearZ0;
        var zMed1 = InternalsLockCabinPatch.MedClearZ1;
        DrawOverlayRect(sb, L, ox, oy, scale, -InternalsLockCabinPatch.Stack + InternalsLockCabinPatch.WallT / 2,
            InternalsLockCabinPatch.Stack - InternalsLockCabinPatch.WallT / 2, zMed0, zMed1, "exps");
        var xs = ShipX(ox, scale, L.Loa, (zMed0 + zMed1) * 0.5);
        var svcLabel = deck == 0 ? "INF+GAL EXT" : "STORE EXT";
        sb.Append(
            $"<text x=\"{F(xs)}\" y=\"{F(oy + 2)}\" class=\"expt\" text-anchor=\"middle\">{Esc(svcLabel)}</text>");
    }

    private static void DrawOverlayRect(StringBuilder sb, LockSnapshot L, double ox, double oy, double scale,
        double y0, double y1, double z0, double z1, string cls)
    {
        var x0 = ShipX(ox, scale, L.Loa, z1);
        var x1 = ShipX(ox, scale, L.Loa, z0);
        var py0 = oy - y1 * scale;
        var py1 = oy - y0 * scale;
        sb.Append(
            $"<rect x=\"{F(Math.Min(x0, x1))}\" y=\"{F(Math.Min(py0, py1))}\" width=\"{F(Math.Abs(x1 - x0))}\" height=\"{F(Math.Abs(py1 - py0))}\" class=\"{cls}\"/>");
    }

    private static void DrawSpaceFootprint(StringBuilder sb, LockSnapshot L, Comp c, double ox, double oy, double scale)
    {
        var x0 = ShipX(ox, scale, L.Loa, c.Z1);
        var x1 = ShipX(ox, scale, L.Loa, c.Z0);
        var y0 = oy - c.Y1 * scale;
        var y1 = oy - c.Y0 * scale;
        var cls = c.Id is not null && c.Id.StartsWith("CORR", StringComparison.Ordinal) ? "corr"
            : c.Id is not null && c.Id.StartsWith("AIRLOCK", StringComparison.Ordinal) ? "air"
            : "space";
        sb.Append(
            $"<rect x=\"{F(Math.Min(x0, x1))}\" y=\"{F(Math.Min(y0, y1))}\" width=\"{F(Math.Abs(x1 - x0))}\" height=\"{F(Math.Abs(y1 - y0))}\" class=\"{cls}\"/>");

        var cx = ShipX(ox, scale, L.Loa, (c.Z0 + c.Z1) * 0.5);
        var cy = oy - (c.Y0 + c.Y1) * 0.5 * scale;
        if (c.Id is not null && ShouldLabel(c.Id))
            sb.Append($"<text x=\"{F(cx)}\" y=\"{F(cy + 1)}\" class=\"room\" text-anchor=\"middle\">{Esc(ShortId(c.Id))}</text>");
    }

    private static bool ShouldLabel(string id) =>
        id.Length <= 12
        || id is "BRIDGE" or "INFIRMARY" or "GALLEY" or "CROSSING" or "LOUNGE" or "UTILITY_M1"
            or "STORE_P1" or "HOLD" or "ENG" or "CREW" or "FUEL" or "ACCESS"
        || id.StartsWith("CABIN", StringComparison.Ordinal)
        || id.StartsWith("CREW_", StringComparison.Ordinal)
        || id.StartsWith("PAX_", StringComparison.Ordinal)
        || id.StartsWith("VEST_", StringComparison.Ordinal)
        || id.StartsWith("CORR", StringComparison.Ordinal)
        || id.StartsWith("AIRLOCK", StringComparison.Ordinal)
        || id.StartsWith("STAIRS", StringComparison.Ordinal)
        || id.StartsWith("ELEV", StringComparison.Ordinal);

    private static void DrawHatchOnBulkhead(StringBuilder sb, LockSnapshot lockDoc, Hatch hch, double ox, double oy, double scale)
    {
        var hx = ShipX(ox, scale, lockDoc.Loa, hch.Z);
        var hy = oy - hch.Y * scale;
        var half = Math.Max(0.8, hch.ClearW * scale * 0.5);
        var t = Math.Max(0.55, 0.18 * scale);
        var n = hch.Normal ?? "";

        double rx, ry, ww, hh;
        if (n.Contains('Y', StringComparison.OrdinalIgnoreCase))
        {
            rx = hx - half;
            ry = hy - t;
            ww = half * 2;
            hh = t * 2;
        }
        else
        {
            rx = hx - t;
            ry = hy - half;
            ww = t * 2;
            hh = half * 2;
        }

        sb.Append($"<rect x=\"{F(rx)}\" y=\"{F(ry)}\" width=\"{F(ww)}\" height=\"{F(hh)}\" class=\"hatch\"/>");
        if (hch.Id is not null && hch.Id.Length <= 10)
            sb.Append($"<text x=\"{F(hx + 1.2)}\" y=\"{F(hy - 1.2)}\" class=\"anno\">{Esc(hch.Id)}</text>");
    }

    private static void DrawHoldPlan(StringBuilder sb, LockSnapshot L, double x, double y, double w, double h,
        out double scale)
    {
        var hold = L.Hold;
        var packL = hold.HoldL;
        var packW = L.Beam - 1.0;
        scale = Math.Min(w / (packL + hold.ApronD), h / packW) * 0.85;
        var ox = x + 20;
        var oy = y + h * 0.5;

        // Hold rectangle (fore = right of pack toward bow in full ship; here local: hatch left, C40 mid, apron left of hatch? Stern left: door at left)
        var doorX = ox;
        var holdLen = packL * scale;
        sb.Append(
            $"<rect x=\"{F(doorX)}\" y=\"{F(oy - packW * 0.5 * scale)}\" width=\"{F(holdLen)}\" height=\"{F(packW * scale)}\" class=\"ol\" fill=\"none\"/>");

        // Door
        var dw = hold.DoorW * scale;
        var dh = 4.0;
        sb.Append(
            $"<rect x=\"{F(doorX - 1)}\" y=\"{F(oy - dw * 0.5)}\" width=\"{F(dh)}\" height=\"{F(dw)}\" class=\"hatch\"/>");
        sb.Append($"<text x=\"{F(doorX + 6)}\" y=\"{F(oy)}\" class=\"room\">DOOR {Nice(hold.DoorW)}×{Nice(hold.DoorH)}</text>");

        // C40 cells
        var c40X = doorX + (hold.RampGap + hold.C40L) * scale; // from aft: ramp then C40 — wait aft is left
        // Layout aft→fore: door | ramp gap | C40 | catwalk gap | catwalk
        var xC40 = doorX + hold.RampGap * scale;
        for (var col = 0; col < 5; col++)
        {
            var cx = xC40 + (col + 0.5) * (hold.C40W + hold.Cell) * scale;
            sb.Append(
                $"<rect x=\"{F(cx - hold.C40W * 0.5 * scale)}\" y=\"{F(oy - hold.C40W * 0.15 * scale)}\" width=\"{F(hold.C40W * scale)}\" height=\"{F(hold.C40W * 0.3 * scale)}\" class=\"space\"/>");
        }

        sb.Append($"<text x=\"{F(xC40 + hold.C40L * 0.5 * scale)}\" y=\"{F(oy - 12)}\" class=\"room\" text-anchor=\"middle\">C40 5×1×3</text>");
        DimH(sb, doorX, oy + packW * 0.5 * scale + 8, doorX + holdLen, oy + packW * 0.5 * scale + 8, $"HOLD PACK {Nice(packL)}");
        _ = c40X;
    }

    private static void HatchTable(StringBuilder sb, LockSnapshot L, double x, double y, double w, double maxH)
    {
        var cols = new[] { 0.10, 0.06, 0.22, 0.14, 0.16, 0.16, 0.16 }; // fractions of w
        var headers = new[] { "ID", "DK", "FROM → TO", "CLEAR", "FACE", "Y", "Z" };
        var rowH = 5.2;
        var n = Math.Min(L.Hatches.Count, (int)((maxH - 12) / rowH) - 1);

        sb.Append($"<text x=\"{F(x)}\" y=\"{F(y)}\" class=\"view\">HATCH OPENING SCHEDULE</text>");
        var hy = y + 6;
        double cx = x;
        for (var i = 0; i < headers.Length; i++)
        {
            sb.Append($"<rect x=\"{F(cx)}\" y=\"{F(hy)}\" width=\"{F(w * cols[i])}\" height=\"{F(rowH)}\" class=\"tbl-h\"/>");
            sb.Append(
                $"<text x=\"{F(cx + 1.5)}\" y=\"{F(hy + rowH - 1.4)}\" class=\"tbl\">{headers[i]}</text>");
            cx += w * cols[i];
        }

        for (var r = 0; r < n; r++)
        {
            var h = L.Hatches[r];
            var ry = hy + (r + 1) * rowH;
            cx = x;
            var cells = new[]
            {
                h.Id ?? "",
                h.Deck.ToString(CultureInfo.InvariantCulture),
                $"{h.From} → {h.To}",
                $"{Nice(h.ClearW)}×{Nice(h.ClearH)}",
                h.Faces ?? "",
                Nice(h.Y),
                Nice(h.Z),
            };
            for (var i = 0; i < cells.Length; i++)
            {
                sb.Append($"<rect x=\"{F(cx)}\" y=\"{F(ry)}\" width=\"{F(w * cols[i])}\" height=\"{F(rowH)}\" class=\"tbl-c\"/>");
                sb.Append(
                    $"<text x=\"{F(cx + 1.2)}\" y=\"{F(ry + rowH - 1.4)}\" class=\"tbl\">{Esc(Trim(cells[i], (int)(w * cols[i] / 1.7)))}</text>");
                cx += w * cols[i];
            }
        }

        if (L.Hatches.Count > n)
            sb.Append(
                $"<text x=\"{F(x)}\" y=\"{F(hy + (n + 1.6) * rowH)}\" class=\"anno\">… {L.Hatches.Count - n} ADDITIONAL OPENINGS IN LOCK JSON</text>");
    }

    // ─── Sheet chrome (ISO 5457 / 7200) ───────────────────────────────────────

    private static void BeginSvg(StringBuilder sb, string dwg, string title, LockSnapshot L)
    {
        sb.AppendLine($"<!-- {dwg} Rev {Rev} · {title} · LOA {L.Loa} -->");
        sb.AppendLine(
            $"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {F(SheetW)} {F(SheetH)}\" width=\"100%\" role=\"img\" aria-label=\"{Esc(dwg)} {Esc(title)}\">");
        sb.AppendLine("""
<style>
  .sheet { fill: #f7f4ea; }
  .frame { fill: none; stroke: #1a1a1a; stroke-width: 0.7; }
  .border { fill: none; stroke: #1a1a1a; stroke-width: 0.35; }
  .ol { stroke: #111; stroke-width: 0.5; fill: none; }
  .thin { stroke: #333; stroke-width: 0.18; fill: none; }
  .cl { stroke: #222; stroke-width: 0.25; stroke-dasharray: 8 1.5 1.2 1.5; fill: none; }
  .space { fill: #fff; stroke: #111; stroke-width: 0.28; }
  .corr { fill: #f0f3f6; stroke: #111; stroke-width: 0.32; }
  .air { fill: #e8eef5; stroke: #111; stroke-width: 0.28; }
  .bh { stroke: #666; stroke-width: 0.22; stroke-dasharray: 2.5 1.2; fill: none; }
  .hatch { fill: #fff; stroke: #0a3d8f; stroke-width: 0.4; }
  .dim { stroke: #222; stroke-width: 0.15; fill: none; }
  .dimt { font: 2.6px "IBM Plex Mono", Consolas, "Liberation Mono", monospace; fill: #111; }
  .room { font: 2.8px "IBM Plex Mono", Consolas, monospace; fill: #111; }
  .anno { font: 2.4px "IBM Plex Mono", Consolas, monospace; fill: #333; }
  .view { font: 3.6px "IBM Plex Mono", Consolas, monospace; fill: #111; font-weight: 600; letter-spacing: 0.04em; }
  .tb { fill: #f7f4ea; stroke: #111; stroke-width: 0.35; }
  .tbl { font: 2.3px "IBM Plex Mono", Consolas, monospace; fill: #111; }
  .tbl-h { fill: #e6e2d6; stroke: #111; stroke-width: 0.2; }
  .tbl-c { fill: #fff; stroke: #111; stroke-width: 0.15; }
  .zone { font: 2.2px "IBM Plex Mono", Consolas, monospace; fill: #555; }
  .note { font: 2.5px "IBM Plex Mono", Consolas, monospace; fill: #111; }
  .exp { fill: none; stroke: #0a6b3c; stroke-width: 0.45; stroke-dasharray: 2.2 1.2; }
  .expw { fill: none; stroke: #a65b12; stroke-width: 0.35; stroke-dasharray: 1.6 1.1; }
  .exps { fill: rgba(10,107,60,0.06); stroke: #0a6b3c; stroke-width: 0.3; stroke-dasharray: 3 1.5; }
  .expt { font: 2.3px "IBM Plex Mono", Consolas, monospace; fill: #0a6b3c; }
  .expwt { font: 2.2px "IBM Plex Mono", Consolas, monospace; fill: #a65b12; }
</style>
""");
        sb.AppendLine($"<rect class=\"sheet\" width=\"{F(SheetW)}\" height=\"{F(SheetH)}\"/>");
    }

    private static void EndSvg(StringBuilder sb) => sb.AppendLine("</svg>");

    private static void SheetChrome(StringBuilder sb, string number, string title, string subtitle, string scale,
        LockSnapshot L)
    {
        // Outer trim edge (visual)
        sb.AppendLine($"<rect x=\"0.5\" y=\"0.5\" width=\"{F(SheetW - 1)}\" height=\"{F(SheetH - 1)}\" class=\"border\"/>");
        // Drawing frame
        sb.AppendLine(
            $"<rect x=\"{F(FrameX)}\" y=\"{F(FrameY)}\" width=\"{F(FrameW)}\" height=\"{F(FrameH)}\" class=\"frame\"/>");

        // Centring marks (ISO 5457)
        var midX = SheetW * 0.5;
        var midY = SheetH * 0.5;
        sb.AppendLine($"<line x1=\"{F(midX)}\" y1=\"0\" x2=\"{F(midX)}\" y2=\"{F(Border - 1)}\" class=\"thin\"/>");
        sb.AppendLine(
            $"<line x1=\"{F(midX)}\" y1=\"{F(SheetH - Border + 1)}\" x2=\"{F(midX)}\" y2=\"{F(SheetH)}\" class=\"thin\"/>");
        sb.AppendLine($"<line x1=\"0\" y1=\"{F(midY)}\" x2=\"{F(BorderL - 1)}\" y2=\"{F(midY)}\" class=\"thin\"/>");
        sb.AppendLine(
            $"<line x1=\"{F(SheetW - Border + 1)}\" y1=\"{F(midY)}\" x2=\"{F(SheetW)}\" y2=\"{F(midY)}\" class=\"thin\"/>");

        // Zone grid letters/numbers in border
        var zonesX = 8;
        var zonesY = 6;
        for (var i = 0; i < zonesX; i++)
        {
            var zx = FrameX + (i + 0.5) * (FrameW / zonesX);
            var letter = (char)('A' + i);
            sb.AppendLine($"<text x=\"{F(zx)}\" y=\"{F(Border - 2.5)}\" class=\"zone\" text-anchor=\"middle\">{letter}</text>");
            sb.AppendLine(
                $"<text x=\"{F(zx)}\" y=\"{F(SheetH - 2.5)}\" class=\"zone\" text-anchor=\"middle\">{letter}</text>");
        }

        for (var i = 0; i < zonesY; i++)
        {
            var zy = FrameY + (i + 0.5) * (FrameH / zonesY);
            sb.AppendLine($"<text x=\"{F(BorderL - 4)}\" y=\"{F(zy + 1)}\" class=\"zone\" text-anchor=\"middle\">{i + 1}</text>");
            sb.AppendLine(
                $"<text x=\"{F(SheetW - Border + 4)}\" y=\"{F(zy + 1)}\" class=\"zone\" text-anchor=\"middle\">{i + 1}</text>");
        }

        // Title block (bottom-right)
        var tx = FrameX + FrameW - TbW;
        var ty = FrameY + FrameH - TbH;
        sb.AppendLine($"<rect x=\"{F(tx)}\" y=\"{F(ty)}\" width=\"{F(TbW)}\" height=\"{F(TbH)}\" class=\"tb\"/>");
        // Internal grid
        sb.AppendLine($"<line x1=\"{F(tx)}\" y1=\"{F(ty + 14)}\" x2=\"{F(tx + TbW)}\" y2=\"{F(ty + 14)}\" class=\"thin\"/>");
        sb.AppendLine($"<line x1=\"{F(tx)}\" y1=\"{F(ty + 28)}\" x2=\"{F(tx + TbW)}\" y2=\"{F(ty + 28)}\" class=\"thin\"/>");
        sb.AppendLine($"<line x1=\"{F(tx)}\" y1=\"{F(ty + 42)}\" x2=\"{F(tx + TbW)}\" y2=\"{F(ty + 42)}\" class=\"thin\"/>");
        sb.AppendLine($"<line x1=\"{F(tx + 110)}\" y1=\"{F(ty)}\" x2=\"{F(tx + 110)}\" y2=\"{F(ty + TbH)}\" class=\"thin\"/>");
        sb.AppendLine($"<line x1=\"{F(tx + 145)}\" y1=\"{F(ty + 14)}\" x2=\"{F(tx + 145)}\" y2=\"{F(ty + TbH)}\" class=\"thin\"/>");

        sb.AppendLine($"<text x=\"{F(tx + 3)}\" y=\"{F(ty + 5)}\" class=\"anno\">OWNER / DESIGN</text>");
        sb.AppendLine($"<text x=\"{F(tx + 3)}\" y=\"{F(ty + 11)}\" class=\"view\">NOVOLIS · CALYPSO</text>");
        sb.AppendLine($"<text x=\"{F(tx + 112)}\" y=\"{F(ty + 5)}\" class=\"anno\">DWG NO.</text>");
        sb.AppendLine($"<text x=\"{F(tx + 112)}\" y=\"{F(ty + 11)}\" class=\"view\">{Esc(number)}</text>");

        sb.AppendLine($"<text x=\"{F(tx + 3)}\" y=\"{F(ty + 19)}\" class=\"anno\">TITLE</text>");
        sb.AppendLine($"<text x=\"{F(tx + 3)}\" y=\"{F(ty + 25)}\" class=\"view\">{Esc(title)}</text>");
        sb.AppendLine($"<text x=\"{F(tx + 112)}\" y=\"{F(ty + 19)}\" class=\"anno\">REV</text>");
        sb.AppendLine($"<text x=\"{F(tx + 112)}\" y=\"{F(ty + 25)}\" class=\"view\">{Rev}</text>");
        sb.AppendLine($"<text x=\"{F(tx + 147)}\" y=\"{F(ty + 19)}\" class=\"anno\">SCALE</text>");
        sb.AppendLine($"<text x=\"{F(tx + 147)}\" y=\"{F(ty + 25)}\" class=\"view\">{Esc(scale)}</text>");

        sb.AppendLine($"<text x=\"{F(tx + 3)}\" y=\"{F(ty + 33)}\" class=\"anno\">SUBTITLE</text>");
        sb.AppendLine($"<text x=\"{F(tx + 3)}\" y=\"{F(ty + 39)}\" class=\"room\">{Esc(subtitle)}</text>");
        sb.AppendLine($"<text x=\"{F(tx + 112)}\" y=\"{F(ty + 33)}\" class=\"anno\">SHEET</text>");
        sb.AppendLine($"<text x=\"{F(tx + 112)}\" y=\"{F(ty + 39)}\" class=\"room\">A1 ISO 5457</text>");
        sb.AppendLine($"<text x=\"{F(tx + 147)}\" y=\"{F(ty + 33)}\" class=\"anno\">UNITS</text>");
        sb.AppendLine($"<text x=\"{F(tx + 147)}\" y=\"{F(ty + 39)}\" class=\"room\">m</text>");

        sb.AppendLine($"<text x=\"{F(tx + 3)}\" y=\"{F(ty + 47)}\" class=\"anno\">ENVELOPE</text>");
        sb.AppendLine(
            $"<text x=\"{F(tx + 3)}\" y=\"{F(ty + 53)}\" class=\"room\">LOA {Nice(L.Loa)} · B {Nice(L.Beam)} · OAH {Nice(L.Oah)} · {Esc(L.Material)}</text>");
        sb.AppendLine($"<text x=\"{F(tx + 112)}\" y=\"{F(ty + 47)}\" class=\"anno\">REF</text>");
        sb.AppendLine($"<text x=\"{F(tx + 112)}\" y=\"{F(ty + 53)}\" class=\"room\">CAL-HULL-GA-001</text>");

        // Standard note under frame left of title block
        sb.AppendLine(
            $"<text x=\"{F(FrameX + 4)}\" y=\"{F(FrameY + FrameH - 2)}\" class=\"anno\">{StdNote}</text>");
    }

    private static void ViewLabel(StringBuilder sb, double x, double y, string text) =>
        sb.AppendLine($"<text x=\"{F(x)}\" y=\"{F(y)}\" class=\"view\">{Esc(text)}</text>");

    private static void ScaleCallout(StringBuilder sb, double x, double y, double pxPerM, string tag) =>
        sb.AppendLine(
            $"<text x=\"{F(x)}\" y=\"{F(y)}\" class=\"anno\">{Esc(tag)} · {F(pxPerM)} mm/m ON THIS SHEET (APPROX)</text>");

    private static void NotesBlock(StringBuilder sb, double x, double y, double w, double h, string[] notes)
    {
        sb.AppendLine($"<rect x=\"{F(x)}\" y=\"{F(y)}\" width=\"{F(w)}\" height=\"{F(h)}\" class=\"tb\"/>");
        sb.AppendLine($"<text x=\"{F(x + 3)}\" y=\"{F(y + 5)}\" class=\"view\">NOTES</text>");
        for (var i = 0; i < notes.Length; i++)
            sb.AppendLine($"<text x=\"{F(x + 3)}\" y=\"{F(y + 12 + i * 5)}\" class=\"note\">{Esc(notes[i])}</text>");
    }

    private static void CenterLine(StringBuilder sb, double x1, double y1, double x2, double y2) =>
        sb.AppendLine($"<line x1=\"{F(x1)}\" y1=\"{F(y1)}\" x2=\"{F(x2)}\" y2=\"{F(y2)}\" class=\"cl\"/>");

    private static void DimH(StringBuilder sb, double x1, double y, double x2, double y2, string label)
    {
        sb.AppendLine($"<line x1=\"{F(x1)}\" y1=\"{F(y)}\" x2=\"{F(x2)}\" y2=\"{F(y2)}\" class=\"dim\"/>");
        sb.AppendLine($"<line x1=\"{F(x1)}\" y1=\"{F(y - 2)}\" x2=\"{F(x1)}\" y2=\"{F(y + 2)}\" class=\"dim\"/>");
        sb.AppendLine($"<line x1=\"{F(x2)}\" y1=\"{F(y - 2)}\" x2=\"{F(x2)}\" y2=\"{F(y + 2)}\" class=\"dim\"/>");
        sb.AppendLine(
            $"<text x=\"{F((x1 + x2) * 0.5)}\" y=\"{F(y - 1.5)}\" class=\"dimt\" text-anchor=\"middle\">{Esc(label)}</text>");
    }

    private static void DimV(StringBuilder sb, double x, double y1, double x2, double y2, string label)
    {
        sb.AppendLine($"<line x1=\"{F(x)}\" y1=\"{F(y1)}\" x2=\"{F(x2)}\" y2=\"{F(y2)}\" class=\"dim\"/>");
        sb.AppendLine(
            $"<text x=\"{F(x + 2)}\" y=\"{F((y1 + y2) * 0.5)}\" class=\"dimt\">{Esc(label)}</text>");
    }

    // ─── Geometry helpers ────────────────────────────────────────────────────

    /// <summary>ISO 128-15: stern left → larger zFromStem maps left.</summary>
    private static double ShipX(double ox, double scale, double loa, double zFromStem) =>
        ox + (loa - zFromStem) * scale;

    private static string HullPlanPath(LockSnapshot L, double ox, double oy, double scale)
    {
        // Port outline stem→aft then stbd aft→stem
        var sb = new StringBuilder();
        var zs = new List<double> { 0, 3.25, 10, 17, L.Loa };
        sb.Append('M');
        foreach (var z in zs)
        {
            var hb = HullBeamAt(L, z) * 0.5;
            sb.Append($" {F(ShipX(ox, scale, L.Loa, z))},{F(oy - hb * scale)}");
        }

        for (var i = zs.Count - 1; i >= 0; i--)
        {
            var z = zs[i];
            var hb = HullBeamAt(L, z) * 0.5;
            sb.Append($" {F(ShipX(ox, scale, L.Loa, z))},{F(oy + hb * scale)}");
        }

        sb.Append(" Z");
        return sb.ToString();
    }

    private static string HullProfilePath(LockSnapshot L, double ox, double oy, double scale)
    {
        var zs = new[] { 0.0, 3.25, 10, 17, L.Loa };
        var sb = new StringBuilder("M");
        foreach (var z in zs)
        {
            var h = HullHeightAt(L, z);
            sb.Append($" {F(ShipX(ox, scale, L.Loa, z))},{F(oy - h * scale)}");
        }

        for (var i = zs.Length - 1; i >= 0; i--)
            sb.Append($" {F(ShipX(ox, scale, L.Loa, zs[i]))},{F(oy)}");
        sb.Append(" Z");
        return sb.ToString();
    }

    private static double HullBeamAt(LockSnapshot L, double z)
    {
        if (z <= 0) return 3.5;
        if (z >= 17) return L.Beam;
        (double z, double b)[] st = [(0, 3.5), (3.25, 10), (10, 17), (17, 20)];
        for (var i = 0; i < st.Length - 1; i++)
        {
            if (z >= st[i].z && z <= st[i + 1].z)
            {
                var t = (z - st[i].z) / (st[i + 1].z - st[i].z);
                return st[i].b + t * (st[i + 1].b - st[i].b);
            }
        }

        return L.Beam;
    }

    private static double HullHeightAt(LockSnapshot L, double z)
    {
        if (z <= 0) return 4;
        if (z >= 17) return L.Oah;
        (double z, double h)[] st = [(0, 4), (3.25, 7.5), (10, 10.5), (17, 12)];
        for (var i = 0; i < st.Length - 1; i++)
        {
            if (z >= st[i].z && z <= st[i + 1].z)
            {
                var t = (z - st[i].z) / (st[i + 1].z - st[i].z);
                return st[i].h + t * (st[i + 1].h - st[i].h);
            }
        }

        return L.Oah;
    }

    private static bool MatchesDeck(Comp c, int deck)
    {
        if (c.DeckTag == "all") return true;
        // Full-height atrium / cargo void — show footprint on every deck plan.
        if (c.DeckTag is "atrium" or "cargo") return true;
        return c.DeckNum == deck;
    }

    private static string ShortId(string id) => id switch
    {
        "INFIRMARY" => "INFIRM",
        "CROSSING" => "CROSS",
        "UTILITY_M1" => "UTIL",
        "STORE_P1" => "STORE",
        "AIRLOCK_A_port" => "AL-A-P",
        "AIRLOCK_B_port" => "AL-B-P",
        "AIRLOCK_A_stbd" => "AL-A-S",
        "AIRLOCK_B_stbd" => "AL-B-S",
        "CORR_P" => "CORR P",
        "CORR_S" => "CORR S",
        _ when id.StartsWith("VEST_CREW_", StringComparison.Ordinal) => "V" + id["VEST_CREW_".Length..],
        _ when id.StartsWith("VEST_PAX_", StringComparison.Ordinal) => "V" + id["VEST_PAX_".Length..],
        _ when id.StartsWith("CREW_", StringComparison.Ordinal) => "C" + id["CREW_".Length..],
        _ when id.StartsWith("PAX_", StringComparison.Ordinal) => "P" + id["PAX_".Length..],
        _ => id.StartsWith("CABIN_", StringComparison.Ordinal) ? id.Replace("CABIN_", "C", StringComparison.Ordinal) : id,
    };

    private static string WrapHtml(string number, string title, string svg) =>
        $$"""
          <!DOCTYPE html>
          <html lang="en">
          <head>
          <meta charset="utf-8"/>
          <title>{{number}} Rev {{Rev}} — {{title}}</title>
          <style>
            @page { size: A1 landscape; margin: 0; }
            html, body { margin: 0; padding: 0; background: #2a2a2a; }
            body { display: flex; justify-content: center; padding: 12px; }
            svg { background: #f7f4ea; max-width: 100%; height: auto; box-shadow: 0 2px 16px rgba(0,0,0,.35); }
            @media print {
              html, body { background: #fff; padding: 0; }
              svg { box-shadow: none; width: 841mm; height: 594mm; }
            }
          </style>
          </head>
          <body>
          {{svg}}
          </body>
          </html>
          """;

    private static string F(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);
    private static string Nice(double v)
    {
        var q = Math.Round(v * 4) / 4;
        return Math.Abs(v - q) < 1e-6
            ? q.ToString("0.##", CultureInfo.InvariantCulture)
            : v.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private static string Esc(string s) =>
        s.Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal);

    private static string Trim(string s, int max) =>
        s.Length <= max ? s : s[..Math.Max(0, max - 1)] + "…";

    // ─── Lock snapshot ───────────────────────────────────────────────────────

    private sealed class LockSnapshot
    {
        public double Loa { get; init; }
        public double Beam { get; init; }
        public double Oah { get; init; }
        public double DkM1 { get; init; }
        public double Dk0 { get; init; }
        public double Dk1 { get; init; }
        public string Material { get; init; } = "AISI 316L";
        public List<Comp> Compartments { get; init; } = [];
        public List<Comp> Airlocks { get; init; } = [];
        public List<Hatch> Hatches { get; init; } = [];
        public HoldPack Hold { get; init; } = new();
        public List<double> BhStations { get; init; } = [];
        public double CorrZ0 { get; init; }
        public double CorrZ1 { get; init; }

        public static LockSnapshot From(JsonElement root)
        {
            var env = root.GetProperty("envelope");
            var decks = root.GetProperty("decks");
            var comps = new List<Comp>();
            foreach (var c in root.GetProperty("compartments").EnumerateArray())
                comps.Add(Comp.From(c));

            var airlocks = new List<Comp>();
            if (root.TryGetProperty("airlocks", out var als))
            {
                foreach (var a in als.EnumerateArray())
                {
                    var c = Comp.FromAirlock(a);
                    // Avoid double-draw if also listed under compartments.
                    if (comps.All(x => !string.Equals(x.Id, c.Id, StringComparison.OrdinalIgnoreCase)))
                        airlocks.Add(c);
                }
            }

            var hatches = new List<Hatch>();
            if (root.TryGetProperty("hatches", out var hs))
            {
                foreach (var h in hs.EnumerateArray())
                    hatches.Add(Hatch.Parse(h));
            }

            var bh = new List<double>();
            if (root.TryGetProperty("stations", out var st))
            {
                foreach (var key in new[]
                         {
                             "Z_BRIDGE_AFT", "Z_VERT_FORE", "Z_VERT_AFT", "Z_CROSS_FORE", "Z_CROSS_AFT",
                             "Z_CREW_FORE", "Z_CREW_AFT", "Z_MED_FORE", "Z_MED_AFT", "Z_ENG_FORE", "Z_ENG_AFT",
                         })
                {
                    if (st.TryGetProperty(key, out var z))
                        bh.Add(z.GetDouble());
                }
            }

            var corr = comps.FirstOrDefault(c => c.Id == "CORR_P");
            var holdEl = root.GetProperty("hold");
            return new LockSnapshot
            {
                Loa = env.GetProperty("LOA").GetDouble(),
                Beam = env.GetProperty("BEAM").GetDouble(),
                Oah = env.GetProperty("OAH").GetDouble(),
                DkM1 = decks.GetProperty("m1").GetDouble(),
                Dk0 = decks.GetProperty("d0").GetDouble(),
                Dk1 = decks.GetProperty("d1").GetDouble(),
                Material = env.TryGetProperty("MATERIAL", out var m) ? m.GetString() ?? "AISI 316L" : "AISI 316L",
                Compartments = comps,
                Airlocks = airlocks,
                Hatches = hatches,
                Hold = HoldPack.From(holdEl),
                BhStations = bh.Distinct().OrderBy(z => z).ToList(),
                CorrZ0 = corr?.Z0 ?? 23,
                CorrZ1 = corr?.Z1 ?? 49.25,
            };
        }
    }

    private sealed class Comp
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

        public static Comp From(JsonElement c)
        {
            int? deckNum = null;
            string? deckTag = null;
            var deck = c.GetProperty("deck");
            if (deck.ValueKind == JsonValueKind.Number)
                deckNum = deck.GetInt32();
            else
                deckTag = deck.GetString();

            List<(double, double)>? ring = null;
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

            return new Comp
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
                PlanRing = ring,
            };
        }

        public static Comp FromAirlock(JsonElement a) => new()
        {
            Id = a.GetProperty("id").GetString(),
            DeckNum = 0,
            Z0 = a.GetProperty("z0").GetDouble(),
            Z1 = a.GetProperty("z1").GetDouble(),
            Y0 = a.GetProperty("y0").GetDouble(),
            Y1 = a.GetProperty("y1").GetDouble(),
            Up0 = a.GetProperty("up0").GetDouble(),
            Up1 = a.GetProperty("up1").GetDouble(),
        };
    }

    private sealed class Hatch
    {
        public string? Id { get; init; }
        public int Deck { get; init; }
        public double ClearW { get; init; }
        public double ClearH { get; init; }
        public double Y { get; init; }
        public double Z { get; init; }
        public string? From { get; init; }
        public string? To { get; init; }
        public string? Faces { get; init; }
        public string? Normal { get; init; }

        public static Hatch Parse(JsonElement h) => new()
        {
            Id = h.GetProperty("id").GetString(),
            Deck = h.GetProperty("deck").GetInt32(),
            ClearW = h.GetProperty("clearW").GetDouble(),
            ClearH = h.GetProperty("clearH").GetDouble(),
            Y = h.GetProperty("y").GetDouble(),
            Z = h.GetProperty("z").GetDouble(),
            From = h.TryGetProperty("from", out var f) ? f.GetString() : null,
            To = h.TryGetProperty("to", out var t) ? t.GetString() : null,
            Faces = h.TryGetProperty("faces", out var fa) ? fa.GetString() : null,
            Normal = h.TryGetProperty("normal", out var n) ? n.GetString() : null,
        };
    }

    private sealed class HoldPack
    {
        public double HoldL { get; init; }
        public double DoorW { get; init; }
        public double DoorH { get; init; }
        public double ApronD { get; init; }
        public double C40L { get; init; }
        public double C40W { get; init; }
        public double Cell { get; init; }
        public double RampGap { get; init; }

        public static HoldPack From(JsonElement h) => new()
        {
            HoldL = h.GetProperty("HOLD_L").GetDouble(),
            DoorW = h.GetProperty("DOOR_W").GetDouble(),
            DoorH = h.GetProperty("DOOR_H").GetDouble(),
            ApronD = h.GetProperty("APRON_D").GetDouble(),
            C40L = h.GetProperty("C40_L").GetDouble(),
            C40W = h.GetProperty("C40_W").GetDouble(),
            Cell = h.GetProperty("CELL").GetDouble(),
            RampGap = h.GetProperty("RAMP_GAP").GetDouble(),
        };
    }
}
