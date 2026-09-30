namespace CapitalistSimulator.Sim;

internal sealed class StockLot
{
    public string ProductId { get; set; } = "";
    public decimal Quantity { get; set; }
    public double Quality { get; set; } = 0.5;
    public decimal UnitCost { get; set; }
}
