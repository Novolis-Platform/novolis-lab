namespace MobilityLab.Experiment;

/// <summary>Causal / twin contrasts for one treated vs CF pair.</summary>
sealed class EffectSizes
{
    public double AttAlphaPop { get; init; }
    public double AttAlphaPopPct { get; init; }
    public double AttAlphaNetMigration { get; init; }
    public double AttMeanPush { get; init; }
    public double AttEarlyApproval { get; init; }
    public double DidPopGrowth { get; init; }
    public double MeanPushGapVsBeta { get; init; }
    public double EarlyLegitimacyGap { get; init; }
    public double EarlyApprovalGap { get; init; }
    public double EarlyMeanLegitimacyAlpha { get; init; }
    public double EarlyMeanLegitimacyBeta { get; init; }
    public double EarlyMeanApprovalAlpha { get; init; }
    public double EarlyMeanApprovalBeta { get; init; }
    public double GammaAbsorbShare { get; init; }
    public double PushDominanceShare { get; init; }
    public double AlphaPopDelta { get; init; }
    public double BetaPopDelta { get; init; }
    public double GammaPopDelta { get; init; }
    public double CounterfactualAlphaPopEnd { get; init; }
    public double TreatedAlphaPopEnd { get; init; }

    // Economy / fiscal ATT
    public double AttCumTaxRevenue { get; init; }
    public double AttMeanProduction { get; init; }
    public double AttEndStateCash { get; init; }

    // Event study (shock design)
    public double PreShockDidGrowth { get; init; }
    public double PreShockMeanNetMig { get; init; }
    public double PostShockMeanNetMig { get; init; }
    public double PreShockMeanPush { get; init; }
    public double PostShockMeanPush { get; init; }
    public double PreShockMeanApproval { get; init; }
    public double PostShockMeanApproval { get; init; }
    public bool HasEventStudy { get; init; }
}
