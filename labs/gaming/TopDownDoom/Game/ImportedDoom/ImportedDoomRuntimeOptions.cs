namespace TopDownDoom.Game.ImportedDoom;

internal sealed record ImportedDoomRuntimeOptions(string PackageRoot, string MapName)
{
    public static ImportedDoomRuntimeOptions FromEnvironment(string contentRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentRoot);

        var mapName = Environment.GetEnvironmentVariable("NOVOLIS_TOPDOWN_DOOM_MAP");
        if (string.IsNullOrWhiteSpace(mapName))
        {
            mapName = "E1M1";
        }

        var packageRoot = Environment.GetEnvironmentVariable("NOVOLIS_TOPDOWN_DOOM_PACKAGE");
        if (string.IsNullOrWhiteSpace(packageRoot))
        {
            packageRoot = Path.Combine(contentRoot, "Assets", "ImportedDoom");
        }
        else if (!Path.IsPathRooted(packageRoot))
        {
            packageRoot = Path.GetFullPath(packageRoot, contentRoot);
        }

        return new ImportedDoomRuntimeOptions(packageRoot, mapName.Trim().ToUpperInvariant());
    }
}
