using System.Globalization;
using System.Text;
using CalypsoCad.Geometry;
using CalypsoCad.Structure;
using Novolis.Cad.Primitives;

namespace CalypsoCad.Blueprints.DrawingPackage;

/// <summary>One continuous knuckle path: profile polyline + segment register from CAD meshes.</summary>
internal static class KnuckleStringerDrawingSheet
{
    public sealed record Seg(string Name, float Z0, float Z1, float Y0, float Up0, float Y1, float Up1);

    public static string Build(DrawingEntry entry, CadDocument cad, int vertex)
    {
        var ship = PackageDrawingChrome.ReadShip(cad);
        CadJsonStructureQuery.EnsureHullGeometry(cad);
        var hull = HullGeometry.RequireActive();
        var segs = LoadSegments(cad, hull, vertex);
        var label = $"knuckle-v{vertex}";

        var sb = PackageDrawingChrome.BeginHtml(
            entry, label,
            [
                ("Assembly", label),
                ("Vertex", vertex.ToString(CultureInfo.InvariantCulture)),
                ("Segments", segs.Count.ToString(CultureInfo.InvariantCulture)),
                ("Span", segs.Count == 0
                    ? "—"
                    : $"z={segs.Min(s => s.Z0):0.##}→{segs.Max(s => s.Z1):0.##} m"),
            ],
            "Hull knuckle stringer · continuous 3D path · CAD swept segments");

        PackageDrawingChrome.Label(sb, 28, 32, "A — PROFILE REF CL (stern ←) · knuckle path");
        PackageDrawingChrome.DrawProfileHull(sb, 28, 42, 520, 200, ship, showDecks: true, (ox, keelY, scale) =>
        {
            foreach (var bh in ship.Bulkheads)
                PackageDrawingChrome.MarkAthwartStation(sb, ship, ox, keelY, scale, bh.ZStem, null, focus: false, profile: true);
            DrawPathProfile(sb, ship, ox, keelY, scale, segs);
        });

        PackageDrawingChrome.Label(sb, 560, 32, "B — PLAN REF (stern ←)");
        PackageDrawingChrome.DrawPlanHull(sb, 560, 42, 260, 160, ship, (ox, oy, scale) =>
        {
            foreach (var bh in ship.Bulkheads)
                PackageDrawingChrome.MarkAthwartStation(sb, ship, ox, oy, scale, bh.ZStem, null, focus: false, profile: false);
            DrawPathPlan(sb, ship, ox, oy, scale, segs);
        });

        PackageDrawingChrome.Label(sb, 560, 220, "C — SEGMENT REGISTER");
        DrawSegmentTable(sb, 560, 230, 260, 300, segs);

        PackageDrawingChrome.NoteLines(sb, 28, 270,
            "Path follows InnerOctagonRing knuckles at frame + BH joints.",
            "Each segment is a swept rectangular mesh along the 3D diagonal.",
            segs.Count == 0 ? "WARNING: no CAD knuckle segments for this vertex." : $"{segs.Count} manufactured segments.");

        return PackageDrawingChrome.EndHtml(sb);
    }

    private static List<Seg> LoadSegments(CadDocument cad, HullGeometry hull, int vertex)
    {
        var list = new List<Seg>();
        var inset = CalypsoShipbuildingFrames.PlatingJoinInset;
        foreach (var e in cad.Entities ?? [])
        {
            if (e.Name is null
                || !e.Name.StartsWith($"stringer-knuckle-v{vertex}-", StringComparison.OrdinalIgnoreCase)
                || e.Properties is null
                || !e.Properties.TryGetValue("z0", out var z0El)
                || !e.Properties.TryGetValue("z1", out var z1El))
                continue;
            var z0 = z0El.GetSingle();
            var z1 = z1El.GetSingle();
            var r0 = hull.InnerOctagonRing(z0, inset);
            var r1 = hull.InnerOctagonRing(z1, inset);
            if (vertex < 0 || vertex >= r0.Length || vertex >= r1.Length)
                continue;
            list.Add(new Seg(e.Name, z0, z1, r0[vertex].Y, r0[vertex].Up, r1[vertex].Y, r1[vertex].Up));
        }

        return list.OrderBy(s => s.Z0).ToList();
    }

