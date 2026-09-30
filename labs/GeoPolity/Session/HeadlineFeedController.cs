using Novolis.Geopolitics.Core;

namespace GeoPolity.Session;

public sealed record Headline(string Tag, string Voice, string Text, int Day);

/// <summary>Event cursor → capped headline queue (same kinds as Spectre UI).</summary>
public sealed class HeadlineFeedController
{
    private static readonly HashSet<GeoEventKind> HeadlineKinds =
    [
        GeoEventKind.WarDeclared,
        GeoEventKind.PeaceSigned,
        GeoEventKind.AllianceFormed,
        GeoEventKind.AllianceBroken,
        GeoEventKind.ProvinceCaptured,
        GeoEventKind.OrgJoined,
        GeoEventKind.OrgLeft,
        GeoEventKind.EmbargoImposed,
        GeoEventKind.BudgetCrisis,
        GeoEventKind.ResourceShortage,
        GeoEventKind.ForceExpansion,
    ];

    private readonly Queue<Headline> _headlines = new();
    private int _eventCursor;

    public int Cap { get; set; } = 7;

    public IReadOnlyList<Headline> Lines => _headlines.ToList();

    public static bool IsHeadline(GeoEvent e) => HeadlineKinds.Contains(e.Kind);

    public static Headline FromEvent(GeoEvent e)
    {
        var (tag, voice) = e.Kind switch
        {
            GeoEventKind.WarDeclared => ("WAR", "war"),
            GeoEventKind.PeaceSigned => ("PEACE", "peace"),
            GeoEventKind.AllianceFormed => ("ALLY", "ally"),
            GeoEventKind.AllianceBroken => ("ALLY", "ally"),
            GeoEventKind.ProvinceCaptured => ("TAKE", "front"),
            GeoEventKind.OrgJoined or GeoEventKind.OrgLeft => ("ORG", "org"),
            GeoEventKind.EmbargoImposed => ("EMB", "trade"),
            GeoEventKind.BudgetCrisis => ("BUD", "civic"),
            GeoEventKind.ResourceShortage => ("RES", "trade"),
            GeoEventKind.ForceExpansion => ("BUILD", "force"),
            _ => ("···", "status"),
        };

        var text = e.Message.Length <= 64 ? e.Message : e.Message[..63] + "…";
        return new Headline(tag, voice, text, e.Day);
    }

    public void SyncFrom(WorldState world)
    {
        for (var i = Math.Max(0, _eventCursor); i < world.Events.Count; i++)
        {
            var e = world.Events[i];
            if (!IsHeadline(e))
            {
                continue;
            }

            _headlines.Enqueue(FromEvent(e));
            while (_headlines.Count > Cap)
            {
                _headlines.Dequeue();
            }
        }

        _eventCursor = world.Events.Count;
    }

    public void Reset()
    {
        _headlines.Clear();
        _eventCursor = 0;
    }
}
