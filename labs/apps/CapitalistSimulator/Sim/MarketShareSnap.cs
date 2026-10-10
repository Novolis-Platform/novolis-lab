namespace CapitalistSimulator.Sim;

internal sealed class MarketShareSnap
{
    public string ProductId { get; set; } = "";
    public CorpId CorpId { get; set; }
    public decimal UnitsSold { get; set; }
    public decimal Revenue { get; set; }
}
