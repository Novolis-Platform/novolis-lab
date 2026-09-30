namespace CapitalistSimulator.Sim;

internal sealed class StockLot
{
    public string ProductId { get; set; } = "";
    public decimal Quantity { get; set; }
    public double Quality { get; set; } = 0.5;
    public decimal UnitCost { get; set; }
}

internal sealed class FunctionalUnit
{
    public UnitId Id { get; init; } = UnitId.New();
    public UnitKind Kind { get; init; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Level { get; set; } = 1;
    public double Training { get; set; } = 0.3;
    public bool PrivateLabel { get; set; }

    // Purchasing
    public string? PurchaseProductId { get; set; }
    public decimal PurchaseQtyTarget { get; set; }
    public bool PurchaseFromSeaport { get; set; } = true;
    public FirmId? PurchaseFromFirm { get; set; }

    // Manufacturing
    public string? RecipeOutputId { get; set; }
    public decimal ProductionRate { get; set; } = 10;

    // Sales / retail slot
    public string? SalesProductId { get; set; }
    public decimal SalesPrice { get; set; }
    public decimal LastSold { get; set; }
    public decimal LastUnmetDemand { get; set; }

    // Advertising
    public string? AdProductId { get; set; }
    public string? AdClass { get; set; }
    public decimal AdBudget { get; set; }

    // Extract
    public ExtractKind ExtractKind { get; set; }
    public string? ExtractProductId { get; set; }
    public decimal ExtractYield { get; set; } = 40;

    // R&D
    public string? RdTargetProductId { get; set; }
    public int RdMonthsRemaining { get; set; }
    public double RdProgress { get; set; }
}

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

internal sealed class CityTile
{
    public TileKind Kind { get; set; } = TileKind.Buildable;
    public FirmId? FirmId { get; set; }
    public decimal LandCost { get; set; } = 5000;
}

internal sealed class City
{
    public CityId Id { get; init; } = CityId.New();
    public string Name { get; set; } = "";
    public int Width { get; set; } = 16;
    public int Height { get; set; } = 12;
    public CityTile[,] Tiles { get; set; } = new CityTile[16, 12];
    public double SpendingLevel { get; set; } = 1.0;
    public double SalaryLevel { get; set; } = 1.0;
    public EconomicClimate Climate { get; set; } = EconomicClimate.Stable;
    public decimal Population { get; set; } = 100_000;
}

internal sealed class ShareHolding
{
    public CorpId Owner { get; set; }
    public CorpId Issuer { get; set; }
    public decimal Shares { get; set; }
}

internal sealed class Loan
{
    public CorpId Borrower { get; set; }
    public decimal Principal { get; set; }
    public decimal MonthlyRate { get; set; } = 0.01m;
}

internal sealed class BrandState
{
    public double Awareness { get; set; }
    public double Loyalty { get; set; }
}

internal sealed class CorpTech
{
    public Dictionary<string, double> ProductTech { get; } = new(StringComparer.OrdinalIgnoreCase);
}

internal sealed class Corporation
{
    public CorpId Id { get; init; } = CorpId.New();
    public string Name { get; set; } = "";
    public bool IsPlayer { get; set; }
    public bool IsAi { get; set; }
    public decimal Cash { get; set; }
    public decimal OpeningCash { get; set; }
    public BrandStrategy BrandStrategy { get; set; } = BrandStrategy.Corporate;
    public Dictionary<string, BrandState> Brands { get; } = new(StringComparer.OrdinalIgnoreCase);
    public CorpTech Tech { get; } = new();
    public decimal SharesOutstanding { get; set; } = 1_000_000;
    public decimal SharePrice { get; set; } = 10;
    public decimal DividendPerShare { get; set; }
    public List<Loan> Loans { get; } = [];
    public decimal LastYearProfit { get; set; }
    /// <summary>Sum of the last up-to-12 recorded monthly P&amp;L entries (true trailing-year profit).</summary>
    public decimal TrailingYearProfit { get; set; }
    public decimal MonthRevenue { get; set; }
    public decimal MonthExpense { get; set; }
    public decimal[] MonthlyPnl { get; } = new decimal[12];
    public int PnlCursor { get; set; }
    public int MonthsRecorded { get; set; }
    public double AiAggressiveness { get; set; } = 0.5;
    public HqDepartments Hq { get; } = new();
    public bool Retired { get; set; }
}

internal sealed class HqDepartments
{
    public bool FinanceAutoDividend { get; set; }
    public bool MarketingAutoAds { get; set; }
    public bool ImportPreferInternal { get; set; } = true;
    public bool RdAutoStart { get; set; }
}

internal sealed class MarketShareSnap
{
    public string ProductId { get; set; } = "";
    public CorpId CorpId { get; set; }
    public decimal UnitsSold { get; set; }
    public decimal Revenue { get; set; }
}

internal sealed class NewsItem
{
    public int Day { get; set; }
    public string Text { get; set; } = "";
}

internal sealed class WinState
{
    public bool Won { get; set; }
    public bool Lost { get; set; }
    public string Message { get; set; } = "";
}
