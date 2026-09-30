namespace MobilityLab.Experiment;

sealed class EnsemblePoint
{
    public required int Seed { get; init; }
    public required double AttPopPct { get; init; }
    public required double DidPopGrowth { get; init; }
    public required double AttMeanPush { get; init; }
}
