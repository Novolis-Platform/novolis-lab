namespace TopDownDoom.Game.ImportedDoom;

internal readonly record struct ImportedDoomLinedef(
    ushort StartVertex,
    ushort EndVertex,
    short Flags,
    short Special,
    short Tag,
    short RightSidedef,
    short LeftSidedef);
