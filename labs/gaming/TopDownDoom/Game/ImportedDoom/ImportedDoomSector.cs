namespace TopDownDoom.Game.ImportedDoom;

internal readonly record struct ImportedDoomSector(
    short FloorHeight,
    short CeilingHeight,
    string FloorFlat,
    string CeilingFlat,
    short LightLevel,
    short Special,
    short Tag);
