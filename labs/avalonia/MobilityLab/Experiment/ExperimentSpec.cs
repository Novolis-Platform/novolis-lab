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
