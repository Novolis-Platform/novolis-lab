namespace MobilityLab.Experiment;

sealed class DosePoint
{
    public required double Tax { get; init; }
    public required double AttPopPct { get; init; }
    public required double AttMeanPush { get; init; }
    public required double DidPopGrowth { get; init; }
    public required double AttTaxRevenue { get; init; }
    public required double AttMeanProd { get; init; }
}
