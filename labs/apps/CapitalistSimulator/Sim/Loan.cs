namespace CapitalistSimulator.Sim;

internal sealed class Loan
{
    public CorpId Borrower { get; set; }
    public decimal Principal { get; set; }
    public decimal MonthlyRate { get; set; } = 0.01m;
}
