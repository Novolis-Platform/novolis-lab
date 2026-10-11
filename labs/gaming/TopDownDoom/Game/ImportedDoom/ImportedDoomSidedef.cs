namespace TopDownDoom.Game.ImportedDoom;

internal readonly record struct ImportedDoomSidedef(
    short TextureOffset,
    short RowOffset,
    string UpperTexture,
    string MiddleTexture,
    string LowerTexture,
    short Sector);
