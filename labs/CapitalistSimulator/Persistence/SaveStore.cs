using System.Text.Json;
using System.Text.Json.Serialization;
using CapitalistSimulator.Sim;

namespace CapitalistSimulator.Persistence;

internal sealed class SaveStore
{
    private readonly string _root;

    public SaveStore(string? root = null)
    {
        _root = root ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Novolis",
            "CapitalistSimulator",
            "saves");
        Directory.CreateDirectory(_root);
    }

    public string Root => _root;

    public void Save(GameWorld world, string name = "autosave")
    {
        var path = Path.Combine(_root, Sanitize(name) + ".json");
        var dto = SaveMapper.ToDto(world);
        var json = JsonSerializer.Serialize(dto, JsonOptions);
        File.WriteAllText(path, json);
    }

    public GameWorld? Load(string name = "autosave")
    {
        var path = Path.Combine(_root, Sanitize(name) + ".json");
        if (!File.Exists(path)) return null;
        var json = File.ReadAllText(path);
        var dto = JsonSerializer.Deserialize<SaveDto>(json, JsonOptions);
        return dto is null ? null : SaveMapper.FromDto(dto);
    }

    public IReadOnlyList<string> List() =>
        Directory.Exists(_root)
            ? Directory.GetFiles(_root, "*.json").Select(Path.GetFileNameWithoutExtension).Where(n => n is not null).Cast<string>().ToList()
            : [];

    private static string Sanitize(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return string.IsNullOrWhiteSpace(name) ? "autosave" : name.Trim();
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };
}
