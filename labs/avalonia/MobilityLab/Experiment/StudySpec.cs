namespace MobilityLab.Experiment;

/// <summary>Full science-battery study configuration.</summary>
readonly record struct StudySpec(
    int Months,
    double AlphaTax,
    double BetaTax,
    double GammaTax,
    int BaselineMonths,
    int ShockMonth,
    bool WarShockOn,
    bool AgentsEnabled,
    bool IncludeDose,
    bool IncludePlacebo,
    bool IncludeEnsemble,
    IReadOnlyList<int> Seeds,
    IReadOnlyList<double> DoseGrid)
{
    public static StudySpec Default { get; } = new(
        Months: 48,
        AlphaTax: 0.38,
        BetaTax: 0.14,
        GammaTax: 0.12,
        BaselineMonths: 12,
        ShockMonth: 12,
        WarShockOn: false,
        AgentsEnabled: false,
        IncludeDose: true,
        IncludePlacebo: true,
        IncludeEnsemble: true,
        Seeds: [42, 43, 44],
        DoseGrid: [0.22, 0.28, 0.32, 0.38, 0.45]);

    public ExperimentSpec PrimaryArm(int seed) => new(
        AlphaTax, BetaTax, GammaTax, Months, seed, WarShockOn, AgentsEnabled,
        BaselineMonths, ShockMonth);

    public ExperimentSpec CounterfactualArm(int seed) => new(
        AlphaTax: BetaTax,
        BetaTax: BetaTax,
        GammaTax: GammaTax,
        Months: Months,
        Seed: seed,
        WarShockOn: false,
        AgentsEnabled: false,
        BaselineMonths: 0,
        ShockMonth: 0);

    public ExperimentSpec PlaceboHighArm(int seed) => new(
        AlphaTax: AlphaTax,
        BetaTax: AlphaTax,
        GammaTax: GammaTax,
        Months: Months,
        Seed: seed,
        WarShockOn: false,
        AgentsEnabled: false,
        BaselineMonths: 0,
        ShockMonth: 0);

    public ExperimentSpec PlaceboLowArm(int seed) => CounterfactualArm(seed);

    public ExperimentSpec DoseArm(double tau, int seed) => new(
        AlphaTax: tau,
        BetaTax: BetaTax,
        GammaTax: GammaTax,
        Months: Months,
        Seed: seed,
        WarShockOn: false,
        AgentsEnabled: false,
        BaselineMonths: BaselineMonths,
        ShockMonth: ShockMonth);
}
