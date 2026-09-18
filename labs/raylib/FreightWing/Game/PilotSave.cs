using System.Text.Json;

namespace FreightWing.Game;

internal sealed class PilotSave
{
    public int UnlockedThrough { get; set; }
    public int LastScore { get; set; }

    private static string Path =>
        System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Novolis", "FreightWing", "pilot.json");

    public static PilotSave Load()
    {
        try
        {
            if (File.Exists(Path))
                return JsonSerializer.Deserialize<PilotSave>(File.ReadAllText(Path)) ?? new PilotSave();
        }
        catch
        {
            // ignore
        }

        return new PilotSave();
    }

    public void Store()
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
        File.WriteAllText(Path, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }
}
