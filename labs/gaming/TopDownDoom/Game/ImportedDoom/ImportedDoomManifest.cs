namespace TopDownDoom.Game.ImportedDoom;

internal sealed class ImportedDoomManifest
{
    public string MapName { get; init; } = string.Empty;

    public string ContentProfile { get; init; } = string.Empty;

    public string SourceSha256 { get; init; } = string.Empty;

    public string ToolVersion { get; init; } = string.Empty;

    public ImportedDoomAsset[] Assets { get; init; } = [];
}
