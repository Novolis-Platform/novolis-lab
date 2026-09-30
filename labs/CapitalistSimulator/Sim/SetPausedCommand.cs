namespace CapitalistSimulator.Sim;
internal sealed record SetPausedCommand(bool Paused) : PlayerCommand;
