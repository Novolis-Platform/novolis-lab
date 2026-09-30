using System.Text.Json;
using System.Text.Json.Serialization;
using CapitalistSimulator.Sim;

namespace CapitalistSimulator.Persistence;

internal sealed class HqDto
{
    public bool FinanceAutoDividend { get; set; }
    public bool MarketingAutoAds { get; set; }
    public bool ImportPreferInternal { get; set; }
    public bool RdAutoStart { get; set; }
}
