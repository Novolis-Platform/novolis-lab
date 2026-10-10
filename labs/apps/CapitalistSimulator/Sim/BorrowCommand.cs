namespace CapitalistSimulator.Sim;

internal sealed record BorrowCommand(decimal Amount) : PlayerCommand;
