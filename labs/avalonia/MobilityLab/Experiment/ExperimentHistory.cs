namespace MobilityLab.Experiment;

sealed class ExperimentHistory
{
    public List<MonthSample> Months { get; } = [];

    public void Record(MonthSample sample) => Months.Add(sample);
}

sealed class PolityFacts
{
    public double Population { get; init; }
    public double NetMigration { get; init; }
    public double EmigrationPressure { get; init; }
    public double Legitimacy { get; init; }
    public double Approval { get; init; }
    public double HouseholdTaxRate { get; init; }
    public double ProductionValue { get; init; }
    public double ControlRatio { get; init; }
    public double TaxCollected { get; init; }
    public double StateCash { get; init; }
    public double OreStock { get; init; }
}

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

readonly record struct CouplingCheck(bool Pass, string Claim, string Detail);

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

sealed class ExperimentHost
{
    public required TaxMobilityWorld.Model Model { get; init; }
    public required TaxMobilityWorld.Model Counterfactual { get; init; }
    public required Queue<string> Log { get; init; }
    public required ExperimentResult Result { get; init; }

    public static ExperimentHost Run(ExperimentSpec spec)
    {
        var treated = Simulate(spec, "treated");
        var cfSpec = spec with
        {
            AlphaTax = spec.BetaTax,
            WarShockOn = false,
            AgentsEnabled = false,
            BaselineMonths = 0,
            ShockMonth = 0,
        };
        var counterfactual = Simulate(cfSpec, "counterfactual");
        var result = ScientificEvaluator.Evaluate(treated.Model, counterfactual.Model);

        foreach (var line in counterfactual.Log)
            treated.Log.Enqueue($"[CF] {line}");

        return new ExperimentHost
        {
            Model = treated.Model,
            Counterfactual = counterfactual.Model,
            Log = treated.Log,
            Result = result,
        };
    }

    public static ExperimentResult EvaluateAgainstCounterfactual(TaxMobilityWorld.Model treated)
    {
        var monthsDone = Math.Max(1, treated.History.Months.Count);
        var cfSpec = treated.Spec with
        {
            AlphaTax = treated.Spec.BetaTax,
            Months = monthsDone,
            WarShockOn = false,
            AgentsEnabled = false,
            BaselineMonths = 0,
            ShockMonth = 0,
        };
        var cf = Simulate(cfSpec, "counterfactual");
        return ScientificEvaluator.Evaluate(treated, cf.Model);
    }

    public static TaxMobilityWorld.Model CreateFresh(ExperimentSpec spec) =>
        TaxMobilityWorld.Create(spec);

    public static (TaxMobilityWorld.Model Model, Queue<string> Log) Simulate(ExperimentSpec spec, string tag)
    {
        var model = TaxMobilityWorld.Create(spec);
        var log = new Queue<string>();
        log.Enqueue(
            $"{tag}: Alpha tax={spec.AlphaTax:0.00} (sched) Beta={spec.BetaTax:0.00} Gamma={spec.GammaTax:0.00} " +
            $"months={spec.Months} shock={spec.ShockMonth} seed={spec.Seed}");

        for (var i = 0; i < spec.Months; i++)
        {
            TaxMobilityMonth.MaybeApplyWarShock(model, log, i);
            TaxMobilityMonth.Advance(model, log);
        }

        return (model, log);
    }
}
