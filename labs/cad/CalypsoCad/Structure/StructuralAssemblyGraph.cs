using System.Text.Json;
using Novolis.Cad.Primitives;

namespace CalypsoCad.Structure;

/// <summary>
/// Graph of primary steel members and validated joints (Shipbuilding 102).
/// Hidden shell still participates — visibility is not connectivity.
/// </summary>
internal static class StructuralAssemblyGraph
{
    public sealed record MemberNode(string Name, string Role, string Assembly, bool IsShell);

    public sealed record ValidationResult(
        IReadOnlyList<StructuralJoint> Joints,
        IReadOnlyList<MemberNode> Members,
        IReadOnlyList<string> IsolatedMembers,
        IReadOnlyList<string> FreeEnds,
        IReadOnlyList<string> InvalidJoints,
        IReadOnlyList<string> GirderGaps,
        bool Ok);

    public static ValidationResult Validate(CadDocument doc, IReadOnlyList<StructuralJoint>? joints = null)
    {
        var entities = doc.Entities ?? [];
        var jointList = joints?.ToList() ?? ReadJointsFromDocument(doc);
        // Boxes and meshes — primitive kind must not exclude pressure bulkhead plates.
        var members = entities
            .Where(e => string.Equals(e.Kind, "box", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(e.Kind, "mesh", StringComparison.OrdinalIgnoreCase))
            .Where(IsPrimarySteel)
            .Select(e => new MemberNode(
                e.Name ?? e.Id.ToString("N"),
                ReadRole(e),
                // Legacy assembly string keeps joint Expand working until Phase 4.
                ReadAssembly(e),
                IsShellEntity(e)))
            .ToList();

        // Names can collide across bays (deck-sandwich-dk0-b0-*); key by unique Name+index.
        var byName = new Dictionary<string, MemberNode>(StringComparer.OrdinalIgnoreCase);
        foreach (var m in members)
        {
            if (!byName.ContainsKey(m.Name))
                byName[m.Name] = m;
        }
        var adj = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var m in members)
            adj[m.Name] = [];

        var invalidJoints = new List<string>();
        foreach (var j in jointList)
        {
            var aOk = byName.ContainsKey(j.MemberA) || AssemblyExists(byName, j.MemberA);
            var bOk = byName.ContainsKey(j.MemberB) || AssemblyExists(byName, j.MemberB);
            if (!aOk || !bOk || !j.GeometryValid)
            {
                invalidJoints.Add(j.Id);
                continue;
            }

            Link(adj, j.MemberA, j.MemberB, byName);
        }

        // Name-prefix joins metadata (legacy) — treat as soft edges until geometry validated.
        foreach (var e in entities.Where(IsPrimarySteel))
        {
            if (e.Name is null || e.Properties is null)
                continue;
            if (!e.Properties.TryGetValue("joins", out var joinsEl) || joinsEl.ValueKind != JsonValueKind.Array)
                continue;
            foreach (var j in joinsEl.EnumerateArray())
            {
                if (j.ValueKind != JsonValueKind.String)
                    continue;
                var other = j.GetString();
                if (string.IsNullOrWhiteSpace(other))
                    continue;
                Link(adj, e.Name, other, byName);
            }
        }

        var girderGaps = FindGirderGaps(entities);
        girderGaps.AddRange(FindKnuckleStringerGaps(entities));
        var isolated = new List<string>();
        var freeEnds = new List<string>();

        // Shell-aware: a frame ring connected only via shell is still assembled.
        foreach (var m in members)
        {
            if (m.IsShell)
                continue;
            if (!adj.TryGetValue(m.Name, out var nbrs) || nbrs.Count == 0)
            {
                // Assembly-level soft link: any member sharing assembly with a jointed peer.
                if (!AssemblyHasAnyEdge(m.Assembly, adj, byName))
                    isolated.Add(m.Name);
                else
                    freeEnds.Add(m.Name);
            }
        }

        var ok = invalidJoints.Count == 0
                 && girderGaps.Count == 0
                 && isolated.Count == 0;

        return new ValidationResult(jointList, members, isolated, freeEnds, invalidJoints, girderGaps, ok);
    }

