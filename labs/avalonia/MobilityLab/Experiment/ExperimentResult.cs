namespace MobilityLab.Experiment;

sealed class ExperimentResult
{
    public required ExperimentSpec Spec { get; init; }
    public required int SampleCount { get; init; }
    public required double AlphaPopStart { get; init; }
    public required double AlphaPopEnd { get; init; }
    public required double BetaPopStart { get; init; }
    public required double BetaPopEnd { get; init; }
    public required double GammaPopStart { get; init; }
    public required double GammaPopEnd { get; init; }
    public required double AlphaNetMigrationSum { get; init; }
    public required double AlphaPeakPressure { get; init; }
    public required double BetaPeakPressure { get; init; }
    public required double AlphaLegitimacyEnd { get; init; }
    public required double BetaLegitimacyEnd { get; init; }
    public required double PopulationMigrated { get; init; }
    public required EffectSizes Effects { get; init; }
    public required IdentificationDiagnostics Identification { get; init; }
    public required IReadOnlyList<CouplingCheck> Checks { get; init; }

    public int PassCount => Checks.Count(c => c.Pass);
    public int CheckCount => Checks.Count;
    public bool AllPass => Checks.Count > 0 && Checks.All(c => c.Pass);
}
