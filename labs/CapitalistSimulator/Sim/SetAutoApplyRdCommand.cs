namespace CapitalistSimulator.Sim;
internal sealed record SetAutoApplyRdCommand(FirmId FirmId, bool Auto) : PlayerCommand;
