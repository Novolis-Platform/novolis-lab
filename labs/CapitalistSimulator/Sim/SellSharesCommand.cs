namespace CapitalistSimulator.Sim;
internal sealed record SellSharesCommand(CorpId Issuer, decimal Shares) : PlayerCommand;
