namespace ArtillerySimulator;

/// <summary>
/// Registry of levels. Add a map by implementing <see cref="IGameLevel"/> and calling <see cref="Register"/>.
/// </summary>
internal static class GameLevelCatalog
{
    private static readonly List<IGameLevel> Levels = [];
    private static readonly Dictionary<string, IGameLevel> ById = new(StringComparer.OrdinalIgnoreCase);

    static GameLevelCatalog()
    {
        Register(new KristiansandDefenseLevel());
        // Future maps: Register(new SomewhereElseLevel());
    }

    public static IGameLevel Default => Require(KristiansandDefenseLevel.LevelId);

    public static IReadOnlyList<IGameLevel> All => Levels;

    public static IReadOnlyList<IGameLevel> Playable
    {
        get
        {
            var buffer = new List<IGameLevel>(Levels.Count);
            foreach (var level in Levels)
            {
                if (level.IsPlayable)
                    buffer.Add(level);
            }

            return buffer;
        }
    }

    public static int PlayableCount
    {
        get
        {
            var n = 0;
            foreach (var level in Levels)
            {
                if (level.IsPlayable)
                    n++;
            }

            return n;
        }
    }

    public static IGameLevel Require(string id) =>
        ById.TryGetValue(id, out var level)
            ? level
            : throw new KeyNotFoundException($"Unknown game level '{id}'.");

    public static bool TryGet(string id, out IGameLevel level) => ById.TryGetValue(id, out level!);

    public static int PlayableIndex(IGameLevel current)
    {
        var i = 0;
        foreach (var level in Levels)
        {
            if (!level.IsPlayable)
                continue;
            if (string.Equals(level.Id, current.Id, StringComparison.OrdinalIgnoreCase))
                return i;
            i++;
        }

        return 0;
    }

    public static void Register(IGameLevel level)
    {
        ArgumentNullException.ThrowIfNull(level);
        if (string.IsNullOrWhiteSpace(level.Id))
            throw new ArgumentException("Level needs an Id.", nameof(level));

        if (ById.ContainsKey(level.Id))
            Levels.RemoveAll(l => string.Equals(l.Id, level.Id, StringComparison.OrdinalIgnoreCase));

        ById[level.Id] = level;
        Levels.Add(level);
    }

    public static IGameLevel NextPlayable(IGameLevel current, int delta)
    {
        var list = Playable;
        if (list.Count == 0)
            return current;

        var idx = PlayableIndex(current);
        var next = idx + delta;
        next %= list.Count;
        if (next < 0)
            next += list.Count;
        return list[next];
    }
}
