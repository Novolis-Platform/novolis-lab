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

static class BatteryEvaluator
{
    public static BatteryAggregates Aggregate(
        ExperimentResult primary,
        ExperimentResult? placebo,
        IReadOnlyList<DosePoint> dose,
        IReadOnlyList<EnsemblePoint> ensemble)
    {
        double? taxAt(double threshold)
        {
            foreach (var p in dose.OrderBy(d => d.Tax))
            {
                if (p.AttPopPct <= threshold)
                    return p.Tax;
            }

            return null;
        }

        var monotonic = true;
        for (var i = 1; i < dose.Count; i++)
        {
            // Higher tax should not produce *less* negative ATT (allow tiny noise)
            if (dose[i].AttPopPct > dose[i - 1].AttPopPct + 0.02)
                monotonic = false;
        }

        var attPcts = ensemble.Select(e => e.AttPopPct).ToList();
        var sameSign = attPcts.Count > 0 &&
                       (attPcts.All(a => a < 0) || attPcts.All(a => a > 0));

        return new BatteryAggregates
        {
            DoseAttAt022 = dose.FirstOrDefault(d => Math.Abs(d.Tax - 0.22) < 1e-9)?.AttPopPct ?? 0,
            DoseAttAt045 = dose.FirstOrDefault(d => Math.Abs(d.Tax - 0.45) < 1e-9)?.AttPopPct ?? 0,
            TaxAtAttMinus5Pct = taxAt(-0.05),
            TaxAtAttMinus20Pct = taxAt(-0.20),
            DoseMonotonic = dose.Count < 2 || monotonic,
            PlaceboDid = placebo?.Effects.DidPopGrowth ?? 0,
            PrimaryDid = primary.Effects.DidPopGrowth,
            EnsembleMeanAttPct = attPcts.Count == 0 ? primary.Effects.AttAlphaPopPct : attPcts.Average(),
            EnsembleMinAttPct = attPcts.Count == 0 ? primary.Effects.AttAlphaPopPct : attPcts.Min(),
            EnsembleMaxAttPct = attPcts.Count == 0 ? primary.Effects.AttAlphaPopPct : attPcts.Max(),
            EnsembleSameSign = sameSign || attPcts.Count == 0,
            PreTrendDid = primary.Effects.PreShockDidGrowth,
            PostShockMeanNetMig = primary.Effects.PostShockMeanNetMig,
            PreShockMeanNetMig = primary.Effects.PreShockMeanNetMig,
        };
    }

    public static List<CouplingCheck> BuildStudyChecks(
        StudySpec study,
        ExperimentResult primary,
        ExperimentResult? placebo,
        BatteryAggregates agg)
    {
        var inv = CultureInfo.InvariantCulture;
        var e = primary.Effects;
        var id = primary.Identification;
        var checks = new List<CouplingCheck>();

        checks.Add(new CouplingCheck(
            id.CounterfactualValid && !id.WarShockOn && !study.AgentsEnabled &&
            id.TaxLockedAtHorizon && id.TwinBalanceGapM1 < 0.08 &&
            study.AlphaTax > study.BetaTax + 0.05,
            "identification",
            $"CF={id.CounterfactualValid}; war={study.WarShockOn}; agents={study.AgentsEnabled}; " +
            $"taxLocked={id.TaxLockedAtHorizon}; balance={id.TwinBalanceGapM1.ToString("0.0%", inv)}; " +
            $"shock M{study.ShockMonth}"));

        checks.Add(new CouplingCheck(
            e.AttAlphaPopPct < -0.02,
            "ATT primary",
            $"ATT pop %={Pct(e.AttAlphaPopPct)}; ATT pop={e.AttAlphaPop.ToString("0", inv)}"));

        var preTrendOk = !e.HasEventStudy || Math.Abs(e.PreShockDidGrowth) < 0.08;
        checks.Add(new CouplingCheck(
            preTrendOk,
            "pre-trend",
            e.HasEventStudy
                ? $"pre-shock DID growth={Pct(e.PreShockDidGrowth)} " +
                  $"(pre net mig={e.PreShockMeanNetMig.ToString("0", inv)}; " +
                  $"post={e.PostShockMeanNetMig.ToString("0", inv)})"
                : "no shock schedule (static treatment) - skipped"));

        var doseOk = !study.IncludeDose ||
                     (agg.DoseMonotonic && agg.DoseAttAt045 <= agg.DoseAttAt022 - 0.02);
        checks.Add(new CouplingCheck(
            doseOk,
            "dose responds",
            study.IncludeDose
                ? $"monotonic={agg.DoseMonotonic}; ATT@0.22={Pct(agg.DoseAttAt022)}; " +
                  $"ATT@0.45={Pct(agg.DoseAttAt045)}; " +
                  $"tax@-5%={FmtTax(agg.TaxAtAttMinus5Pct)}; tax@-20%={FmtTax(agg.TaxAtAttMinus20Pct)}"
                : "dose grid off"));

        var placeboOk = !study.IncludePlacebo ||
                        (Math.Abs(agg.PlaceboDid) < Math.Abs(agg.PrimaryDid) * 0.35 + 0.03);
        checks.Add(new CouplingCheck(
            placeboOk,
            "placebo symmetry",
            study.IncludePlacebo
                ? $"placebo DID={Pct(agg.PlaceboDid)}; primary DID={Pct(agg.PrimaryDid)}"
                : "placebo off"));

        var fiscalOk = e.AttCumTaxRevenue > 0 && e.AttAlphaPop < 0;
        checks.Add(new CouplingCheck(
            fiscalOk,
            "fiscal tradeoff",
            $"ATT cum tax revenue={e.AttCumTaxRevenue.ToString("+0.0;-0.0", inv)}; " +
            $"ATT mean prod={e.AttMeanProduction.ToString("+0.00;-0.00", inv)}; " +
            $"ATT end cash={e.AttEndStateCash.ToString("+0.0;-0.0", inv)}; " +
            $"ATT pop={e.AttAlphaPop.ToString("0", inv)}"));

        var ensOk = !study.IncludeEnsemble || agg.EnsembleSameSign;
        checks.Add(new CouplingCheck(
            ensOk,
            "ensemble sign",
            study.IncludeEnsemble
                ? $"mean ATT%={Pct(agg.EnsembleMeanAttPct)}; " +
                  $"min={Pct(agg.EnsembleMinAttPct)}; " +
                  $"max={Pct(agg.EnsembleMaxAttPct)}; sameSign={agg.EnsembleSameSign}"
                : "ensemble off"));

        return checks;

        static string FmtTax(double? t) =>
            t is null ? "n/a" : t.Value.ToString("0.00", CultureInfo.InvariantCulture);

        static string Pct(double v) =>
            v.ToString("+0.0%;-0.0%;0.0%", CultureInfo.InvariantCulture);
    }
}
