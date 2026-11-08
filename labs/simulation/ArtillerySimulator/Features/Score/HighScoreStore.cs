using System.Text.Json;

namespace ArtillerySimulator;

/// <summary>Persists best score per level under local app data.</summary>
internal sealed class HighScoreStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly string _path;
    private readonly Dictionary<string, HighScoreRecord> _byLevel = new(StringComparer.OrdinalIgnoreCase);
    private bool _loaded;

    public HighScoreStore(string? path = null)
    {
        _path = path ?? DefaultPath();
    }

    public int BestPoints(string levelId)
    {
        EnsureLoaded();
        return _byLevel.TryGetValue(levelId, out var row) ? row.Points : 0;
    }

    public HighScoreRecord? Best(string levelId)
    {
        EnsureLoaded();
        return _byLevel.TryGetValue(levelId, out var row) ? row : null;
    }

    /// <returns>True when this run became the new high score for the level.</returns>
    public bool TrySubmit(HighScoreRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        EnsureLoaded();
        if (_byLevel.TryGetValue(record.LevelId, out var prior) && prior.Points >= record.Points)
            return false;

        _byLevel[record.LevelId] = record;
        Save();
        return true;
    }

    private void EnsureLoaded()
    {
        if (_loaded)
            return;
        _loaded = true;
        try
        {
            if (!File.Exists(_path))
                return;
            var json = File.ReadAllText(_path);
            var rows = JsonSerializer.Deserialize<List<HighScoreRecord>>(json, JsonOptions);
            if (rows is null)
                return;
            foreach (var row in rows)
            {
                if (string.IsNullOrWhiteSpace(row.LevelId))
                    continue;
                if (!_byLevel.TryGetValue(row.LevelId, out var existing) || row.Points > existing.Points)
                    _byLevel[row.LevelId] = row;
            }
        }
        catch
        {
            // Corrupt file — start empty; next save rewrites.
        }
    }

    private void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            var rows = _byLevel.Values.OrderByDescending(r => r.Points).ToList();
            File.WriteAllText(_path, JsonSerializer.Serialize(rows, JsonOptions));
        }
        catch
        {
            // Disk full / locked — scoring still works in-memory.
        }
    }

    private static string DefaultPath()
    {
        var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(root))
            root = AppContext.BaseDirectory;
        return Path.Combine(root, "Novolis", "ArtillerySimulator", "highscores.json");
    }
}
