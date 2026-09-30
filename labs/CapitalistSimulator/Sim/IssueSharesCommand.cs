namespace CapitalistSimulator.Sim;
internal sealed record IssueSharesCommand(decimal Shares, decimal Price) : PlayerCommand;
