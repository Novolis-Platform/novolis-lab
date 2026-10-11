namespace TopDownDoom.Game.ImportedDoom;

internal sealed class ImportedDoomMap
{
    public string MapName { get; init; } = string.Empty;

    public ImportedDoomBounds Bounds { get; init; } = new();

    public ImportedDoomVertex[] Vertices { get; init; } = [];

    public ImportedDoomLinedef[] Linedefs { get; init; } = [];

    public ImportedDoomSidedef[] Sidedefs { get; init; } = [];

    public ImportedDoomSector[] Sectors { get; init; } = [];

    public ImportedDoomThing[] Things { get; init; } = [];
}