    private static void DrawPathProfile(
        StringBuilder sb, PackageDrawingChrome.ShipBasics ship,
        double ox, double keelY, double scale, IReadOnlyList<Seg> segs)
    {
        if (segs.Count == 0)
            return;
        var d = new StringBuilder("M");
        var first = true;
        foreach (var s in segs)
        {
            var x0 = LockDraftGeometry.ShipX(ox, scale, ship.Loa, s.Z0);
            var y0 = keelY - s.Up0 * scale;
            var x1 = LockDraftGeometry.ShipX(ox, scale, ship.Loa, s.Z1);
            var y1 = keelY - s.Up1 * scale;
            if (first)
            {
                d.Append($" {PackageDrawingChrome.F(x0)} {PackageDrawingChrome.F(y0)}");
                first = false;
            }

            d.Append($" L {PackageDrawingChrome.F(x1)} {PackageDrawingChrome.F(y1)}");
        }

        sb.AppendLine($"<path d=\"{d}\" fill=\"none\" stroke=\"#c82828\" stroke-width=\"2.2\"/>");
    }

    private static void DrawPathPlan(
        StringBuilder sb, PackageDrawingChrome.ShipBasics ship,
        double ox, double oy, double scale, IReadOnlyList<Seg> segs)
    {
        foreach (var s in segs)
        {
            var x0 = LockDraftGeometry.ShipX(ox, scale, ship.Loa, s.Z0);
            var x1 = LockDraftGeometry.ShipX(ox, scale, ship.Loa, s.Z1);
            var y0 = oy - s.Y0 * scale;
            var y1 = oy - s.Y1 * scale;
            sb.AppendLine(
                $"<line x1=\"{PackageDrawingChrome.F(x0)}\" y1=\"{PackageDrawingChrome.F(y0)}\" x2=\"{PackageDrawingChrome.F(x1)}\" y2=\"{PackageDrawingChrome.F(y1)}\" stroke=\"#c82828\" stroke-width=\"1.6\"/>");
        }
    }

    private static void DrawSegmentTable(
        StringBuilder sb, double x, double y, double w, double h, IReadOnlyList<Seg> segs)
    {
        sb.AppendLine($"<rect x=\"{PackageDrawingChrome.F(x)}\" y=\"{PackageDrawingChrome.F(y)}\" width=\"{PackageDrawingChrome.F(w)}\" height=\"{PackageDrawingChrome.F(h)}\" fill=\"#fff\" stroke=\"#333\"/>");
        sb.AppendLine($"<text x=\"{PackageDrawingChrome.F(x + 8)}\" y=\"{PackageDrawingChrome.F(y + 16)}\" font-size=\"10\" fill=\"#333\">#  z0→z1 (m)  part</text>");
        var row = 0;
        foreach (var s in segs.Take(18))
        {
            var yy = y + 32 + row * 14;
            sb.AppendLine(
                $"<text x=\"{PackageDrawingChrome.F(x + 8)}\" y=\"{PackageDrawingChrome.F(yy)}\" font-size=\"9\" fill=\"#222\">{PackageDrawingChrome.Esc($"{row}  {s.Z0:0.#}→{s.Z1:0.#}  {s.Name}")}</text>");
            row++;
        }

        if (segs.Count > 18)
            sb.AppendLine($"<text x=\"{PackageDrawingChrome.F(x + 8)}\" y=\"{PackageDrawingChrome.F(y + h - 10)}\" font-size=\"9\" fill=\"#666\">… +{segs.Count - 18} more</text>");
    }
}
