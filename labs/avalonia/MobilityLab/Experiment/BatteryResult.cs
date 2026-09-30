namespace MobilityLab.Experiment;

sealed class BatteryResult
{
    public required StudySpec Study { get; init; }
    public required ArmResult Primary { get; init; }
    public required ArmResult? Placebo { get; init; }
    public required IReadOnlyList<DosePoint> DoseCurve { get; init; }
    public required IReadOnlyList<EnsemblePoint> Ensemble { get; init; }
    public required IReadOnlyList<CouplingCheck> StudyChecks { get; init; }
    public required BatteryAggregates Aggregates { get; init; }

    public int PassCount => StudyChecks.Count(c => c.Pass);
    public int CheckCount => StudyChecks.Count;
    public bool AllPass => StudyChecks.Count > 0 && StudyChecks.All(c => c.Pass);
}
