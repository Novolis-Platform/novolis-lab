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

        var decks = entities.Count(StructuralBayEmitter.IsDeck);
        Add("DK0 sandwich faces", decks >= 2, $"deckSolids={decks}");

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
