namespace MobilityLab.Experiment;

/// <summary>Fixed treatment/control parameters for one world run.</summary>
readonly record struct ExperimentSpec(
    double AlphaTax,
    double BetaTax,
    double GammaTax,
    int Months,
    int Seed,
    bool WarShockOn,
    bool AgentsEnabled,
    int BaselineMonths,
    int ShockMonth)
{
    /// <summary>Static treatment from t0 (legacy single-arm default).</summary>
    public static ExperimentSpec Default { get; } = new(
        AlphaTax: 0.38,
        BetaTax: 0.14,
        GammaTax: 0.12,
        Months: 36,
        Seed: 42,
        WarShockOn: false,
        AgentsEnabled: false,
        BaselineMonths: 0,
        ShockMonth: 0);

    /// <summary>Study default: 12-month baseline then shock to treatment tax.</summary>
    public static ExperimentSpec ShockDefault { get; } = new(
        AlphaTax: 0.38,
        BetaTax: 0.14,
        GammaTax: 0.12,
        Months: 48,
        Seed: 42,
        WarShockOn: false,
        AgentsEnabled: false,
        BaselineMonths: 12,
        ShockMonth: 12);

    /// <summary>
    /// Alpha household tax for 0-based month index (before sample is recorded).
    /// ShockMonth &lt;= 0 → always <see cref="AlphaTax"/>; else baseline at Beta tax until shock.
    /// </summary>
    public double EffectiveAlphaTax(int monthIndex0Based)
    {
        if (ShockMonth <= 0)
            return AlphaTax;
        return monthIndex0Based < ShockMonth ? BetaTax : AlphaTax;
    }

    public bool UsesShockSchedule => ShockMonth > 0;
}

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

enum ArmKind
{
    Primary,
    Counterfactual,
    PlaceboHigh,
    Dose,
    EnsemblePrimary,
}

sealed class ArmResult
{
    public required ArmKind Kind { get; init; }
    public required string Label { get; init; }
    public required ExperimentSpec Spec { get; init; }
    public required TaxMobilityWorld.Model Model { get; init; }
    public required ExperimentResult Result { get; init; }
    public double? DoseTax { get; init; }
}

sealed class DosePoint
{
    public required double Tax { get; init; }
    public required double AttPopPct { get; init; }
    public required double AttMeanPush { get; init; }
    public required double DidPopGrowth { get; init; }
    public required double AttTaxRevenue { get; init; }
    public required double AttMeanProd { get; init; }
}

sealed class EnsemblePoint
{
    public required int Seed { get; init; }
    public required double AttPopPct { get; init; }
    public required double DidPopGrowth { get; init; }
    public required double AttMeanPush { get; init; }
}

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
