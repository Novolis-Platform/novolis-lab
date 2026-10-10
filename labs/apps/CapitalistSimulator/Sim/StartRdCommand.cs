namespace CapitalistSimulator.Sim;

internal sealed record StartRdCommand(FirmId FirmId, UnitId UnitId, string ProductId, int Months) : PlayerCommand;
