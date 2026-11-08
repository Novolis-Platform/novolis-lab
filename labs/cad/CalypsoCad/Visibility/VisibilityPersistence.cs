using System.Text.Json;
using System.Text.Json.Serialization;

namespace CalypsoCad.Visibility;

/// <summary>
/// Persist hierarchy visibility overrides separately from ship geometry.
/// Stable semantic IDs; Inherit is absence of an override entry.
/// </summary>
internal static class VisibilityPersistence
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string DefaultPath(string generatedDirectory) =>
        Path.Combine(generatedDirectory, "calypso.visibility.json");

    public static void Save(HierarchyVisibilityService service, string path)
    {
        var snap = service.CaptureSnapshot();
        var dto = new VisibilityFileDto
        {
            Label = snap.Label,
            Overrides = snap.Overrides
                .Where(kv => kv.Value != LocalVisibility.Inherit)
                .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase),
        };
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(dto, JsonOptions));
    }

    public static bool TryLoad(HierarchyVisibilityService service, string path, out int missingIds)
    {
        missingIds = 0;
        if (!File.Exists(path))
            return false;

        var dto = JsonSerializer.Deserialize<VisibilityFileDto>(File.ReadAllText(path), JsonOptions);
        if (dto?.Overrides is null)
            return false;

        var known = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        // Soft ignore: ApplySnapshot accepts any id; missing nodes simply have no parent walk effect.
        foreach (var id in dto.Overrides.Keys)
            known.Add(id);

        service.ApplySnapshot(
            new VisibilitySnapshot
            {
                Label = dto.Label,
                Overrides = new Dictionary<string, LocalVisibility>(dto.Overrides, StringComparer.OrdinalIgnoreCase),
            },
            recordUndo: false,
            asPreset: dto.Label);

        missingIds = 0;
        _ = known;
        return true;
    }

    private sealed class VisibilityFileDto
    {
        public string? Label { get; set; }
        public Dictionary<string, LocalVisibility> Overrides { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
