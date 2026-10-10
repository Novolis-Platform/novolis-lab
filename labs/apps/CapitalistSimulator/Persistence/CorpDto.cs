using System.Text.Json;
using System.Text.Json.Serialization;
using CapitalistSimulator.Sim;

namespace CapitalistSimulator.Persistence;

internal sealed class CorpDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public bool IsPlayer { get; set; }
    public bool IsAi { get; set; }
    public decimal Cash { get; set; }
    public decimal OpeningCash { get; set; }
    public BrandStrategy BrandStrategy { get; set; }
    public decimal SharesOutstanding { get; set; }
    public decimal SharePrice { get; set; }
    public decimal DividendPerShare { get; set; }
    public decimal LastYearProfit { get; set; }
    public double AiAggressiveness { get; set; }
    public bool Retired { get; set; }
    public Dictionary<string, BrandDto> Brands { get; set; } = new();
    public Dictionary<string, double> Tech { get; set; } = new();
    public List<LoanDto> Loans { get; set; } = [];
    public HqDto Hq { get; set; } = new();
    public decimal[] MonthlyPnl { get; set; } = [];
    public int PnlCursor { get; set; }
}