    public static List<StructuralJoint> ReadJointsFromDocument(CadDocument doc)
    {
        var list = new List<StructuralJoint>();
        if (doc.Properties is null
            || !doc.Properties.TryGetValue("structuralJoints", out var el)
            || el.ValueKind != JsonValueKind.Array)
            return list;

        foreach (var item in el.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
                continue;
            var id = item.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? "" : "";
            var a = item.TryGetProperty("memberA", out var aEl) ? aEl.GetString() ?? "" : "";
            var b = item.TryGetProperty("memberB", out var bEl) ? bEl.GetString() ?? "" : "";
            var typeStr = item.TryGetProperty("type", out var tEl) ? tEl.GetString() ?? "FilletWeld" : "FilletWeld";
            Enum.TryParse<StructuralJointType>(typeStr, ignoreCase: true, out var type);
            var valid = item.TryGetProperty("geometryValid", out var vEl) && vEl.ValueKind == JsonValueKind.True;
            float Y = 0, Up = 0, Z = 0;
            if (item.TryGetProperty("interface", out var iface) && iface.ValueKind == JsonValueKind.Object)
            {
                if (iface.TryGetProperty("y", out var y)) Y = (float)y.GetDouble();
                if (iface.TryGetProperty("up", out var u)) Up = (float)u.GetDouble();
                if (iface.TryGetProperty("zStem", out var z)) Z = (float)z.GetDouble();
            }

            list.Add(new StructuralJoint(
                id, a, b, type,
                new JointInterface(Y, Up, Z, 0, 0, 1),
                new JointSpecification("fillet"),
                valid));
        }

        return list;
    }

    public static void WriteJointsToDocument(CadDocument doc, IReadOnlyList<StructuralJoint> joints)
    {
        doc.Properties ??= new Dictionary<string, JsonElement>();
        var arr = joints.Select(j => new
        {
            id = j.Id,
            memberA = j.MemberA,
            memberB = j.MemberB,
            type = j.Type.ToString(),
            geometryValid = j.GeometryValid,
            @interface = new
            {
                y = j.Interface.Y,
                up = j.Interface.Up,
                zStem = j.Interface.ZStem,
                toleranceM = j.Interface.ToleranceM,
            },
            specification = new { weldClass = j.Specification.WeldClass, notes = j.Specification.Notes },
        }).ToArray();
        doc.Properties["structuralJoints"] = JsonSerializer.SerializeToElement(arr);
    }

    private static bool IsPrimarySteel(CadEntity e)
    {
        var n = e.Name ?? "";
        // Graph membership stays name-gated so outfit/closures stamped via MakeSolid do not
        // inflate the primary steel set. Migrated kinds refine identity once admitted.
        var namePrimary = n.StartsWith("frame-", StringComparison.OrdinalIgnoreCase)
               || n.StartsWith("girder-", StringComparison.OrdinalIgnoreCase)
               || n.StartsWith("stringer-", StringComparison.OrdinalIgnoreCase)
               || n.StartsWith("shell-", StringComparison.OrdinalIgnoreCase)
               || n.StartsWith("deck-sandwich-", StringComparison.OrdinalIgnoreCase)
               || n.Contains("-join-", StringComparison.OrdinalIgnoreCase)
               || n.StartsWith("bh-", StringComparison.OrdinalIgnoreCase);
        if (!namePrimary)
            return false;

        if (e.Properties is not null
            && e.Properties.TryGetValue("cadPartKind", out var kindEl)
            && kindEl.ValueKind == JsonValueKind.String
            && Enum.TryParse<Model.CadPartKind>(kindEl.GetString(), ignoreCase: true, out var kind))
        {
            return kind is not (Model.CadPartKind.Unknown or Model.CadPartKind.Partition or Model.CadPartKind.Hatch);
        }

        return true;
    }

    private static bool IsShellEntity(CadEntity e)
    {
        var n = e.Name ?? "";
        if (n.StartsWith("shell-", StringComparison.OrdinalIgnoreCase))
            return true;
        var role = ReadRole(e);
        return string.Equals(role, StructuralRoles.ShellPanel, StringComparison.OrdinalIgnoreCase)
               || string.Equals(role, StructuralRoles.ShellStiffener, StringComparison.OrdinalIgnoreCase);
    }

    private static string ReadRole(CadEntity e) =>
        e.Properties is not null
        && e.Properties.TryGetValue("structuralRole", out var r)
        && r.ValueKind == JsonValueKind.String
            ? r.GetString() ?? ""
            : "";

    private static string ReadAssembly(CadEntity e) =>
        e.Properties is not null
        && e.Properties.TryGetValue("assembly", out var a)
        && a.ValueKind == JsonValueKind.String
            ? a.GetString() ?? ""
            : e.Name ?? "";

    /// <summary>Prefer stamped assemblyId; fall back to legacy assembly string for joint Expand.</summary>
    private static string ReadAssemblyIdOrLegacy(CadEntity e)
    {
        if (e.Properties is not null
            && e.Properties.TryGetValue("assemblyId", out var aid)
            && aid.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(aid.GetString()))
            return aid.GetString()!;
        return ReadAssembly(e);
    }

    private static bool AssemblyExists(Dictionary<string, MemberNode> byName, string key) =>
        byName.ContainsKey(key)
        || byName.Values.Any(m => string.Equals(m.Assembly, key, StringComparison.OrdinalIgnoreCase)
                                  || m.Name.StartsWith(key, StringComparison.OrdinalIgnoreCase));

