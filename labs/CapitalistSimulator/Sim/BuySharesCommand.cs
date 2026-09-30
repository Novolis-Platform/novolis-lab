namespace CapitalistSimulator.Sim;

internal sealed record BuySharesCommand(CorpId Issuer, decimal Shares) : PlayerCommand;
