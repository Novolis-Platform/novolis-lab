using System.Globalization;
using System.Text;
using CalypsoCad.Blueprints;
using CalypsoCad.Blueprints.DrawingPackage;

namespace CalypsoCad.Drawing;

/// <summary>Composes projected geometry into the existing package SVG chrome.</summary>
internal static class DrawingSvgComposer
{
    public static string ComposeHtml(
        DrawingRequest request,
        SubjectGeometryResolver.ResolveResult resolved,
        IReadOnlyList<DrawingMeasurement> measurements,
        IReadOnlyList<PartRegisterLine> register,
        PackageDrawingChrome.ShipBasics ship,
        DrawingManifest manifest)
    {
        var entry = new DrawingEntry(
            Category: DrawingCategoryId.General,
            Sequence: 0,
            DrawingNumber: request.DrawingNumber ?? $"CAL-DWG-{Sanitize(request.SubjectId)}",
            Title: request.Title ?? $"Drawing · {request.SubjectId}",
            Label: request.Label ?? request.SubjectId,
            RelativePath: "",
            DetailSourceFile: null,
            SubjectId: request.SubjectId,
            VolumeKind: VolumeCirculationKind.None,
            Tags: []);

        var block = new List<(string, string)>
        {
            ("Subject", request.SubjectId),
            ("Geometry", $"{resolved.Geometry.Count} drawable(s)"),
            ("Engine", "UniversalDrawingEngine"),
        };
        foreach (var m in measurements.Take(4))
            block.Add((m.Label, $"{m.ValueMeters.ToString("0.###", CultureInfo.InvariantCulture)} m{(m.IsTrue3D ? " (3D)" : "")}"));

        const string subtitle = "Hierarchy-driven drawing · actual CAD geometry · ISO 128-15";
        var sb = PackageDrawingChrome.BeginHtml(
            entry,
            request.SubjectId,
            block,
            subtitle);

        // Clip paths for every view slot — geometry must not spill into sibling panels.
        sb.AppendLine("<defs>");
        var slots = LayoutSlots(request.Views.Count);
        for (var i = 0; i < slots.Count; i++)
        {
            var (x, y, w, h) = slots[i];
            sb.AppendLine(
                $"<clipPath id=\"view-clip-{i}\"><rect x=\"{F(x)}\" y=\"{F(y)}\" width=\"{F(w)}\" height=\"{F(h)}\"/></clipPath>");
        }

        sb.AppendLine("</defs>");

        for (var i = 0; i < request.Views.Count; i++)
        {
            var view = request.Views[i];
            var (x, y, w, h) = slots[i];
            PackageDrawingChrome.Label(sb, x, y - 10, $"{view.Id} — {view.Title ?? view.Orientation.ToString()}");
            sb.AppendLine($"<g clip-path=\"url(#view-clip-{i})\">");
            ComposeView(sb, x, y, w, h, view, resolved, ship, request.References);
            sb.AppendLine("</g>");
        }

        if (register.Count > 0)
        {
            PackageDrawingChrome.Label(sb, 28, 390, "PART REGISTER");
            DrawRegister(sb, 28, 402, register.Take(8).ToList());
        }

        if (manifest.Diagnostics.Count > 0)
        {
            PackageDrawingChrome.NoteLines(sb, 280, 390,
                manifest.Diagnostics.Take(4).ToArray());
        }
        else
        {
            PackageDrawingChrome.NoteLines(sb, 280, 390,
                "Geometry from ShipPartRegistry ownership.",
                "Viewport visibility does not affect this drawing.",
                "Scale AS SHOWN · ISO 128-15 stern left.",
                $"Ok={manifest.Ok}");
        }

        return PackageDrawingChrome.EndHtml(sb, entry, request.SubjectId, block, subtitle);
    }

    private static void ComposeView(
        StringBuilder sb,
        double x,
        double y,
        double w,
        double h,
        DrawingViewDefinition view,
        SubjectGeometryResolver.ResolveResult resolved,
        PackageDrawingChrome.ShipBasics ship,
        DrawingReferenceSet references)
    {
        sb.AppendLine(
            $"<rect x=\"{F(x)}\" y=\"{F(y)}\" width=\"{F(w)}\" height=\"{F(h)}\" fill=\"#fff\" stroke=\"#333\"/>");

        if (!resolved.HasDrawableGeometry && view.Kind != DrawingViewKind.Assembly)
        {
            sb.AppendLine(
                $"<text x=\"{F(x + w * 0.5)}\" y=\"{F(y + h * 0.5)}\" text-anchor=\"middle\" fill=\"#c00\">no drawable geometry</text>");
            DrawOrientationMarker(sb, x, y, view);
            return;
        }

        if (view.Kind == DrawingViewKind.Section && view.SectionPlane is { } plane)
        {
            ComposeSection(sb, x, y, w, h, view, resolved, plane);
            DrawOrientationMarker(sb, x, y, view);
            return;
        }

        // Small plan/profile panels: hull reference + subject extent (not full mesh dump).
        var contextPanel = w < 320
                           && references.Flags.HasFlag(DrawingReferenceFlags.OmlOutline)
                           && view.Orientation is StandardViewOrientation.PlanSternLeft
                               or StandardViewOrientation.ProfileSternLeft;
        if (contextPanel && view.Orientation is StandardViewOrientation.PlanSternLeft)
        {
            PackageDrawingChrome.DrawPlanHull(sb, x, y, w, h, ship, (ox, oy, scale) =>
            {
                DrawSubjectExtentOnPlan(sb, resolved, ship, ox, oy, scale, x, y, w, h);
            });
            DrawOrientationMarker(sb, x, y, view);
            return;
        }

        if (contextPanel && view.Orientation is StandardViewOrientation.ProfileSternLeft)
        {
            PackageDrawingChrome.DrawProfileHull(sb, x, y, w, h, ship, showDecks: true, (ox, keelY, scale) =>
            {
                DrawSubjectExtentOnProfile(sb, resolved, ship, ox, keelY, scale, x, y, w, h);
            });
            DrawOrientationMarker(sb, x, y, view);
            return;
        }

        var fill = view.Orientation is StandardViewOrientation.TransverseLookingAft
            or StandardViewOrientation.TransverseLookingForward;
        var proj = ViewProjector.ProjectOrthographic(
            resolved.Geometry,
            view.Orientation,
            ViewProjector.EdgeMode.SilhouetteAndPolylines,
            includeFilledTriangles: fill);
        FitAndDraw(sb, x, y, w, h, view, proj);
        DrawOrientationMarker(sb, x, y, view);
    }

    /// <summary>
    /// Location context: highlight subject world AABB on the plan — never dump triangulation.
    /// Adds LOA / beam overall dims and subject offsets from stern/bow and port/stbd hull edges.
    /// </summary>
    private static void DrawSubjectExtentOnPlan(
        StringBuilder sb,
        SubjectGeometryResolver.ResolveResult resolved,
        PackageDrawingChrome.ShipBasics ship,
        double ox,
        double oy,
        double scale,
        double panelX,
        double panelY,
        double panelW,
        double panelH)
    {
        var bounds = resolved.Geometry.Aggregate(DrawingBounds3.Empty, (a, g) => a.Union(g.Bounds));
        if (bounds.IsEmpty)
            return;

        var loa = (float)ship.Loa;
        // World Z is midships-relative — convert before ShipX(zFromStem).
        var zStem0 = CadShipCoords.ClampZStem(CadShipCoords.ZStemFromWorld(bounds.Min.Z, loa), loa);
        var zStem1 = CadShipCoords.ClampZStem(CadShipCoords.ZStemFromWorld(bounds.Max.Z, loa), loa);
        var yPort = Math.Min(bounds.Min.X, bounds.Max.X);
        var yStbd = Math.Max(bounds.Min.X, bounds.Max.X);
        var zAft = Math.Max(zStem0, zStem1);
        var zFore = Math.Min(zStem0, zStem1);
        var xAft = LockDraftGeometry.ShipX(ox, scale, ship.Loa, zAft);
        var xFore = LockDraftGeometry.ShipX(ox, scale, ship.Loa, zFore);
        var sternX = LockDraftGeometry.ShipX(ox, scale, ship.Loa, loa);
        var bowX = LockDraftGeometry.ShipX(ox, scale, ship.Loa, 0);
        var syStbd = oy - yStbd * scale;
        var syPort = oy - yPort * scale;
        var left = Math.Min(xAft, xFore);
        var right = Math.Max(xAft, xFore);
        var top = Math.Min(syStbd, syPort);
        var bot = Math.Max(syStbd, syPort);
        sb.AppendLine(
            $"<rect x=\"{F(left)}\" y=\"{F(top)}\" width=\"{F(Math.Max(1, right - left))}\" height=\"{F(Math.Max(1, bot - top))}\" fill=\"#c82828\" fill-opacity=\"0.18\" stroke=\"#c82828\" stroke-width=\"1.4\"/>");

        var zMid = (zAft + zFore) * 0.5;
        var halfBeam = LockDraftGeometry.LoftHalfBeam(ship.Loft, zMid);
        var hullStbdY = oy - halfBeam * scale;
        var hullPortY = oy + halfBeam * scale;

        // Overall ship: LOA under hull, beam at bow side.
        var loaY = Math.Min(panelY + panelH - 14, Math.Max(hullPortY + 12, oy + halfBeam * scale + 10));
        PackageDrawingChrome.DimH(sb, sternX, loaY, bowX, PackageDrawingChrome.Nice(ship.Loa), labelAbove: true);
        var beamX = Math.Min(panelX + panelW - 8, bowX + 10);
        PackageDrawingChrome.DimV(sb, beamX, hullStbdY, hullPortY, PackageDrawingChrome.Nice(ship.Beam), labelRight: false);

        // Longitudinal offsets: stern→aft edge, fore edge→bow (or single station from both ends).
        var offY = Math.Max(panelY + 28, oy - halfBeam * scale - 10);
        var fromStern = loa - zAft;
        var fromBow = zFore;
        if (Math.Abs(xFore - xAft) < 2.5)
        {
            // Thin station — dim both ends to the same line.
            if (fromStern > 0.05)
                PackageDrawingChrome.DimH(sb, sternX, offY, xAft, PackageDrawingChrome.Nice(fromStern));
            if (fromBow > 0.05)
                PackageDrawingChrome.DimH(sb, xFore, offY + 11, bowX, PackageDrawingChrome.Nice(fromBow));
        }
        else
        {
            if (fromStern > 0.05)
                PackageDrawingChrome.DimH(sb, sternX, offY, xAft, PackageDrawingChrome.Nice(fromStern));
            if (fromBow > 0.05)
                PackageDrawingChrome.DimH(sb, xFore, offY, bowX, PackageDrawingChrome.Nice(fromBow));
            var span = Math.Abs(zAft - zFore);
            if (span > 0.05)
                PackageDrawingChrome.DimH(sb, xAft, offY + 11, xFore, PackageDrawingChrome.Nice(span));
        }

        // Athwartship offsets from hull edges to part edges (skip when flush / full-beam).
        var fromStbd = halfBeam - yStbd;
        var fromPort = halfBeam + yPort; // port X is negative
        var athX = Math.Max(panelX + 10, left - 10);
        if (fromStbd > 0.15 && fromStbd < halfBeam - 0.05)
            PackageDrawingChrome.DimV(sb, athX, hullStbdY, syStbd, PackageDrawingChrome.Nice(fromStbd));
        if (fromPort > 0.15 && fromPort < halfBeam - 0.05)
            PackageDrawingChrome.DimV(sb, athX, syPort, hullPortY, PackageDrawingChrome.Nice(fromPort));
    }

    private static void DrawSubjectExtentOnProfile(
        StringBuilder sb,
        SubjectGeometryResolver.ResolveResult resolved,
        PackageDrawingChrome.ShipBasics ship,
        double ox,
        double keelY,
        double scale,
        double panelX,
        double panelY,
        double panelW,
        double panelH)
    {
        var bounds = resolved.Geometry.Aggregate(DrawingBounds3.Empty, (a, g) => a.Union(g.Bounds));
        if (bounds.IsEmpty)
            return;

        var loa = (float)ship.Loa;
        var zStem0 = CadShipCoords.ClampZStem(CadShipCoords.ZStemFromWorld(bounds.Min.Z, loa), loa);
        var zStem1 = CadShipCoords.ClampZStem(CadShipCoords.ZStemFromWorld(bounds.Max.Z, loa), loa);
        var u0 = Math.Clamp(bounds.Min.Y, 0, (float)ship.Oah);
        var u1 = Math.Clamp(bounds.Max.Y, 0, (float)ship.Oah);
        var zAft = Math.Max(zStem0, zStem1);
        var zFore = Math.Min(zStem0, zStem1);
        var xAft = LockDraftGeometry.ShipX(ox, scale, ship.Loa, zAft);
        var xFore = LockDraftGeometry.ShipX(ox, scale, ship.Loa, zFore);
        var sternX = LockDraftGeometry.ShipX(ox, scale, ship.Loa, loa);
        var bowX = LockDraftGeometry.ShipX(ox, scale, ship.Loa, 0);
        var yTop = keelY - u1 * scale;
        var yBot = keelY - u0 * scale;
        var left = Math.Min(xAft, xFore);
        var right = Math.Max(xAft, xFore);
        sb.AppendLine(
            $"<rect x=\"{F(left)}\" y=\"{F(Math.Min(yTop, yBot))}\" width=\"{F(Math.Max(1, right - left))}\" height=\"{F(Math.Max(1, Math.Abs(yBot - yTop)))}\" fill=\"#c82828\" fill-opacity=\"0.18\" stroke=\"#c82828\" stroke-width=\"1.4\"/>");

        LockDraftGeometry.SampleLoftUpRange(ship.Loft, (zAft + zFore) * 0.5, ship.Oah, out var hullU0, out var hullU1);
        var hullTopY = keelY - hullU1 * scale;
        var hullBotY = keelY - hullU0 * scale;
        var oahTopY = keelY - ship.Oah * scale;

        // Overall ship: LOA under hull, OAH at bow side.
        var loaY = Math.Min(panelY + panelH - 14, Math.Max(hullBotY + 12, keelY + 8));
        PackageDrawingChrome.DimH(sb, sternX, loaY, bowX, PackageDrawingChrome.Nice(ship.Loa), labelAbove: true);
        var oahX = Math.Min(panelX + panelW - 8, bowX + 10);
        PackageDrawingChrome.DimV(sb, oahX, oahTopY, keelY, PackageDrawingChrome.Nice(ship.Oah), labelRight: false);

        // Longitudinal offsets from stern / bow.
        var offY = Math.Max(panelY + 28, hullTopY - 8);
        var fromStern = loa - zAft;
        var fromBow = zFore;
        if (Math.Abs(xFore - xAft) < 2.5)
        {
            if (fromStern > 0.05)
                PackageDrawingChrome.DimH(sb, sternX, offY, xAft, PackageDrawingChrome.Nice(fromStern));
            if (fromBow > 0.05)
                PackageDrawingChrome.DimH(sb, xFore, offY + 11, bowX, PackageDrawingChrome.Nice(fromBow));
        }
        else
        {
            if (fromStern > 0.05)
                PackageDrawingChrome.DimH(sb, sternX, offY, xAft, PackageDrawingChrome.Nice(fromStern));
            if (fromBow > 0.05)
                PackageDrawingChrome.DimH(sb, xFore, offY, bowX, PackageDrawingChrome.Nice(fromBow));
            var span = Math.Abs(zAft - zFore);
            if (span > 0.05)
                PackageDrawingChrome.DimH(sb, xAft, offY + 11, xFore, PackageDrawingChrome.Nice(span));
        }

        // Vertical offsets: keel→part bottom, part top→hull top (skip when flush).
        var athX = Math.Max(panelX + 10, left - 10);
        var fromKeel = u0 - hullU0;
        var fromTop = hullU1 - u1;
        if (fromKeel > 0.15)
            PackageDrawingChrome.DimV(sb, athX, yBot, hullBotY, PackageDrawingChrome.Nice(fromKeel));
        if (fromTop > 0.15)
            PackageDrawingChrome.DimV(sb, athX, hullTopY, yTop, PackageDrawingChrome.Nice(fromTop));
        var height = u1 - u0;
        if (height > 0.15 && Math.Abs(yTop - yBot) > 4)
            PackageDrawingChrome.DimV(sb, Math.Min(panelX + panelW - 18, right + 8), yTop, yBot, PackageDrawingChrome.Nice(height));
    }

    private static void ComposeSection(
        StringBuilder sb,
        double x,
        double y,
        double w,
        double h,
        DrawingViewDefinition view,
        SubjectGeometryResolver.ResolveResult resolved,
        DrawingPlane plane)
    {
        var edges = MeshSectioner.Section(resolved.Geometry, plane);
        if (edges.Count == 0)
        {
            sb.AppendLine(
                $"<text x=\"{F(x + w * 0.5)}\" y=\"{F(y + h * 0.5)}\" text-anchor=\"middle\" fill=\"#c00\">no section intersections</text>");
            return;
        }

        var segs = edges.Select(e =>
        {
            var a = ViewProjector.Map(e.A, view.Orientation);
            var b = ViewProjector.Map(e.B, view.Orientation);
            return new ViewProjector.ProjectedSegment(a, b, e.SourceEntityId, e.SourceName, true);
        }).ToList();

        var minU = segs.Min(s => Math.Min(s.A.U, s.B.U));
        var maxU = segs.Max(s => Math.Max(s.A.U, s.B.U));
        var minV = segs.Min(s => Math.Min(s.A.V, s.B.V));
        var maxV = segs.Max(s => Math.Max(s.A.V, s.B.V));
        FitAndDraw(sb, x, y, w, h, view,
            new ViewProjector.ProjectionResult(segs, [], DrawingBounds3.Empty, minU, maxU, minV, maxV),
            hatch: true);
    }

    private static void FitAndDraw(
        StringBuilder sb,
        double x,
        double y,
        double w,
        double h,
        DrawingViewDefinition view,
        ViewProjector.ProjectionResult proj,
        bool hatch = false)
    {
        const double pad = 28;
        var spanU = Math.Max(0.2f, proj.MaxU - proj.MinU);
        var spanV = Math.Max(0.2f, proj.MaxV - proj.MinV);
        var scale = Math.Min((w - 2 * pad) / spanU, (h - 2 * pad) / spanV) * 0.9;
        var cx = x + w * 0.5;
        var cy = y + h * 0.5;
        var midU = (proj.MinU + proj.MaxU) * 0.5;
        var midV = (proj.MinV + proj.MaxV) * 0.5;

        double SX(float u) => cx + (u - midU) * scale;
        double SY(float v) => cy - (v - midV) * scale;

        foreach (var t in proj.FilledTriangles)
        {
            if (view.Orientation is not (StandardViewOrientation.TransverseLookingAft
                or StandardViewOrientation.TransverseLookingForward))
                continue;
            sb.AppendLine(
                $"<polygon points=\"{F(SX(t.A.U))},{F(SY(t.A.V))} {F(SX(t.B.U))},{F(SY(t.B.V))} {F(SX(t.C.U))},{F(SY(t.C.V))}\" fill=\"#9aa3ad\" fill-opacity=\"0.55\" stroke=\"none\"/>");
        }

        foreach (var s in proj.Segments)
        {
            var stroke = s.IsSectionCut ? "#222" : "#444";
            var width = s.IsSectionCut ? "1.8" : "0.7";
            sb.AppendLine(
                $"<line x1=\"{F(SX(s.A.U))}\" y1=\"{F(SY(s.A.V))}\" x2=\"{F(SX(s.B.U))}\" y2=\"{F(SY(s.B.V))}\" stroke=\"{stroke}\" stroke-width=\"{width}\"/>");
            if (hatch && s.IsSectionCut)
            {
                var mx = (SX(s.A.U) + SX(s.B.U)) * 0.5;
                var my = (SY(s.A.V) + SY(s.B.V)) * 0.5;
                sb.AppendLine(
                    $"<line x1=\"{F(mx - 3)}\" y1=\"{F(my - 3)}\" x2=\"{F(mx + 3)}\" y2=\"{F(my + 3)}\" stroke=\"#666\" stroke-width=\"0.5\"/>");
            }
        }

        // Overall extents on the principal view (drafting convention).
        var x0 = SX(proj.MinU);
        var x1 = SX(proj.MaxU);
        var y0 = SY(proj.MaxV);
        var y1 = SY(proj.MinV);
        PackageDrawingChrome.DimH(sb, x0, Math.Min(y + h - 14, y1 + 14), x1, PackageDrawingChrome.Nice(spanU));
        PackageDrawingChrome.DimV(sb, Math.Min(x + w - 10, x1 + 12), y0, y1, PackageDrawingChrome.Nice(spanV), labelRight: false);

        sb.AppendLine(
            $"<text x=\"{F(x + 6)}\" y=\"{F(y + h - 8)}\" font-size=\"9\" fill=\"#555\">{Esc(view.Orientation.ToString())} · {spanU.ToString("0.##", CultureInfo.InvariantCulture)}×{spanV.ToString("0.##", CultureInfo.InvariantCulture)} m</text>");
    }

    private static void DrawOrientationMarker(StringBuilder sb, double x, double y, DrawingViewDefinition view)
    {
        var label = view.Orientation switch
        {
            StandardViewOrientation.PlanSternLeft => "PLAN · stern← · bow→",
            StandardViewOrientation.ProfileSternLeft => "PROFILE CL · stern← · bow→",
            StandardViewOrientation.TransverseLookingAft => "TRANSVERSE · looking aft",
            StandardViewOrientation.TransverseLookingForward => "TRANSVERSE · looking forward",
            _ => view.Orientation.ToString(),
        };
        sb.AppendLine(
            $"<text x=\"{F(x + 6)}\" y=\"{F(y + 14)}\" font-size=\"9\" fill=\"#0645ad\">{Esc(label)}</text>");
    }

    private static void DrawRegister(StringBuilder sb, double x, double y, IReadOnlyList<PartRegisterLine> lines)
    {
        var yy = y;
        sb.AppendLine(
            $"<text x=\"{F(x)}\" y=\"{F(yy)}\" font-size=\"8\" font-family=\"Consolas,monospace\" fill=\"#333\">Part · Assembly · Qty</text>");
        yy += 12;
        foreach (var line in lines)
        {
            sb.AppendLine(
                $"<text x=\"{F(x)}\" y=\"{F(yy)}\" font-size=\"8\" font-family=\"Consolas,monospace\" fill=\"#222\">{Esc($"{Trim(line.PartId, 22)}  {Trim(line.OwningAssembly, 18)}  ×{line.Quantity}")}</text>");
            yy += 11;
            if (yy > 560)
                break;
        }
    }

    // Leave bottom-right clear for ISO 7200 title block (x≥521, y≥504).
    private static List<(double X, double Y, double W, double H)> LayoutSlots(int count) =>
        count switch
        {
            <= 1 => [(28, 36, 780, 340)],
            2 => [(28, 36, 500, 340), (540, 36, 270, 150)],
            _ => [(28, 36, 500, 340), (540, 36, 270, 150), (540, 210, 270, 160)],
        };

    private static string Sanitize(string id)
    {
        var chars = id.Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray();
        return new string(chars);
    }

    private static string Trim(string s, int n) =>
        s.Length <= n ? s : s[..(n - 1)] + "…";

    private static string F(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);
    private static string Esc(string s) => System.Net.WebUtility.HtmlEncode(s);
}
