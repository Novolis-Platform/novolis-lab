namespace MobilityLab.Experiment;

sealed class ArmResult
{
    public required ArmKind Kind { get; init; }
    public required string Label { get; init; }
    public required ExperimentSpec Spec { get; init; }
    public required TaxMobilityWorld.Model Model { get; init; }
    public required ExperimentResult Result { get; init; }
    public double? DoseTax { get; init; }
}
