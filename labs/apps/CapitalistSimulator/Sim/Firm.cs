namespace CapitalistSimulator.Sim;

internal sealed class Firm
{
    public FirmId Id { get; init; } = FirmId.New();
    public CorpId Owner { get; set; }
    public CityId CityId { get; init; }
    public string FirmTypeId { get; init; } = "";
    public FirmKind Kind { get; init; }
    public string Name { get; set; } = "";
    public int TileX { get; set; }
    public int TileY { get; set; }
    public int LayoutW { get; set; } = 4;
    public int LayoutH { get; set; } = 3;
    public RetailFamily? RetailFamily { get; set; }
    public ExtractKind ExtractKind { get; set; }
    public int FactorySize { get; set; } = 1;
    public List<FunctionalUnit> Units { get; } = [];
    public List<(UnitId From, UnitId To)> Links { get; } = [];
    public List<StockLot> Inventory { get; } = [];
    public decimal MonthlyExpense { get; set; }
    public decimal LastMonthProfit { get; set; }
    public bool AutoApplyRd { get; set; } = true;
}
