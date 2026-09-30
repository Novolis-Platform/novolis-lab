namespace MobilityLab.Experiment;

sealed class IdentificationDiagnostics
{
    public bool WarShockOn { get; init; }
    public bool AgentsEnabled { get; init; }
    public double TreatmentTaxGap { get; init; }
    public bool TaxLockedAtHorizon { get; init; }
    public bool CounterfactualValid { get; init; }
    public double TwinBalanceGapM1 { get; init; }
    public int BurnInMonths { get; init; }
    public int EarlyCivicWindow { get; init; }
    public int PostSampleMonths { get; init; }
    public int ShockMonth { get; init; }
    public bool UsesShock { get; init; }
}
