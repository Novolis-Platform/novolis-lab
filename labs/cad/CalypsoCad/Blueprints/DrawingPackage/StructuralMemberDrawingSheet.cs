using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.Json;
using Novolis.Cad.Primitives;

namespace CalypsoCad.Blueprints.DrawingPackage;

/// <summary>Structural members only (girders / longitudinals / coamings) + hull refs.</summary>
internal static class StructuralMemberDrawingSheet
{
    public static string Build(
        DrawingEntry entry,
        CadDocument cad,
        IReadOnlyList<string> memberNames)
    {
        var ship = PackageDrawingChrome.ReadShip(cad);
        var boxes = cad.Entities
            .Where(e => e.Name is not null && memberNames.Contains(e.Name, StringComparer.OrdinalIgnoreCase))
            .Select(e => (Name: e.Name!, Box: TryBox(e)))
            .Where(t => t.Box is not null)
            .Select(t => (t.Name, Center: t.Box!.Value.Center, Half: t.Box!.Value.Half))
            .ToList();

        var sb = PackageDrawingChrome.BeginHtml(
            entry, entry.Label,
            [("Members", memberNames.Count.ToString(CultureInfo.InvariantCulture)),
             ("Placed", boxes.Count.ToString(CultureInfo.InvariantCulture))],
            "Primary structural members only · ISO 128-15 · no BH / deck / outfit");

        PackageDrawingChrome.Label(sb, 28, 32, "A — PLAN · members on this sheet");
        DrawMemberPlan(sb, 28, 42, 520, 300, ship, boxes);

        PackageDrawingChrome.Label(sb, 560, 32, "B — PROFILE REF · members");
        DrawMemberProfile(sb, 560, 42, 260, 200, ship, boxes);

        PackageDrawingChrome.Label(sb, 28, 360, "C — MEMBER REGISTER");
        DrawRegister(sb, 28, 372, 800, 190, memberNames, boxes);

        return PackageDrawingChrome.EndHtml(sb);
    }

    private static void DrawMemberPlan(
        StringBuilder sb, double x, double y, double w, double h,
        PackageDrawingChrome.ShipBasics ship,
        IReadOnlyList<(string Name, Vector3 Center, Vector3 Half)> boxes)
    {
        sb.AppendLine($"<rect x=\"{PackageDrawingChrome.F(x)}\" y=\"{PackageDrawingChrome.F(y)}\" width=\"{PackageDrawingChrome.F(w)}\" height=\"{PackageDrawingChrome.F(h)}\" fill=\"#fff\" stroke=\"#333\"/>");
        var scale = LockDraftGeometry.SharedPlanScale(ship.Loa, ship.Beam, w, h);
        var ox = x + LockDraftGeometry.PlanPadL
                 + ((w - LockDraftGeometry.PlanPadL - LockDraftGeometry.PlanPadR) - ship.Loa * scale) * 0.5;
        var oy = y + LockDraftGeometry.PlanPadTop
                 + (h - LockDraftGeometry.PlanPadTop - LockDraftGeometry.PlanPadBot) * 0.5;
        sb.AppendLine($"<path d=\"{PackageDrawingChrome.HullPlanPath(ship.Loft, ship.Loa, ox, oy, scale)}\" fill=\"#f7f8f9\" stroke=\"#aaa\" stroke-width=\"0.8\"/>");
        sb.AppendLine($"<line x1=\"{PackageDrawingChrome.F(ox)}\" y1=\"{PackageDrawingChrome.F(oy)}\" x2=\"{PackageDrawingChrome.F(ox + ship.Loa * scale)}\" y2=\"{PackageDrawingChrome.F(oy)}\" stroke=\"#0645ad\" stroke-dasharray=\"4 2\" stroke-width=\"0.55\"/>");

        foreach (var (name, center, half) in boxes)
        {
            var zStem = ship.Loa * 0.5 - center.Z;
            var color = MemberColor(name);
            var x0 = LockDraftGeometry.ShipX(ox, scale, ship.Loa, zStem + half.Z);
            var x1 = LockDraftGeometry.ShipX(ox, scale, ship.Loa, zStem - half.Z);
            var y0 = oy - (center.X + half.X) * scale;
            var y1 = oy - (center.X - half.X) * scale;
            sb.AppendLine(
                $"<rect x=\"{PackageDrawingChrome.F(Math.Min(x0, x1))}\" y=\"{PackageDrawingChrome.F(Math.Min(y0, y1))}\" width=\"{PackageDrawingChrome.F(Math.Max(1.5, Math.Abs(x1 - x0)))}\" height=\"{PackageDrawingChrome.F(Math.Max(1.5, Math.Abs(y1 - y0)))}\" fill=\"{color}\" fill-opacity=\"0.85\" stroke=\"#222\" stroke-width=\"0.5\"/>");
        }

        sb.AppendLine($"<text x=\"{PackageDrawingChrome.F(x + w * 0.5)}\" y=\"{PackageDrawingChrome.F(y + 14)}\" text-anchor=\"middle\" font-size=\"11\" fill=\"#222\">{boxes.Count} placed · {LockDraftGeometry.ScaleNote(scale)}</text>");
    }

