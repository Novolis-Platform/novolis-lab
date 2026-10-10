namespace CapitalistSimulator.Sim;

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
