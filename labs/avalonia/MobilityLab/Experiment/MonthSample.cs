namespace MobilityLab.Experiment;

sealed class MonthSample
{
    public required int Month { get; init; }
    public required string Phase { get; init; }
    public required bool AtWar { get; init; }
    public required double TradeDelta { get; init; }
    public required PolityFacts Alpha { get; init; }
    public required PolityFacts Beta { get; init; }
    public required PolityFacts Gamma { get; init; }
}
