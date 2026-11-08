namespace CalypsoCad.Drawing;

[Flags]
internal enum DrawingReferenceFlags
{
    None = 0,
    ShipCenterline = 1 << 0,
    StemDatum = 1 << 1,
    SternExtent = 1 << 2,
    StructuralStations = 1 << 3,
    DeckElevations = 1 << 4,
    OmlOutline = 1 << 5,
    ImlOutline = 1 << 6,
    DefaultContext = ShipCenterline | StemDatum | SternExtent | DeckElevations | OmlOutline,
}
