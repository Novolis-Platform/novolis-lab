using System.Numerics;

namespace TopDownDoom.Game.ImportedDoom;

internal readonly record struct ImportedDoomWallSegment(
    Vector3 Start,
    Vector3 End,
    int FrontSector,
    int BackSector,
    bool Solid,
    string TextureName,
    int SourceLineIndex);
