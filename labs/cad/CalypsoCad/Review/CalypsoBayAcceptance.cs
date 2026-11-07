using System.Text.Json;
using Novolis.Cad.Primitives;
using CalypsoCad.Structure;

namespace CalypsoCad.Review;

/// <summary>Bay-scoped gates for piece-by-piece structural bay CAD (not full-ship acceptance).</summary>
internal static class CalypsoBayAcceptance
{
    public readonly record struct Check(string Name, bool Ok, string Detail = "");

    public static IReadOnlyList<Check> Evaluate(CadDocument doc)
    {
        var list = new List<Check>();
        void Add(string name, bool ok, string detail = "") => list.Add(new Check(name, ok, detail));

        var entities = doc.Entities ?? [];
        var hasBay = doc.Properties is not null && doc.Properties.ContainsKey("structuralBay");
        Add("structuralBay document tag", hasBay);

        Add("no space entities", entities.All(e => !string.Equals(e.Kind, "space", StringComparison.OrdinalIgnoreCase)));
        Add("no outfit entities", entities.All(e => e.Name?.StartsWith("outfit-", StringComparison.OrdinalIgnoreCase) != true));
        Add("no C40 cargo", entities.All(e => e.Name?.StartsWith("C40-", StringComparison.OrdinalIgnoreCase) != true));
        Add("no partition walls", entities.All(e =>
            e.Name?.StartsWith("wall-", StringComparison.OrdinalIgnoreCase) != true
            && e.Name?.StartsWith("partition-", StringComparison.OrdinalIgnoreCase) != true));

        var shells = entities.Count(StructuralBayEmitter.IsShellEntity);
        Add("shell panels + stiffeners", shells >= 6, $"shellEntities={shells}");

        var frames = entities.Count(e => e.Name?.StartsWith("frame-", StringComparison.OrdinalIgnoreCase) == true);
        Add("open frame assemblies", frames >= 20, $"frameBars={frames}");

        Add("no floating passage-portal bars",
            entities.Count(e => e.Name?.Contains("-passage-portal-", StringComparison.OrdinalIgnoreCase) == true) == 0);

        var chordGaps = entities.Count(e =>
            e.Name?.Contains("-chord-", StringComparison.OrdinalIgnoreCase) == true);
        Add("deck chords split at passage tracks", chordGaps >= 4, $"chords={chordGaps}");

        var wallLike = entities.Count(e =>
        {
            var n = e.Name ?? "";
            if (!n.StartsWith("frame-", StringComparison.OrdinalIgnoreCase)
                || !n.Contains("-web-solid-", StringComparison.OrdinalIgnoreCase))
                return false;
            if (e.Points is not { Count: >= 2 })
                return false;
            var h = MathF.Abs((float)e.Points[1][1]) * 2f;
            return h >= 6.5f;
        });
        Add("bay frames not wall-like", wallLike == 0, $"tallWebSolids={wallLike}");

        var girders = entities.Count(e => e.Name?.StartsWith("girder-", StringComparison.OrdinalIgnoreCase) == true);
        Add("keel+port+stbd girders", girders >= 3, $"girders={girders}");

        var faceTop = entities.Count(e =>
            e.Name?.StartsWith("deck-sandwich-", StringComparison.OrdinalIgnoreCase) == true
            && e.Name.Contains("-face-top", StringComparison.OrdinalIgnoreCase));
        var ribs = entities.Count(e =>
            e.Name?.Contains("-rib-", StringComparison.OrdinalIgnoreCase) == true);
        Add("DK0 sandwich faces", faceTop >= 1, $"faceTop={faceTop}");
        Add("cellular deck ribs", ribs >= 2, $"ribs={ribs}");

        var orientedRings = entities.Count(e =>
            e.Name?.Contains("-ring-", StringComparison.OrdinalIgnoreCase) == true
            && e.Properties is not null
            && e.Properties.ContainsKey("orientZ"));
        Add("oriented polygonal ring edges", orientedRings >= 6, $"orientedRing={orientedRings}");

        // Geometric continuity: consecutive girder segments along the same assembly meet within 0.05 m.
        var girderEnds = entities
            .Where(e => e.Name?.StartsWith("girder-", StringComparison.OrdinalIgnoreCase) == true
                        && e.Points is { Count: >= 2 })
            .Select(e =>
            {
                var c = e.Points![0];
                var h = e.Points[1];
                var zc = c.Length > 2 ? c[2] : 0f;
                var hz = h.Length > 2 ? MathF.Abs(h[2]) : 0f;
                var asm = e.Properties is not null
                          && e.Properties.TryGetValue("assembly", out var a)
                          && a.ValueKind == JsonValueKind.String
                    ? a.GetString() ?? ""
                    : e.Name ?? "";
                return (Asm: asm, Z0: zc - hz, Z1: zc + hz);
            })
            .GroupBy(g => g.Asm)
            .ToList();
        // Flag only unexpected small gaps between neighbors that should meet (same run).
        // Large separations (midbody vs HOLD) are intentional and ignored.
        var gapFails = 0;
        var checkedPairs = 0;
        foreach (var g in girderEnds)
        {
            var segs = g.OrderBy(s => s.Z0).ToList();
            for (var i = 1; i < segs.Count; i++)
            {
                var gap = segs[i].Z0 - segs[i - 1].Z1;
                var sep = MathF.Abs(gap);
                if (sep > 3.5f)
                    continue; // different hull regions
                checkedPairs++;
                if (sep > 0.05f)
                    gapFails++;
            }
        }

        Add("longitudinal girder continuity (≤50 mm gaps)", checkedPairs == 0 || gapFails == 0,
            $"gapFails={gapFails} checked={checkedPairs}");

        var withJoins = entities.Count(e =>
            e.Properties is not null
            && e.Properties.TryGetValue("joins", out var j)
            && j.ValueKind == JsonValueKind.Array
            && j.GetArrayLength() > 0);
        Add("members with joins metadata", withJoins >= 6, $"withJoins={withJoins}");

        var withRole = entities.Count(e =>
            e.Properties is not null
            && e.Properties.ContainsKey("structuralRole")
            && e.Properties.ContainsKey("assembly"));
        Add("structural metadata on steel", withRole >= 20, $"tagged={withRole}");

        return list;
    }
}
