using Novolis.Rendering.Planar;

namespace TopDownDoom.Game.ImportedDoom;

internal sealed class ImportedDoomTextureCatalog
{
    private readonly ImportedDoomPackage _package;
    private readonly Dictionary<string, PlanarTextureId> _loaded = new(StringComparer.OrdinalIgnoreCase);

    public ImportedDoomTextureCatalog(ImportedDoomPackage package)
    {
        _package = package;
    }

    public bool TryLoad(
        PlanarTextureRegistry registry,
        string kind,
        string name,
        out PlanarTextureId texture)
    {
        texture = default;
        if (string.IsNullOrWhiteSpace(name) || name == "-")
        {
            return false;
        }

        var key = $"{kind}:{name}";
        if (_loaded.TryGetValue(key, out texture))
        {
            return true;
        }

        var asset = _package.Manifest.Assets.FirstOrDefault(candidate =>
            string.Equals(candidate.Kind, kind, StringComparison.OrdinalIgnoreCase)
            && string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase));
        if (asset is null)
        {
            return false;
        }

        var path = _package.GetAssetPath(asset);
        if (!File.Exists(path))
        {
            return false;
        }

        try
        {
            texture = PlanarPngLoader.LoadPng(registry, path);
            _loaded[key] = texture;
            return true;
        }
        catch (InvalidDataException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
    }
}
