namespace MobilityLab.Experiment;

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
