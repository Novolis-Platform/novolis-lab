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
        var knuckleStringers = entities.Count(e =>
            e.Name?.StartsWith("stringer-knuckle-", StringComparison.OrdinalIgnoreCase) == true
            || e.Name?.StartsWith("stringer-join-", StringComparison.OrdinalIgnoreCase) == true
            || (e.Properties is not null
                && e.Properties.TryGetValue("platingJoin", out var pj)
                && pj.ValueKind == JsonValueKind.True));
        checks.Add(new("hull stringers present", stringers.Count >= 12 || knuckleStringers >= 12,
            $"stringers={stringers.Count} knuckleSegs={knuckleStringers}"));
        checks.Add(new("shell panels present", shells.Count >= 8, $"shell={shells.Count}"));

        // Path-swept knuckles: each vertex has a segment whose stamped span covers mid-bay Z.
        var joinMiss = 0;
        var platingInset = CalypsoShipbuildingFrames.PlatingJoinInset;
        foreach (var zStem in new[] { 26f, 30f }) // bay mid-stations F24–28 and F28–32
        {
            var ring = CalypsoShipbuildingFrames.InnerOctagonRing(zStem, platingInset);
            var (cy, cu) = ring.GetSectionCenter();
            for (var i = 0; i < ring.Length; i++)
            {
                var vx = ring[i].Y;
                var vu = ring[i].Up;
                var member = entities.FirstOrDefault(e =>
                    e.Name?.Contains($"stringer-knuckle-v{i}-", StringComparison.OrdinalIgnoreCase) == true
                    && e.Properties is not null
                    && e.Properties.TryGetValue("z0", out var z0)
                    && e.Properties.TryGetValue("z1", out var z1)
                    && z0.GetSingle() - 0.05f <= zStem
                    && zStem <= z1.GetSingle() + 0.05f);
                if (member?.Points is not { Count: >= 1 })
                {
                    joinMiss++;
                    continue;
                }

                var c = member.Points[0];
                // Member center must lie near the inward ray from the plating join at mid-bay.
                var toC = (c[0] - vx, c[1] - vu);
                var toIn = (cy - vx, cu - vu);
                var inLen = MathF.Sqrt(toIn.Item1 * toIn.Item1 + toIn.Item2 * toIn.Item2);
                var dist = MathF.Sqrt(toC.Item1 * toC.Item1 + toC.Item2 * toC.Item2);
                if (inLen < 1e-4f || dist > 0.85f)
                {
                    joinMiss++;
                    continue;
                }

                var align = (toC.Item1 * toIn.Item1 + toC.Item2 * toIn.Item2)
                            / (dist * inLen);
                if (align < 0.75f)
                    joinMiss++;
            }
        }

        checks.Add(new("plating-join longitudinals at 8 octagon knuckles", joinMiss == 0,
            $"misses={joinMiss} (expect 0 of 16 join seats)"));
        checks.Add(new("cellular deck cores present",
            decks.Count(e => e.Name!.Contains("-rib-", StringComparison.OrdinalIgnoreCase)
                             || e.Name!.Contains("-peri-", StringComparison.OrdinalIgnoreCase)) >= 8,
            $"deckParts={decks.Count}"));

        // Frame ring closure: each ring-i bar sits on IML octagon edge i (Y–Up plane).
        var ringGaps = 0;
        foreach (var group in rings.GroupBy(e => FrameAsm(e.Name!)))
        {
            var zStem = group
                .Select(e => e.Properties is not null
                             && e.Properties.TryGetValue("stationZ", out var sz)
                             && sz.ValueKind == JsonValueKind.Number
                    ? (float)sz.GetDouble()
                    : float.NaN)
                .FirstOrDefault(z => !float.IsNaN(z));
            if (float.IsNaN(zStem))
            {
                ringGaps += 8;
                continue;
            }

            // Bay frames use plating inset (knuckles = outer-hull plate seams).
            var expected = CalypsoShipbuildingFrames.InnerOctagonRing(
                zStem, CalypsoShipbuildingFrames.PlatingJoinInset);
            for (var i = 0; i < expected.Length; i++)
            {
                var a = expected[i];
                var b = expected[(i + 1) % expected.Length];
                var midY = (a.Y + b.Y) * 0.5f;
                var midU = (a.Up + b.Up) * 0.5f;
                var bar = group.FirstOrDefault(e =>
                    e.Name!.Contains($"-ring-{i}", StringComparison.OrdinalIgnoreCase)
                    || e.Name.EndsWith($"-ring-{i}", StringComparison.OrdinalIgnoreCase));
                // Name may be frame-F24-ring-0 — match by suffix index.
                bar ??= group.FirstOrDefault(e =>
                {
                    var n = e.Name!;
                    var idx = n.LastIndexOf("-ring-", StringComparison.OrdinalIgnoreCase);
                    return idx >= 0
                           && int.TryParse(n[(idx + 6)..], out var ri)
                           && ri == i;
                });
                if (bar?.Points is not { Count: >= 1 })
                {
                    ringGaps++;
                    continue;
                }

                // CAD: X=Y_lock, Y=Up — compare in section plane (ignore Z).
                var c = bar.Points[0];
                var dy = c[0] - midY;
                var du = c[1] - midU;
                if (MathF.Sqrt(dy * dy + du * du) > 0.85f)
                    ringGaps++;
            }
        }

        checks.Add(new("frame ring edge continuity (geometric)", ringGaps == 0, $"edgeMisses={ringGaps}"));

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
            var (geomOk, detail) = JointGeometryOk(j, byName, loa);
            jointRows.Add((j.Id, j.MemberA, j.MemberB, geomOk, detail));
            if (!geomOk)
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
        var as_ = FindMembers(j.MemberA, byName);
        var bs = FindMembers(j.MemberB, byName);
        if (as_.Count == 0 || bs.Count == 0)
            return (false, "member missing");

        var boxesA = as_.Select(WorldBox).Where(b => b is not null).Select(b => b!).ToList();
        var boxesB = bs.Select(WorldBox).Where(b => b is not null).Select(b => b!).ToList();
        if (boxesA.Count == 0 || boxesB.Count == 0)
            return (false, "no AABB");

        var iface = LockToWorld(j.Interface.Y, j.Interface.Up, j.Interface.ZStem, loa);
        var tol = MathF.Max(0.35f, j.Interface.ToleranceM);
        var da = boxesA.Min(b => DistToAabb(iface, b));
        // Frame assemblies: stringers/girders sit inboard of the ring — score section-plane
        // distance to the nearest ring bar (ignore thin web depth on Z).
        var frameSide = j.MemberA.StartsWith("frame-", StringComparison.OrdinalIgnoreCase)
                        || j.MemberB.StartsWith("frame-", StringComparison.OrdinalIgnoreCase);
        float db;
        if (frameSide)
        {
            var frameBoxes = j.MemberB.StartsWith("frame-", StringComparison.OrdinalIgnoreCase) ? boxesB : boxesA;
            db = frameBoxes.Min(b => SectionDist(iface, b));
            // Longitudinal must reach the frame station in Z.
            var longBoxes = j.MemberB.StartsWith("frame-", StringComparison.OrdinalIgnoreCase) ? boxesA : boxesB;
            var zReach = longBoxes.Min(b =>
            {
                var z0 = MathF.Min(b.EndA.Z, b.EndB.Z);
                var z1 = MathF.Max(b.EndA.Z, b.EndB.Z);
                if (iface.Z >= z0 - 0.05f && iface.Z <= z1 + 0.05f)
                    return 0f;
                return MathF.Min(MathF.Abs(iface.Z - z0), MathF.Abs(iface.Z - z1));
            });
            if (zReach > 0.2f)
                return (false, $"Z reach={zReach:0.###}");
            // Inboard pull to clear BoxFits is expected (~1 m on side flats).
            if (db > 1.35f)
                return (false, $"section dist to frame={db:0.###}");
        }
        else
        {
            db = boxesB.Min(b => DistToAabb(iface, b));
            if (da > tol || db > tol)
                return (false, $"iface dist A={da:0.###} B={db:0.###} tol={tol:0.###}");
        }

        if (!frameSide && da > tol)
            return (false, $"iface dist A={da:0.###}");

        var gap = boxesA.Min(a => boxesB.Min(b => AabbGap(a, b)));
        if (gap > MathF.Max(tol * 2.5f, 1.5f))
            return (false, $"member gap={gap:0.###}");
        return (true, $"gap={gap:0.###} secB={db:0.###}");
    }

    private static float SectionDist(System.Numerics.Vector3 p, WBox box)
    {
        // CAD X/Y = lock Y/Up — distance in the transverse section plane.
        var dx = MathF.Abs(p.X - box.Center.X) - box.Half.X;
        var dy = MathF.Abs(p.Y - box.Center.Y) - box.Half.Y;
        dx = MathF.Max(0f, dx);
        dy = MathF.Max(0f, dy);
        return MathF.Sqrt(dx * dx + dy * dy);
    }

    private static List<CadEntity> FindMembers(string key, Dictionary<string, CadEntity> byName)
    {
        var list = new List<CadEntity>();
        if (byName.TryGetValue(key, out var exact))
            list.Add(exact);
        foreach (var x in byName.Values)
        {
            if (x.Name is null || list.Contains(x))
                continue;
            if (x.Name.StartsWith(key + "-", StringComparison.OrdinalIgnoreCase)
                || string.Equals(x.Name, key, StringComparison.OrdinalIgnoreCase))
            {
                list.Add(x);
                continue;
            }

            if (x.Properties is not null
                && x.Properties.TryGetValue("assembly", out var a)
                && a.ValueKind == JsonValueKind.String
                && string.Equals(a.GetString(), key, StringComparison.OrdinalIgnoreCase))
                list.Add(x);
        }

        return list;
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
