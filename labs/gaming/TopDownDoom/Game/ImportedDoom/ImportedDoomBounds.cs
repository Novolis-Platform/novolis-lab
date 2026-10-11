namespace TopDownDoom.Game.ImportedDoom;

internal sealed class ImportedDoomBounds
{
    public short MinX { get; init; }

    public short MinY { get; init; }

    public short MaxX { get; init; }

    public short MaxY { get; init; }

    public int Width => MaxX - MinX;

    public int Height => MaxY - MinY;
}
