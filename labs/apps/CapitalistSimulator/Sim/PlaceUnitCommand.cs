namespace CapitalistSimulator.Sim;

internal sealed record PlaceUnitCommand(
    FirmId FirmId,
    UnitKind Kind,
    int X,
    int Y) : PlayerCommand;
