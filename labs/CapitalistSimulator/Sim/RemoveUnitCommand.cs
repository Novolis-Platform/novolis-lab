namespace CapitalistSimulator.Sim;

internal sealed record RemoveUnitCommand(FirmId FirmId, UnitId UnitId) : PlayerCommand;
