namespace CapitalistSimulator.Sim;
internal sealed record CancelRdCommand(FirmId FirmId, UnitId UnitId) : PlayerCommand;
