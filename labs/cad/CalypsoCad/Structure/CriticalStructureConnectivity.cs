using Novolis.Cad.Primitives;

namespace CalypsoCad.Structure;

/// <summary>
/// Critical primary steel: pressure bulkheads ↔ transverse frames ↔ cellular deck plates.
/// Emits seat flanges and <see cref="StructuralJoint"/>s so the three share one load path.
/// </summary>
internal static class CriticalStructureConnectivity
{
    /// <summary>
    /// After BH / frames / sandwich emit: seat flanges at every BH×deck and frame×deck crossing,
    /// plus joints linking assemblies <c>bh-*</c>, <c>frame-*</c>, and <c>deck-dk*</c>.
    /// </summary>
    public static List<StructuralJoint> Connect(
        List<CadEntity> entities,
        float loa,
        StructuralTopology topo,
        IReadOnlyList<(string Id, float ZStem, float ThicknessScale)> bulkheads)
    {
        var joints = new List<StructuralJoint>();
        var web = topo.Web;
        var tPlate = topo.TPlate;
        var inset = CalypsoShipbuildingFrames.DefaultSkinInset + 0.22f;
        var seatHu = MathF.Max(0.05f, web * 0.32f);
        var seatHz = MathF.Max(web * 0.55f, tPlate * 0.6f);

        foreach (var (id, zStem, scale) in bulkheads)
        {
            var thickness = Math.Clamp(topo.TBh * scale, 0.18f, 0.65f);
            var z = Math.Clamp(zStem, thickness, loa - thickness);
            var bhAsm = $"bh-{id}";
            foreach (var deckUp in topo.DeckUps)
            {
                if (CalypsoShipbuildingFrames.IsOpenAtriumDeck(z, deckUp, topo.OpenAtriumSpans)
                    && !IsHoldFloor(deckUp, topo.DeckUps))
                    continue;

                var halfY = CalypsoLockGenerator.HalfBeamInsidePublic(z, deckUp, inset) - 0.2f;
                if (halfY < 0.4f)
                    continue;

                var deckAsm = DeckAssemblyFor(deckUp, topo);
                var seatName = $"{bhAsm}-deckseat-d{deckUp:0.#}";
                var up = deckUp - tPlate * 0.5f;
                var placed = CalypsoLockGenerator.TryAddStructureBar(
                    entities, loa, seatName, 0f, up, z,
                    halfY * 0.92f, seatHu, MathF.Max(seatHz, thickness * 0.55f),
                    meta: new StructuralSolidMeta(
                        StructuralRoles.StructuralBracket, bhAsm,
                        StationZ: z, DeckUp: deckUp, Joins: [bhAsm, deckAsm]));
                if (!placed)
                    continue;

                joints.Add(Fillet(seatName, bhAsm, deckAsm, 0f, up, z, "BH×deck seat"));
            }
        }

        foreach (var (id, zStem, _) in topo.FrameStations)
        {
            var z = Math.Clamp(zStem, web * 0.5f, loa - web * 0.5f);
            var frameName = id.StartsWith("frame-", StringComparison.OrdinalIgnoreCase) ? id : $"frame-{id}";
            var frameAsm = FrameAssembly(frameName);
            var hold = topo.InOpenHoldCell(zStem);
            var floorDeck = topo.DeckUps.Count > 0 ? topo.DeckUps[0] : 0.5f;

            foreach (var deckUp in topo.DeckUps)
            {
                if (hold && deckUp > floorDeck + 0.15f)
                    continue;
                if (!hold && CalypsoShipbuildingFrames.IsOpenAtriumDeck(z, deckUp, topo.OpenAtriumSpans))
                    continue;

                var halfY = CalypsoLockGenerator.HalfBeamInsidePublic(z, deckUp, inset) - 0.2f;
                if (halfY < 0.35f)
                    continue;

                var deckAsm = DeckAssemblyFor(deckUp, topo);
                var seatName = $"{frameName}-deckseat-d{deckUp:0.#}";
                var up = deckUp - tPlate * 0.45f;
                var placed = CalypsoLockGenerator.TryAddStructureBar(
                    entities, loa, seatName, 0f, up, z,
                    halfY * 0.88f, seatHu, seatHz,
                    meta: new StructuralSolidMeta(
                        StructuralRoles.StructuralBracket, frameAsm,
                        StationZ: z, DeckUp: deckUp, Joins: [frameAsm, deckAsm]));
                if (!placed)
                    continue;

                joints.Add(Fillet(seatName, frameAsm, deckAsm, 0f, up, z, "frame×deck seat"));
            }
        }

        // Nearest frame ↔ BH at each bulkhead station (shared transverse plane family).
        foreach (var (id, zStem, _) in bulkheads)
        {
            var bhAsm = $"bh-{id}";
            var nearest = topo.FrameStations
                .Select(f => (f.Id, f.ZStem, Dist: MathF.Abs(f.ZStem - zStem)))
                .OrderBy(f => f.Dist)
                .FirstOrDefault();
            if (nearest.Id is null || nearest.Dist > topo.FrameBay * 0.55f)
                continue;

            var frameName = nearest.Id.StartsWith("frame-", StringComparison.OrdinalIgnoreCase)
                ? nearest.Id
                : $"frame-{nearest.Id}";
            var frameAsm = FrameAssembly(frameName);
            joints.Add(Fillet(
                $"j-{bhAsm}-{frameAsm}", bhAsm, frameAsm,
                0f, topo.DeckUps.Count > 1 ? topo.DeckUps[1] : 4.4f, zStem,
                "BH×nearest frame"));
        }

        return joints;
    }

