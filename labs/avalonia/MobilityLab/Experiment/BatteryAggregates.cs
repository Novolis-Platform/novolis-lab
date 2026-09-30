namespace MobilityLab.Experiment;

sealed class BatteryAggregates
{
    public double DoseAttAt022 { get; init; }
    public double DoseAttAt045 { get; init; }
    public double? TaxAtAttMinus5Pct { get; init; }
    public double? TaxAtAttMinus20Pct { get; init; }
    public bool DoseMonotonic { get; init; }
    public double PlaceboDid { get; init; }
    public double PrimaryDid { get; init; }
    public double EnsembleMeanAttPct { get; init; }
    public double EnsembleMinAttPct { get; init; }
    public double EnsembleMaxAttPct { get; init; }
    public bool EnsembleSameSign { get; init; }
    public double PreTrendDid { get; init; }
    public double PostShockMeanNetMig { get; init; }
    public double PreShockMeanNetMig { get; init; }
}
