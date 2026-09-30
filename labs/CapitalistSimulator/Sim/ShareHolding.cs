namespace CapitalistSimulator.Sim;

internal sealed class ShareHolding
{
    public CorpId Owner { get; set; }
    public CorpId Issuer { get; set; }
    public decimal Shares { get; set; }
}
