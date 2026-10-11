using System.Text.Json;

namespace TopDownDoom.Game.ImportedDoom;

internal sealed class ImportedDoomPackage
{
    private ImportedDoomPackage(string rootDirectory, ImportedDoomMap map, ImportedDoomManifest manifest)
    {
        RootDirectory = rootDirectory;
        Map = map;
        Manifest = manifest;
    }

    public string RootDirectory { get; }

    public ImportedDoomMap Map { get; }

    public ImportedDoomManifest Manifest { get; }

    public static bool TryLoad(string rootDirectory, string mapName, out ImportedDoomPackage? package)
    {
        package = null;
        if (string.IsNullOrWhiteSpace(rootDirectory)
            || !Directory.Exists(rootDirectory)
            || !File.Exists(Path.Combine(rootDirectory, "level.json"))
            || !File.Exists(Path.Combine(rootDirectory, "manifest.json")))
        {
            return false;
        }

        try
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var map = JsonSerializer.Deserialize<ImportedDoomMap>(
                File.ReadAllText(Path.Combine(rootDirectory, "level.json")),
                options);
            var manifest = JsonSerializer.Deserialize<ImportedDoomManifest>(
                File.ReadAllText(Path.Combine(rootDirectory, "manifest.json")),
                options);
            if (map is null || manifest is null || !string.Equals(map.MapName, mapName, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            package = new ImportedDoomPackage(Path.GetFullPath(rootDirectory), map, manifest);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
    }

    public string GetAssetPath(ImportedDoomAsset asset) =>
        Path.Combine(RootDirectory, asset.RelativePath.Replace('/', Path.DirectorySeparatorChar));
}