    private static void Link(
        Dictionary<string, HashSet<string>> adj,
        string a,
        string b,
        Dictionary<string, MemberNode> byName)
    {
        foreach (var left in Expand(a, byName))
        foreach (var right in Expand(b, byName))
        {
            if (!adj.ContainsKey(left))
                adj[left] = [];
            if (!adj.ContainsKey(right))
                adj[right] = [];
            adj[left].Add(right);
            adj[right].Add(left);
        }
    }

    private static IEnumerable<string> Expand(string key, Dictionary<string, MemberNode> byName)
    {
        if (byName.ContainsKey(key))
        {
            yield return key;
            yield break;
        }

        foreach (var m in byName.Values)
        {
            if (string.Equals(m.Assembly, key, StringComparison.OrdinalIgnoreCase)
                || m.Name.StartsWith(key + "-", StringComparison.OrdinalIgnoreCase)
                || m.Name.StartsWith(key, StringComparison.OrdinalIgnoreCase))
                yield return m.Name;
        }
    }

    private static bool AssemblyHasAnyEdge(
        string assembly,
        Dictionary<string, HashSet<string>> adj,
        Dictionary<string, MemberNode> byName)
    {
        if (string.IsNullOrEmpty(assembly))
            return false;
        foreach (var m in byName.Values.Where(x => string.Equals(x.Assembly, assembly, StringComparison.OrdinalIgnoreCase)))
        {
            if (adj.TryGetValue(m.Name, out var n) && n.Count > 0)
                return true;
        }

        return false;
    }

    private static List<string> FindGirderGaps(List<CadEntity> entities)
    {
        var fails = new List<string>();
        var groups = entities
            .Where(e => e.Name?.StartsWith("girder-", StringComparison.OrdinalIgnoreCase) == true
                        && e.Points is { Count: >= 2 })
            .Select(e =>
            {
                var c = e.Points![0];
                var h = e.Points[1];
                var zc = c.Length > 2 ? c[2] : 0f;
                var hz = h.Length > 2 ? MathF.Abs(h[2]) : 0f;
                var asm = ReadAssembly(e);
                return (Asm: asm, Name: e.Name!, Z0: zc - hz, Z1: zc + hz);
            })
            .GroupBy(g => g.Asm);

        foreach (var g in groups)
        {
            var segs = g.OrderBy(s => s.Z0).ToList();
            for (var i = 1; i < segs.Count; i++)
            {
                var gap = segs[i].Z0 - segs[i - 1].Z1;
                if (gap < -0.35f || gap > 3.5f)
                    continue; // heavy overlap / distant region
                // Only positive clearance is a discontinuity (overlap into frame web is intentional).
                if (gap > 0.05f)
                    fails.Add($"{g.Key}: {segs[i - 1].Name}‥{segs[i].Name} gap={gap:0.###}m");
            }
        }

        return fails;
    }

    /// <summary>
    /// Continuity of path-swept knuckle stringers using stamped z0/z1 (not AABB Z).
    /// </summary>
    private static List<string> FindKnuckleStringerGaps(List<CadEntity> entities)
    {
        var fails = new List<string>();
        var groups = entities
            .Where(e => e.Name?.StartsWith("stringer-knuckle-", StringComparison.OrdinalIgnoreCase) == true
                        && e.Properties is not null
                        && e.Properties.TryGetValue("z0", out var z0)
                        && z0.ValueKind == JsonValueKind.Number
                        && e.Properties.TryGetValue("z1", out var z1)
                        && z1.ValueKind == JsonValueKind.Number)
            .Select(e =>
            {
                var z0 = e.Properties!["z0"].GetSingle();
                var z1 = e.Properties!["z1"].GetSingle();
                var asm = e.Properties.TryGetValue("assembly", out var a) && a.ValueKind == JsonValueKind.String
                    ? a.GetString() ?? ReadAssembly(e)
                    : ReadAssembly(e);
                return (Asm: asm, Name: e.Name!, Z0: MathF.Min(z0, z1), Z1: MathF.Max(z0, z1));
            })
            .GroupBy(g => g.Asm, StringComparer.OrdinalIgnoreCase);

        foreach (var g in groups)
        {
            var segs = g.OrderBy(s => s.Z0).ToList();
            if (segs.Count == 0)
                continue;
            for (var i = 1; i < segs.Count; i++)
            {
                var gap = segs[i].Z0 - segs[i - 1].Z1;
                // Path segments share joint stations — tiny positive gap is a discontinuity.
                if (gap > 0.08f)
                    fails.Add($"{g.Key}: {segs[i - 1].Name}‥{segs[i].Name} gap={gap:0.###}m");
            }
        }

        return fails;
    }
}
