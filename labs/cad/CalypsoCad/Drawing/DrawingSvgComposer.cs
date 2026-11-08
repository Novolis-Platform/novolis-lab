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

        var sb = PackageDrawingChrome.BeginHtml(
            entry,
            request.SubjectId,
            block,
            "Hierarchy-driven drawing · actual CAD geometry · ISO 128-15");

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
            PackageDrawingChrome.Label(sb, 560, 420, "PART REGISTER");
            DrawRegister(sb, 560, 430, register.Take(12).ToList());
        }

        if (manifest.Diagnostics.Count > 0)
        {
            PackageDrawingChrome.NoteLines(sb, 28, 520,
                manifest.Diagnostics.Take(4).ToArray());
        }
        else
        {
            PackageDrawingChrome.NoteLines(sb, 28, 520,
                "Geometry projected from ShipPartRegistry ownership.",
                "Viewport visibility does not affect this drawing.",
                $"Ok={manifest.Ok}");
        }

        return PackageDrawingChrome.EndHtml(sb);
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
                DrawSubjectExtentOnPlan(sb, resolved, ship, ox, oy, scale);
            });
            DrawOrientationMarker(sb, x, y, view);
            return;
        }

        if (contextPanel && view.Orientation is StandardViewOrientation.ProfileSternLeft)
        {
            PackageDrawingChrome.DrawProfileHull(sb, x, y, w, h, ship, showDecks: true, (ox, keelY, scale) =>
            {
                DrawSubjectExtentOnProfile(sb, resolved, ship, ox, keelY, scale);
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
    /// </summary>
    private static void DrawSubjectExtentOnPlan(
        StringBuilder sb,
        SubjectGeometryResolver.ResolveResult resolved,
        PackageDrawingChrome.ShipBasics ship,
        double ox,
        double oy,
        double scale)
    {
        var bounds = resolved.Geometry.Aggregate(DrawingBounds3.Empty, (a, g) => a.Union(g.Bounds));
        if (bounds.IsEmpty)
            return;

        var z0 = Math.Clamp(bounds.Min.Z, 0, (float)ship.Loa);
        var z1 = Math.Clamp(bounds.Max.Z, 0, (float)ship.Loa);
        var y0 = bounds.Min.X;
        var y1 = bounds.Max.X;
        var x0 = LockDraftGeometry.ShipX(ox, scale, ship.Loa, z0);
        var x1 = LockDraftGeometry.ShipX(ox, scale, ship.Loa, z1);
        var sy0 = oy - y1 * scale;
        var sy1 = oy - y0 * scale;
        var left = Math.Min(x0, x1);
        var right = Math.Max(x0, x1);
        var top = Math.Min(sy0, sy1);
        var bot = Math.Max(sy0, sy1);
        sb.AppendLine(
            $"<rect x=\"{F(left)}\" y=\"{F(top)}\" width=\"{F(Math.Max(1, right - left))}\" height=\"{F(Math.Max(1, bot - top))}\" fill=\"#c82828\" fill-opacity=\"0.18\" stroke=\"#c82828\" stroke-width=\"1.4\"/>");
        // Location context is extent-only — never dump mesh/box triangulation here.
    }

    private static void DrawSubjectExtentOnProfile(
        StringBuilder sb,
        SubjectGeometryResolver.ResolveResult resolved,
        PackageDrawingChrome.ShipBasics ship,
        double ox,
        double keelY,
        double scale)
    {
        var bounds = resolved.Geometry.Aggregate(DrawingBounds3.Empty, (a, g) => a.Union(g.Bounds));
        if (bounds.IsEmpty)
            return;

        var z0 = Math.Clamp(bounds.Min.Z, 0, (float)ship.Loa);
        var z1 = Math.Clamp(bounds.Max.Z, 0, (float)ship.Loa);
        var u0 = Math.Clamp(bounds.Min.Y, 0, (float)ship.Oah);
        var u1 = Math.Clamp(bounds.Max.Y, 0, (float)ship.Oah);
        var x0 = LockDraftGeometry.ShipX(ox, scale, ship.Loa, z0);
        var x1 = LockDraftGeometry.ShipX(ox, scale, ship.Loa, z1);
        var yTop = keelY - u1 * scale;
        var yBot = keelY - u0 * scale;
        var left = Math.Min(x0, x1);
        var right = Math.Max(x0, x1);
        sb.AppendLine(
            $"<rect x=\"{F(left)}\" y=\"{F(Math.Min(yTop, yBot))}\" width=\"{F(Math.Max(1, right - left))}\" height=\"{F(Math.Max(1, Math.Abs(yBot - yTop)))}\" fill=\"#c82828\" fill-opacity=\"0.18\" stroke=\"#c82828\" stroke-width=\"1.4\"/>");
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

    private static List<(double X, double Y, double W, double H)> LayoutSlots(int count) =>
        count switch
        {
            <= 1 => [(28, 42, 780, 360)],
            2 => [(28, 42, 520, 360), (560, 42, 260, 160)],
            _ => [(28, 42, 520, 360), (560, 42, 260, 160), (560, 230, 260, 172)],
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
