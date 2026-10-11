namespace TopDownDoom.Game.ImportedDoom;

internal sealed class ImportedDoomAsset
{
    public string Kind { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string RelativePath { get; init; } = string.Empty;

    public int Width { get; init; }

    public int Height { get; init; }

    public short OffsetX { get; init; }

    public short OffsetY { get; init; }
}
