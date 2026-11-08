namespace CalypsoCad.Model;

/// <summary>Integrity checks for nested assembly ownership.</summary>
internal static class HierarchyValidation
{
    public sealed record Result(
        bool Ok,
        IReadOnlyList<string> OrphanAssemblies,
        IReadOnlyList<string> OrphanParts,
        IReadOnlyList<string> Cycles,
        IReadOnlyList<string> DuplicateOwnership,
        IReadOnlyList<string> DanglingParents,
        IReadOnlyList<string> DanglingConnections,
        IReadOnlyList<string> DuplicateRegistrationConflicts,
        int RootCount,
        int KnuckleAssemblies,
        int BulkheadAssemblies,
        int FrameAssemblies,
        int DeckAssemblies);

    public static Result Validate(ShipPartRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);

        var orphans = new List<string>();
        var cycles = new List<string>();
        var dangling = new List<string>();
        var dupOwn = new List<string>();
        var dangConn = new List<string>();

        var rootId = ShipHierarchyIds.ShipRoot;
        foreach (var asm in registry.Assemblies)
        {
            if (string.Equals(asm.Id.Value, rootId, StringComparison.OrdinalIgnoreCase))
                continue;
            if (asm.ParentAssemblyId is null)
            {
                orphans.Add(asm.Id.Value);
                continue;
            }

            if (!registry.TryGetAssembly(asm.ParentAssemblyId.Value, out _))
                dangling.Add($"{asm.Id.Value}→{asm.ParentAssemblyId.Value.Value}");

            // Cycle walk.
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { asm.Id.Value };
            var cur = asm.ParentAssemblyId;
            while (cur is { } p)
            {
                if (!seen.Add(p.Value))
                {
                    cycles.Add(asm.Id.Value);
                    break;
                }

                if (!registry.TryGetAssembly(p, out var parent))
                    break;
                cur = parent.ParentAssemblyId;
            }
        }

        var partOwners = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in registry.Parts)
        {
            if (!registry.TryGetAssembly(part.Assembly, out _))
                orphans.Add($"part:{part.Id.Value}");
            if (partOwners.TryGetValue(part.Id.Value, out var prior))
                dupOwn.Add($"{part.Id.Value} owned by {prior} and {part.Assembly.Value}");
            else
                partOwners[part.Id.Value] = part.Assembly.Value;
        }

        foreach (var c in registry.Connections)
        {
            if (c.PartA is { } pa && !registry.TryGetPart(pa, out _))
                dangConn.Add($"conn {c.Id} PartA={pa.Value}");
            if (c.PartB is { } pb && !registry.TryGetPart(pb, out _))
                dangConn.Add($"conn {c.Id} PartB={pb.Value}");
            if (c.AssemblyA is { } aa && !registry.TryGetAssembly(aa, out _))
                dangConn.Add($"conn {c.Id} AssemblyA={aa.Value}");
            if (c.AssemblyB is { } ab && !registry.TryGetAssembly(ab, out _))
                dangConn.Add($"conn {c.Id} AssemblyB={ab.Value}");
        }

        var roots = registry.Assemblies.Count(a => a.ParentAssemblyId is null);
        var knuckles = registry.Assemblies.Count(a => a.Kind == CadAssemblyKind.HullKnuckleStringer);
        var bulkheads = registry.Assemblies.Count(a =>
            a.Kind is CadAssemblyKind.SolidBulkhead or CadAssemblyKind.PressureBulkhead or CadAssemblyKind.RingBulkhead
            || a.Id.Value.StartsWith("BH-", StringComparison.OrdinalIgnoreCase));
        var frames = registry.Assemblies.Count(a =>
            a.Kind is CadAssemblyKind.OrdinaryRingFrame or CadAssemblyKind.DeepWebRingFrame
                or CadAssemblyKind.ReinforcedRingFrame
            || a.Id.Value.StartsWith("FRAME-", StringComparison.OrdinalIgnoreCase));
        var decks = registry.Assemblies.Count(a =>
            a.Kind == CadAssemblyKind.DeckBay
            || a.Id.Value.StartsWith("deck-", StringComparison.OrdinalIgnoreCase));

        var orphanAssemblies = orphans.Where(o => !o.StartsWith("part:", StringComparison.Ordinal)).ToList();
        var orphanParts = orphans.Where(o => o.StartsWith("part:", StringComparison.Ordinal)).ToList();

        var ok = orphanAssemblies.Count == 0
                 && orphanParts.Count == 0
                 && cycles.Count == 0
                 && dangling.Count == 0
                 && dupOwn.Count == 0
                 && dangConn.Count == 0
                 && registry.DuplicateRegistrationConflicts.Count == 0
                 && knuckles >= 8
                 && bulkheads >= 6
                 && frames >= 1
                 && decks >= 3
                 && roots == 1;

        return new Result(
            ok,
            orphanAssemblies,
            orphanParts,
            cycles,
            dupOwn,
            dangling,
            dangConn,
            registry.DuplicateRegistrationConflicts.ToList(),
            roots,
            knuckles,
            bulkheads,
            frames,
            decks);
    }
}
