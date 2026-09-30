namespace CapitalistSimulator.Sim;

internal sealed class HqDepartments
{
    public bool FinanceAutoDividend { get; set; }
    public bool MarketingAutoAds { get; set; }
    public bool ImportPreferInternal { get; set; } = true;
    public bool RdAutoStart { get; set; }
}
