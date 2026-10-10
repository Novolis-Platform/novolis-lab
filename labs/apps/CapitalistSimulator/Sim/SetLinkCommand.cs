namespace CapitalistSimulator.Sim;

internal sealed record SetLinkCommand(FirmId FirmId, UnitId From, UnitId To) : PlayerCommand;