    private static void DrawMemberProfile(
        StringBuilder sb, double x, double y, double w, double h,
        PackageDrawingChrome.ShipBasics ship,
        IReadOnlyList<(string Name, Vector3 Center, Vector3 Half)> boxes)
    {
        PackageDrawingChrome.DrawProfileHull(sb, x, y, w, h, ship, showDecks: true, (ox, keelY, scale) =>
        {
            foreach (var (name, center, half) in boxes)
            {
                var zStem = ship.Loa * 0.5 - center.Z;
                var sx0 = LockDraftGeometry.ShipX(ox, scale, ship.Loa, zStem + half.Z);
                var sx1 = LockDraftGeometry.ShipX(ox, scale, ship.Loa, zStem - half.Z);
                var u0 = keelY - (center.Y + half.Y) * scale;
                var u1 = keelY - (center.Y - half.Y) * scale;
                sb.AppendLine(
                    $"<rect x=\"{PackageDrawingChrome.F(Math.Min(sx0, sx1))}\" y=\"{PackageDrawingChrome.F(Math.Min(u0, u1))}\" width=\"{PackageDrawingChrome.F(Math.Max(1.2, Math.Abs(sx1 - sx0)))}\" height=\"{PackageDrawingChrome.F(Math.Max(1.2, Math.Abs(u1 - u0)))}\" fill=\"{MemberColor(name)}\" fill-opacity=\"0.8\" stroke=\"#222\" stroke-width=\"0.45\"/>");
            }
        });
    }

    private static void DrawRegister(
        StringBuilder sb, double x, double y, double w, double h,
        IReadOnlyList<string> names,
        IReadOnlyList<(string Name, Vector3 Center, Vector3 Half)> boxes)
    {
        sb.AppendLine($"<rect x=\"{PackageDrawingChrome.F(x)}\" y=\"{PackageDrawingChrome.F(y)}\" width=\"{PackageDrawingChrome.F(w)}\" height=\"{PackageDrawingChrome.F(h)}\" fill=\"#fff\" stroke=\"#333\"/>");
        var placed = boxes.Select(b => b.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var colW = w / 3;
        var rowH = 12.0;
        for (var i = 0; i < names.Count; i++)
        {
            var col = i % 3;
            var row = i / 3;
            if (y + 16 + row * rowH > y + h - 6)
                break;
            var tx = x + 8 + col * colW;
            var ty = y + 16 + row * rowH;
            var mark = placed.Contains(names[i]) ? "●" : "○";
            sb.AppendLine($"<text x=\"{PackageDrawingChrome.F(tx)}\" y=\"{PackageDrawingChrome.F(ty)}\" font-size=\"9\" fill=\"#222\" font-family=\"ui-monospace,Consolas,monospace\">{mark} {PackageDrawingChrome.Esc(names[i])}</text>");
        }
    }

    private static string MemberColor(string name)
    {
        if (name.StartsWith("girder-", StringComparison.OrdinalIgnoreCase))
            return "#5b7c99";
        if (name.StartsWith("long-", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("longitudinal-", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("stringer-", StringComparison.OrdinalIgnoreCase))
            return "#6a8f6a";
        if (name.StartsWith("coaming-", StringComparison.OrdinalIgnoreCase))
            return "#9a7b4f";
        return "#7a6a8a";
    }

    private static (Vector3 Center, Vector3 Half)? TryBox(CadEntity e)
    {
        if (e.Center is { Length: >= 3 } c && e.HalfExtents is { Length: >= 3 } h)
            return (new Vector3(c[0], c[1], c[2]), new Vector3(h[0], h[1], h[2]));
        if (e.Points is { Count: >= 2 } pts && pts[0].Length >= 3 && pts[1].Length >= 3)
            return (
                new Vector3(pts[0][0], pts[0][1], pts[0][2]),
                new Vector3(pts[1][0], pts[1][1], pts[1][2]));
        return null;
    }
}
