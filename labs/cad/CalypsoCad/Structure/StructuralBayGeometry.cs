using System.Text;
using System.Text.Json;
using Novolis.Cad.Primitives;

namespace CalypsoCad.Structure;

/// <summary>
/// Geometric (not metadata-only) validation for the two-bay structural proof.
/// </summary>
internal static class StructuralBayGeometry
{
    public sealed record Check(string Name, bool Ok, string Detail);

    public sealed record Report(
        IReadOnlyList<Check> Checks,
        IReadOnlyList<(string Id, string MemberA, string MemberB, bool GeometryOk, string Detail)> Joints,
        bool Ok);

    public static Report Evaluate(CadDocument doc, IReadOnlyList<StructuralJoint> joints, float loa)
    {
        var entities = doc.Entities ?? [];
        var checks = new List<Check>();
        var jointRows = new List<(string, string, string, bool, string)>();

        var frames = entities.Where(e => e.Name?.StartsWith("frame-", StringComparison.OrdinalIgnoreCase) == true).ToList();
        var rings = frames.Where(e => e.Name!.Contains("-ring-", StringComparison.OrdinalIgnoreCase)).ToList();
        var girders = entities.Where(e => e.Name?.StartsWith("girder-", StringComparison.OrdinalIgnoreCase) == true).ToList();
        var stringers = entities.Where(e => e.Name?.StartsWith("stringer-", StringComparison.OrdinalIgnoreCase) == true).ToList();
        var shells = entities.Where(e => e.Name?.StartsWith("shell-", StringComparison.OrdinalIgnoreCase) == true).ToList();
        var decks = entities.Where(e => e.Name?.StartsWith("deck-sandwich-", StringComparison.OrdinalIgnoreCase) == true).ToList();
        var wallLike = entities.Count(e =>
        {
            var n = e.Name ?? "";
            if (n.StartsWith("bh-", StringComparison.OrdinalIgnoreCase))
                return true;
            if (!n.StartsWith("frame-", StringComparison.OrdinalIgnoreCase))
                return false;
            if (e.Points is not { Count: >= 2 })
                return false;
            var h = MathF.Abs((float)e.Points[1][1]) * 2f;
            return h >= 3.5f && !n.Contains("-ring-", StringComparison.OrdinalIgnoreCase);
        });

        checks.Add(new("two open frames present", rings.Count >= 16, $"ringBars={rings.Count} (expect ≥16 for 3×8)"));
        checks.Add(new("no wall-like ordinary frames / BH in bay proof", wallLike == 0, $"wallLike={wallLike}"));
        checks.Add(new("longitudinal girders present", girders.Count >= 6, $"girders={girders.Count}"));
        checks.Add(new("hull stringers present", stringers.Count >= 12, $"stringers={stringers.Count}"));
        checks.Add(new("shell panels present", shells.Count >= 8, $"shell={shells.Count}"));
        checks.Add(new("cellular deck cores present",
            decks.Count(e => e.Name!.Contains("-rib-", StringComparison.OrdinalIgnoreCase)
                             || e.Name!.Contains("-peri-", StringComparison.OrdinalIgnoreCase)) >= 8,
            $"deckParts={decks.Count}"));

        // Frame ring closure: consecutive ring bars of same frame share near endpoints.
        var ringGaps = 0;
        foreach (var group in rings.GroupBy(e => FrameAsm(e.Name!)))
        {
            var bars = group.Select(WorldBox).Where(b => b is not null).Select(b => b!).OrderBy(b => b.Name).ToList();
            if (bars.Count < 6)
            {
                ringGaps++;
                continue;
            }

            // Each bar should have a neighbor within 0.35 m of an endpoint (polygonal chain).
            foreach (var bar in bars)
            {
                var ends = new[] { bar.EndA, bar.EndB };
                var linked = ends.Count(end =>
                    bars.Any(other => other.Name != bar.Name
                                      && (Dist(end, other.EndA) <= 0.35f || Dist(end, other.EndB) <= 0.35f)));
                if (linked < 2)
                    ringGaps++;
            }
        }

        checks.Add(new("frame ring edge continuity (geometric)", ringGaps == 0, $"unclosedEnds={ringGaps}"));

        // Girder continuity across bay boundary at F28 (z≈28 in stem; CAD Z = loa/2 - zStem).
        var keelSegs = girders
            .Where(e => e.Name!.Contains("girder-keel", StringComparison.OrdinalIgnoreCase))
            .Select(WorldBox)
            .Where(b => b is not null)
            .Select(b => b!)
            .OrderBy(b => MathF.Min(b.EndA.Z, b.EndB.Z))
            .ToList();
        var girderGapFails = 0;
        for (var i = 1; i < keelSegs.Count; i++)
        {
            var a1 = keelSegs[i - 1].EndA.Z > keelSegs[i - 1].EndB.Z ? keelSegs[i - 1].EndA : keelSegs[i - 1].EndB;
            var b0 = keelSegs[i].EndA.Z < keelSegs[i].EndB.Z ? keelSegs[i].EndA : keelSegs[i].EndB;
            var gap = b0.Z - a1.Z;
            // Allow small overlap; forbid open gap > 0.15 m.
            if (gap > 0.15f)
                girderGapFails++;
        }

        checks.Add(new("keel girder continuous across bays", girderGapFails == 0 && keelSegs.Count >= 2,
            $"keelSegs={keelSegs.Count} gaps={girderGapFails}"));

        // Geometric joint mating: interface point near both members' AABBs.
        var byName = entities
            .Where(e => e.Name is not null)
            .GroupBy(e => e.Name!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var geomFail = 0;
        foreach (var j in joints)
        {
            var (ok, detail) = JointGeometryOk(j, byName, loa);
            jointRows.Add((j.Id, j.MemberA, j.MemberB, ok, detail));
            if (!ok)
                geomFail++;
        }

        checks.Add(new("joint mating geometry", geomFail == 0 && joints.Count >= 20,
            $"joints={joints.Count} geomFail={geomFail}"));

        // Assembly graph with only geometrically-valid joints.
        var validJoints = joints.Where((j, i) => jointRows[i].Item4).ToList();
        var graph = StructuralAssemblyGraph.Validate(doc, validJoints);
        checks.Add(new("connected primary assembly (geom joints)",
            graph.IsolatedMembers.Count == 0 && graph.GirderGaps.Count == 0,
            $"isolated={graph.IsolatedMembers.Count} girderGaps={graph.GirderGaps.Count} freeEnds={graph.FreeEnds.Count}"));

        // Habitable clearance: no solid filling mid-bay center (Y≈0, Up≈deck) except thin girders.
        var clutter = entities.Count(e =>
        {
            var n = e.Name ?? "";
            if (n.StartsWith("girder-", StringComparison.OrdinalIgnoreCase))
                return false;
            if (n.Contains("-chord-", StringComparison.OrdinalIgnoreCase))
                return false;
            if (n.StartsWith("deck-sandwich-", StringComparison.OrdinalIgnoreCase)
                && (n.Contains("-face-", StringComparison.OrdinalIgnoreCase)
                    || n.Contains("-rib-", StringComparison.OrdinalIgnoreCase)
                    || n.Contains("-peri-", StringComparison.OrdinalIgnoreCase)))
                return false;
            var box = WorldBox(e);
            if (box is null)
                return false;
            var c = box.Center;
            return MathF.Abs(c.X) < 1.5f
                   && c.Y > 1.5f && c.Y < 7f
                   && MathF.Abs(c.Z - (loa * 0.5f - 28f)) < 1.5f
                   && box.Half.X > 1.2f && box.Half.Y > 1.2f;
        });
        checks.Add(new("habitable mid-bay volume clear of wall solids", clutter == 0, $"clutter={clutter}"));

        var ok = checks.All(c => c.Ok);
        return new Report(checks, jointRows, ok);
    }

    public static string WriteReportMarkdown(Report report, string path)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Calypso two-bay structural validation");
        sb.AppendLine();
        sb.AppendLine($"Overall: **{(report.Ok ? "PASS" : "FAIL")}**");
        sb.AppendLine();
        sb.AppendLine("## Checks");
        sb.AppendLine();
        sb.AppendLine("| Check | Result | Detail |");
        sb.AppendLine("| --- | --- | --- |");
        foreach (var c in report.Checks)
            sb.AppendLine($"| {c.Name} | {(c.Ok ? "PASS" : "FAIL")} | {c.Detail} |");
        sb.AppendLine();
        sb.AppendLine("## Joints (geometric mating)");
        sb.AppendLine();
        sb.AppendLine("| Id | A | B | Geometry | Detail |");
        sb.AppendLine("| --- | --- | --- | --- | --- |");
        foreach (var (id, a, b, gOk, detail) in report.Joints)
            sb.AppendLine($"| `{id}` | `{a}` | `{b}` | {(gOk ? "PASS" : "FAIL")} | {detail} |");

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, sb.ToString());
        return path;
    }

    private static (bool Ok, string Detail) JointGeometryOk(
        StructuralJoint j,
        Dictionary<string, CadEntity> byName,
        float loa)
    {
        var a = FindMember(j.MemberA, byName);
        var b = FindMember(j.MemberB, byName);
        if (a is null || b is null)
            return (false, "member missing");

        var ba = WorldBox(a);
        var bb = WorldBox(b);
        if (ba is null || bb is null)
            return (false, "no AABB");

        var iface = LockToWorld(j.Interface.Y, j.Interface.Up, j.Interface.ZStem, loa);
        var tol = MathF.Max(0.12f, j.Interface.ToleranceM);
        var da = DistToAabb(iface, ba);
        var db = DistToAabb(iface, bb);
        if (da > tol || db > tol)
            return (false, $"iface dist A={da:0.###} B={db:0.###} tol={tol:0.###}");

        // Members must nearly touch each other (not merely share a distant interface claim).
        var gap = AabbGap(ba, bb);
        if (gap > tol * 2f)
            return (false, $"member gap={gap:0.###}");
        return (true, $"gap={gap:0.###}");
    }

    private static CadEntity? FindMember(string key, Dictionary<string, CadEntity> byName)
    {
        if (byName.TryGetValue(key, out var e))
            return e;
        return byName.Values.FirstOrDefault(x =>
            x.Name is not null
            && (x.Name.StartsWith(key + "-", StringComparison.OrdinalIgnoreCase)
                || string.Equals(
                    x.Properties?.TryGetValue("assembly", out var a) == true
                    && a.ValueKind == JsonValueKind.String
                        ? a.GetString()
                        : null,
                    key,
                    StringComparison.OrdinalIgnoreCase)));
    }

    private sealed record WBox(string Name, System.Numerics.Vector3 Center, System.Numerics.Vector3 Half, System.Numerics.Vector3 EndA, System.Numerics.Vector3 EndB);

    private static WBox? WorldBox(CadEntity e)
    {
        if (e.Name is null || e.Points is not { Count: >= 2 })
            return null;
        var c = e.Points[0];
        var h = e.Points[1];
        if (c.Length < 3 || h.Length < 3)
            return null;
        var center = new System.Numerics.Vector3(c[0], c[1], c[2]);
        var half = new System.Numerics.Vector3(MathF.Abs(h[0]), MathF.Abs(h[1]), MathF.Abs(h[2]));
        // Longitudinal extent along CAD +Z (stem/stern).
        var endA = center with { Z = center.Z - half.Z };
        var endB = center with { Z = center.Z + half.Z };
        return new WBox(e.Name, center, half, endA, endB);
    }

    private static float Dist(System.Numerics.Vector3 a, System.Numerics.Vector3 b) =>
        System.Numerics.Vector3.Distance(a, b);

    private static float DistToAabb(System.Numerics.Vector3 p, WBox box)
    {
        var d = System.Numerics.Vector3.Abs(p - box.Center) - box.Half;
        var outside = System.Numerics.Vector3.Max(d, System.Numerics.Vector3.Zero);
        return outside.Length();
    }

    private static float AabbGap(WBox a, WBox b)
    {
        var d = System.Numerics.Vector3.Abs(a.Center - b.Center) - (a.Half + b.Half);
        var outside = System.Numerics.Vector3.Max(d, System.Numerics.Vector3.Zero);
        return outside.Length();
    }

    private static System.Numerics.Vector3 LockToWorld(float y, float up, float zStem, float loa) =>
        new(y, up, loa * 0.5f - zStem);

    private static string FrameAsm(string name)
    {
        var segs = name.Split('-');
        return segs.Length >= 2 ? $"frame-{segs[1]}" : name;
    }
}
