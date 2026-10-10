namespace CapitalistSimulator.Sim;
internal sealed record SetDividendCommand(decimal PerShare) : PlayerCommand;
