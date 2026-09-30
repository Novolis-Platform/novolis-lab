namespace CapitalistSimulator.Sim;

internal sealed record SetTrainingCommand(FirmId FirmId, UnitId UnitId, double Training) : PlayerCommand;