    /// <summary>
    /// Critical subgraph OK when every BH and every non-HOLD frame assembly has a path to a deck assembly.
    /// </summary>
    public static (bool Ok, string Detail) Validate(CadDocument doc, IReadOnlyList<StructuralJoint>? joints = null)
    {
        var result = StructuralAssemblyGraph.Validate(doc, joints);
        var byAsm = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        void Link(string a, string b)
        {
            if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b))
                return;
            if (!byAsm.TryGetValue(a, out var sa))
                byAsm[a] = sa = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!byAsm.TryGetValue(b, out var sb))
                byAsm[b] = sb = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            sa.Add(b);
            sb.Add(a);
        }

        foreach (var m in result.Members)
        {
            if (!string.IsNullOrWhiteSpace(m.Assembly))
                Link(m.Name, m.Assembly);
        }

        foreach (var j in result.Joints.Where(j => j.GeometryValid))
            Link(j.MemberA, j.MemberB);

        foreach (var e in doc.Entities ?? [])
        {
            if (e.Name is null || e.Properties is null)
                continue;
            if (!e.Properties.TryGetValue("joins", out var joinsEl)
                || joinsEl.ValueKind != System.Text.Json.JsonValueKind.Array)
                continue;
            var asm = e.Properties.TryGetValue("assembly", out var aEl)
                      && aEl.ValueKind == System.Text.Json.JsonValueKind.String
                ? aEl.GetString() ?? e.Name
                : e.Name;
            foreach (var j in joinsEl.EnumerateArray())
            {
                if (j.ValueKind == System.Text.Json.JsonValueKind.String
                    && j.GetString() is { Length: > 0 } other)
                    Link(asm, other);
            }
        }

        var bhAsms = byAsm.Keys.Where(k => k.StartsWith("bh-", StringComparison.OrdinalIgnoreCase)).ToList();
        var frameAsms = byAsm.Keys
            .Where(k => k.StartsWith("frame-", StringComparison.OrdinalIgnoreCase)
                        && k.Split('-').Length <= 2)
            .ToList();
        var deckAsms = byAsm.Keys.Where(k => k.StartsWith("deck-dk", StringComparison.OrdinalIgnoreCase)).ToList();

        if (bhAsms.Count < 4)
            return (false, $"bhAssemblies={bhAsms.Count}");
        if (frameAsms.Count < 8)
            return (false, $"frameAssemblies={frameAsms.Count}");
        if (deckAsms.Count < 2)
            return (false, $"deckAssemblies={deckAsms.Count}");

        var orphanBh = bhAsms.Count(bh => !ReachesDeck(bh, byAsm, deckAsms));
        var orphanFr = frameAsms.Count(fr => !ReachesDeck(fr, byAsm, deckAsms));
        var ok = orphanBh == 0 && orphanFr == 0;
        return (ok, $"bh→deck orphans={orphanBh}/{bhAsms.Count}; frame→deck orphans={orphanFr}/{frameAsms.Count}; decks={deckAsms.Count}");
    }

    private static bool ReachesDeck(
        string start,
        Dictionary<string, HashSet<string>> adj,
        IReadOnlyList<string> deckAsms)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { start };
        var q = new Queue<string>();
        q.Enqueue(start);
        while (q.Count > 0)
        {
            var n = q.Dequeue();
            if (deckAsms.Any(d => string.Equals(d, n, StringComparison.OrdinalIgnoreCase)
                                  || n.StartsWith(d, StringComparison.OrdinalIgnoreCase)))
                return true;
            if (!adj.TryGetValue(n, out var nbrs))
                continue;
            foreach (var x in nbrs)
            {
                if (seen.Add(x))
                    q.Enqueue(x);
            }
        }

        return false;
    }

    private static StructuralJoint Fillet(
        string id, string a, string b, float y, float up, float z, string notes) =>
        new(
            id, a, b, StructuralJointType.FilletWeld,
            new JointInterface(y, up, z, 0, 0, 1),
            new JointSpecification("fillet", notes),
            GeometryValid: true);

    private static string FrameAssembly(string frameName)
    {
        var segs = frameName.Split('-');
        return segs.Length >= 2 ? $"frame-{segs[1]}" : frameName;
    }

    private static string DeckAssemblyFor(float deckUp, StructuralTopology topo)
    {
        for (var i = 0; i < topo.SandwichDecks.Count; i++)
        {
            if (MathF.Abs(topo.SandwichDecks[i].PlateTop - deckUp) < 0.08f)
            {
                var n = topo.SandwichDecks[i].DeckNum;
                return n < 0 ? "deck-dkm1" : $"deck-dk{n}";
            }
        }

        return "deck-dk0";
    }

    private static bool IsHoldFloor(float deckUp, IReadOnlyList<float> deckUps) =>
        deckUps.Count > 0 && MathF.Abs(deckUp - deckUps[0]) < 0.08f;
}
