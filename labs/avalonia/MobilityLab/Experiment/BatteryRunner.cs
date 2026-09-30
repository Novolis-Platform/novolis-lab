using System.Globalization;

namespace MobilityLab.Experiment;

static class BatteryRunner
{
    public static BatteryResult Run(StudySpec study)
    {
        var seed0 = study.Seeds.Count > 0 ? study.Seeds[0] : 42;

        var primarySpec = study.PrimaryArm(seed0);
        var cfSpec = study.CounterfactualArm(seed0);
        var primaryModel = ExperimentHost.Simulate(primarySpec, "primary").Model;
        var cfModel = ExperimentHost.Simulate(cfSpec, "cf").Model;
        var primaryResult = ScientificEvaluator.Evaluate(primaryModel, cfModel);
        var primary = new ArmResult
        {
            Kind = ArmKind.Primary,
            Label = $"primary α={study.AlphaTax:0.00}",
            Spec = primarySpec,
            Model = primaryModel,
            Result = primaryResult,
        };

        ArmResult? placebo = null;
        if (study.IncludePlacebo)
        {
            var hi = ExperimentHost.Simulate(study.PlaceboHighArm(seed0), "placebo-hi").Model;
            var lo = ExperimentHost.Simulate(study.PlaceboLowArm(seed0), "placebo-lo").Model;
            var placeboResult = ScientificEvaluator.Evaluate(hi, lo);
            placebo = new ArmResult
            {
                Kind = ArmKind.PlaceboHigh,
                Label = $"placebo α=β={study.AlphaTax:0.00}",
                Spec = study.PlaceboHighArm(seed0),
                Model = hi,
                Result = placeboResult,
            };
        }

        var doseCurve = new List<DosePoint>();
        if (study.IncludeDose)
        {
            foreach (var tau in study.DoseGrid)
            {
                var dModel = ExperimentHost.Simulate(study.DoseArm(tau, seed0), $"dose-{tau:0.00}").Model;
                var dRes = ScientificEvaluator.Evaluate(dModel, cfModel);
                doseCurve.Add(new DosePoint
                {
                    Tax = tau,
                    AttPopPct = dRes.Effects.AttAlphaPopPct,
                    AttMeanPush = dRes.Effects.AttMeanPush,
                    DidPopGrowth = dRes.Effects.DidPopGrowth,
                    AttTaxRevenue = dRes.Effects.AttCumTaxRevenue,
                    AttMeanProd = dRes.Effects.AttMeanProduction,
                });
            }
        }

        var ensemble = new List<EnsemblePoint>();
        if (study.IncludeEnsemble)
        {
            foreach (var seed in study.Seeds)
            {
                ExperimentResult eRes;
                if (seed == seed0)
                {
                    eRes = primaryResult;
                }
                else
                {
                    var eModel = ExperimentHost.Simulate(study.PrimaryArm(seed), $"ens-{seed}").Model;
                    var eCf = ExperimentHost.Simulate(study.CounterfactualArm(seed), $"ens-cf-{seed}").Model;
                    eRes = ScientificEvaluator.Evaluate(eModel, eCf);
                }

                ensemble.Add(new EnsemblePoint
                {
                    Seed = seed,
                    AttPopPct = eRes.Effects.AttAlphaPopPct,
                    DidPopGrowth = eRes.Effects.DidPopGrowth,
                    AttMeanPush = eRes.Effects.AttMeanPush,
                });
            }
        }

        var aggregates = BatteryEvaluator.Aggregate(primaryResult, placebo?.Result, doseCurve, ensemble);
        var checks = BatteryEvaluator.BuildStudyChecks(study, primaryResult, placebo?.Result, aggregates);

        return new BatteryResult
        {
            Study = study,
            Primary = primary,
            Placebo = placebo,
            DoseCurve = doseCurve,
            Ensemble = ensemble,
            StudyChecks = checks,
            Aggregates = aggregates,
        };
    }
}
