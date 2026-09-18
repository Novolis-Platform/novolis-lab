using System.Globalization;

namespace MobilityLab.Experiment;

/// <summary>
/// Evaluates a treated run against a same-seed counterfactual (Alpha tax = Beta tax).
/// </summary>
static class ScientificEvaluator
{
    public const int BurnInMonths = 6;
    public const int EarlyCivicMonths = 18;
    public const int PostShockWindow = 12;

    public static ExperimentResult Evaluate(
        TaxMobilityWorld.Model treated,
        TaxMobilityWorld.Model counterfactual)
    {
        var tHist = treated.History.Months;
        var cHist = counterfactual.History.Months;
        var inv = CultureInfo.InvariantCulture;

        if (tHist.Count < 2)
        {
            return Bare(treated,
            [
                new CouplingCheck(false, "horizon", "Need >=2 month samples on treated run"),
            ]);
        }

        var t0 = tHist[0];
        var tN = tHist[^1];
        var cN = cHist.Count > 0 ? cHist[^1] : tN;

        var postT = Post(tHist);
        var postC = Post(cHist);
        var earlyT = Early(tHist);

        var alphaGrowthT = RelChange(t0.Alpha.Population, tN.Alpha.Population);
        var betaGrowthT = RelChange(t0.Beta.Population, tN.Beta.Population);
        var didPopGrowth = alphaGrowthT - betaGrowthT;

        var attAlphaPop = tN.Alpha.Population - cN.Alpha.Population;
        var attAlphaPopPct = t0.Alpha.Population > 0 ? attAlphaPop / t0.Alpha.Population : 0;
        var attAlphaNet = tHist.Sum(m => m.Alpha.NetMigration) - cHist.Sum(m => m.Alpha.NetMigration);
        var meanPushT = Mean(postT, m => m.Alpha.EmigrationPressure);
        var meanPushC = Mean(postC, m => m.Alpha.EmigrationPressure);
        var attMeanPush = meanPushT - meanPushC;
        var pressureGap = meanPushT - Mean(postT, m => m.Beta.EmigrationPressure);

        var earlyMeanLA = Mean(earlyT, m => m.Alpha.Legitimacy);
        var earlyMeanLB = Mean(earlyT, m => m.Beta.Legitimacy);
        var earlyMeanAppA = Mean(earlyT, m => m.Alpha.Approval);
        var earlyMeanAppB = Mean(earlyT, m => m.Beta.Approval);

        var attEarlyApp = Mean(Early(tHist), m => m.Alpha.Approval)
                          - Mean(Early(cHist), m => m.Alpha.Approval);

        var deltaAlpha = tN.Alpha.Population - t0.Alpha.Population;
        var deltaGamma = tN.Gamma.Population - t0.Gamma.Population;
        var gammaAbsorbShare = deltaAlpha < -1
            ? Math.Clamp(deltaGamma / -deltaAlpha, -2, 2)
            : 0;

        var monthsPushHigher = postT.Count == 0
            ? 0
            : postT.Count(m => m.Alpha.EmigrationPressure > m.Beta.EmigrationPressure + 1e-9);
        var pushDominance = postT.Count == 0 ? 0 : monthsPushHigher / (double)postT.Count;

        var balanceGap = Math.Abs(t0.Alpha.Population - t0.Beta.Population)
                         / Math.Max(1.0, (t0.Alpha.Population + t0.Beta.Population) / 2.0);

        var expectedEndTax = treated.Spec.EffectiveAlphaTax(Math.Max(0, tHist.Count - 1));
        var taxLocked =
            Math.Abs(tN.Alpha.HouseholdTaxRate - expectedEndTax) < 1e-6 &&
            Math.Abs(tN.Beta.HouseholdTaxRate - treated.Spec.BetaTax) < 1e-6;

        var cfValid = Math.Abs(counterfactual.Spec.AlphaTax - counterfactual.Spec.BetaTax) < 1e-9
                      && counterfactual.Spec.Seed == treated.Spec.Seed
                      && !counterfactual.Spec.WarShockOn
                      && cHist.Count == tHist.Count;

        var attCumTax = tHist.Sum(m => m.Alpha.TaxCollected) - cHist.Sum(m => m.Alpha.TaxCollected);
        var attMeanProd = Mean(tHist, m => m.Alpha.ProductionValue) - Mean(cHist, m => m.Alpha.ProductionValue);
        var attCash = tN.Alpha.StateCash - cN.Alpha.StateCash;

        var ev = BuildEventStudy(treated.Spec, tHist);

        var effects = new EffectSizes
        {
            AttAlphaPop = attAlphaPop,
            AttAlphaPopPct = attAlphaPopPct,
            AttAlphaNetMigration = attAlphaNet,
            AttMeanPush = attMeanPush,
            AttEarlyApproval = attEarlyApp,
            DidPopGrowth = didPopGrowth,
            MeanPushGapVsBeta = pressureGap,
            EarlyLegitimacyGap = earlyMeanLA - earlyMeanLB,
            EarlyApprovalGap = earlyMeanAppA - earlyMeanAppB,
            EarlyMeanLegitimacyAlpha = earlyMeanLA,
            EarlyMeanLegitimacyBeta = earlyMeanLB,
            EarlyMeanApprovalAlpha = earlyMeanAppA,
            EarlyMeanApprovalBeta = earlyMeanAppB,
            GammaAbsorbShare = gammaAbsorbShare,
            PushDominanceShare = pushDominance,
            AlphaPopDelta = deltaAlpha,
            BetaPopDelta = tN.Beta.Population - t0.Beta.Population,
            GammaPopDelta = deltaGamma,
            CounterfactualAlphaPopEnd = cN.Alpha.Population,
            TreatedAlphaPopEnd = tN.Alpha.Population,
            AttCumTaxRevenue = attCumTax,
            AttMeanProduction = attMeanProd,
            AttEndStateCash = attCash,
            PreShockDidGrowth = ev.PreDid,
            PreShockMeanNetMig = ev.PreNet,
            PostShockMeanNetMig = ev.PostNet,
            PreShockMeanPush = ev.PrePush,
            PostShockMeanPush = ev.PostPush,
            PreShockMeanApproval = ev.PreApp,
            PostShockMeanApproval = ev.PostApp,
            HasEventStudy = ev.Has,
        };

        var id = new IdentificationDiagnostics
        {
            WarShockOn = treated.Spec.WarShockOn,
            AgentsEnabled = treated.Spec.AgentsEnabled,
            TreatmentTaxGap = treated.Spec.AlphaTax - treated.Spec.BetaTax,
            TaxLockedAtHorizon = taxLocked,
            CounterfactualValid = cfValid,
            TwinBalanceGapM1 = balanceGap,
            BurnInMonths = BurnInMonths,
            EarlyCivicWindow = EarlyCivicMonths,
            PostSampleMonths = postT.Count,
            ShockMonth = treated.Spec.ShockMonth,
            UsesShock = treated.Spec.UsesShockSchedule,
        };

        var checks = BuildArmChecks(effects, id, treated, inv);

        return new ExperimentResult
        {
            Spec = treated.Spec,
            SampleCount = tHist.Count,
            AlphaPopStart = t0.Alpha.Population,
            AlphaPopEnd = tN.Alpha.Population,
            BetaPopStart = t0.Beta.Population,
            BetaPopEnd = tN.Beta.Population,
            GammaPopStart = t0.Gamma.Population,
            GammaPopEnd = tN.Gamma.Population,
            AlphaNetMigrationSum = tHist.Sum(m => m.Alpha.NetMigration),
            AlphaPeakPressure = tHist.Max(m => m.Alpha.EmigrationPressure),
            BetaPeakPressure = tHist.Max(m => m.Beta.EmigrationPressure),
            AlphaLegitimacyEnd = tN.Alpha.Legitimacy,
            BetaLegitimacyEnd = tN.Beta.Legitimacy,
            PopulationMigrated = treated.Telemetry.PopulationMigrated,
            Effects = effects,
            Identification = id,
            Checks = checks,
        };
    }

    static (bool Has, double PreDid, double PreNet, double PostNet, double PrePush, double PostPush, double PreApp, double PostApp)
        BuildEventStudy(ExperimentSpec spec, IReadOnlyList<MonthSample> months)
    {
        if (!spec.UsesShockSchedule || spec.ShockMonth <= 0)
            return (false, 0, 0, 0, 0, 0, 0, 0);

        var pre = months.Where(m => m.Month <= spec.ShockMonth).ToList();
        var postEnd = spec.ShockMonth + PostShockWindow;
        var post = months.Where(m => m.Month > spec.ShockMonth && m.Month <= postEnd).ToList();
        if (pre.Count < 2 || post.Count < 1)
            return (false, 0, 0, 0, 0, 0, 0, 0);

        var preDid = RelChange(pre[0].Alpha.Population, pre[^1].Alpha.Population)
                     - RelChange(pre[0].Beta.Population, pre[^1].Beta.Population);

        return (
            true,
            preDid,
            Mean(pre, m => m.Alpha.NetMigration),
            Mean(post, m => m.Alpha.NetMigration),
            Mean(pre, m => m.Alpha.EmigrationPressure),
            Mean(post, m => m.Alpha.EmigrationPressure),
            Mean(pre, m => m.Alpha.Approval),
            Mean(post, m => m.Alpha.Approval));
    }

    static List<CouplingCheck> BuildArmChecks(
        EffectSizes e,
        IdentificationDiagnostics id,
        TaxMobilityWorld.Model treated,
        CultureInfo inv)
    {
        var checks = new List<CouplingCheck>();
        var gapOk = id.TreatmentTaxGap > 0.05 || Math.Abs(treated.Spec.AlphaTax - treated.Spec.BetaTax) < 1e-9;

        checks.Add(new CouplingCheck(
            id.CounterfactualValid && !id.WarShockOn && id.TaxLockedAtHorizon && id.TwinBalanceGapM1 < 0.08 && gapOk,
            "identification",
            $"CF valid={id.CounterfactualValid}; war={id.WarShockOn}; taxLocked={id.TaxLockedAtHorizon}; " +
            $"twin balance gap M1={id.TwinBalanceGapM1.ToString("0.0%", inv)}; " +
            $"tax gap={id.TreatmentTaxGap.ToString("0.00", inv)}; shock={id.ShockMonth}"));

        checks.Add(new CouplingCheck(
            e.AttAlphaPopPct < -0.02,
            "ATT Alpha pop < 0",
            $"ATT pop={e.AttAlphaPop.ToString("0", inv)} " +
            $"({e.AttAlphaPopPct.ToString("+0.0%;-0.0%", inv)} of baseline); " +
            $"treated end {e.TreatedAlphaPopEnd.ToString("0", inv)} vs CF {e.CounterfactualAlphaPopEnd.ToString("0", inv)}"));

        checks.Add(new CouplingCheck(
            e.DidPopGrowth < -0.02,
            "DID Alpha vs Beta growth",
            $"DID growth={e.DidPopGrowth.ToString("+0.0%;-0.0%", inv)} " +
            $"(Alpha {e.AlphaPopDelta.ToString("+0;-0", inv)} vs Beta {e.BetaPopDelta.ToString("+0;-0", inv)})"));

        checks.Add(new CouplingCheck(
            e.AttMeanPush > 0.03 && e.MeanPushGapVsBeta > 0.02,
            "ATT + twin pressure",
            $"ATT mean push (post M{BurnInMonths})={e.AttMeanPush.ToString("+0.00;-0.00", inv)}; " +
            $"Alpha-Beta gap={e.MeanPushGapVsBeta.ToString("+0.00;-0.00", inv)}; " +
            $"dominance={e.PushDominanceShare.ToString("0%", inv)}"));

        checks.Add(new CouplingCheck(
            e.GammaPopDelta > 10_000 && e.GammaAbsorbShare > 0.25,
            "Gamma absorbs outflow",
            $"Gamma delta={e.GammaPopDelta.ToString("+0;-0", inv)}; " +
            $"absorb share={e.GammaAbsorbShare.ToString("0.0%", inv)}; " +
            $"telemetry={treated.Telemetry.PopulationMigrated.ToString("0", inv)}"));

        checks.Add(new CouplingCheck(
            e.EarlyApprovalGap < -0.015 || e.AttEarlyApproval < -0.015 ||
            (e.HasEventStudy && e.PostShockMeanApproval < e.PreShockMeanApproval - 0.01),
            "early approval (tax channel)",
            $"early App gap={e.EarlyApprovalGap.ToString("+0.000;-0.000", inv)}; " +
            $"ATT early App={e.AttEarlyApproval.ToString("+0.000;-0.000", inv)}; " +
            $"ATT cum tax={e.AttCumTaxRevenue.ToString("+0.0;-0.0", inv)}; " +
            $"ATT mean prod={e.AttMeanProduction.ToString("+0.00;-0.00", inv)}"));

        return checks;
    }

    static ExperimentResult Bare(TaxMobilityWorld.Model treated, List<CouplingCheck> checks)
    {
        var t = treated.History.Months;
        var first = t.Count > 0 ? t[0] : null;
        var last = t.Count > 0 ? t[^1] : null;
        return new ExperimentResult
        {
            Spec = treated.Spec,
            SampleCount = t.Count,
            AlphaPopStart = first?.Alpha.Population ?? 0,
            AlphaPopEnd = last?.Alpha.Population ?? 0,
            BetaPopStart = first?.Beta.Population ?? 0,
            BetaPopEnd = last?.Beta.Population ?? 0,
            GammaPopStart = first?.Gamma.Population ?? 0,
            GammaPopEnd = last?.Gamma.Population ?? 0,
            AlphaNetMigrationSum = t.Sum(m => m.Alpha.NetMigration),
            AlphaPeakPressure = t.Count == 0 ? 0 : t.Max(m => m.Alpha.EmigrationPressure),
            BetaPeakPressure = t.Count == 0 ? 0 : t.Max(m => m.Beta.EmigrationPressure),
            AlphaLegitimacyEnd = last?.Alpha.Legitimacy ?? 0,
            BetaLegitimacyEnd = last?.Beta.Legitimacy ?? 0,
            PopulationMigrated = treated.Telemetry.PopulationMigrated,
            Effects = new EffectSizes(),
            Identification = new IdentificationDiagnostics
            {
                WarShockOn = treated.Spec.WarShockOn,
                AgentsEnabled = treated.Spec.AgentsEnabled,
                TreatmentTaxGap = treated.Spec.AlphaTax - treated.Spec.BetaTax,
                ShockMonth = treated.Spec.ShockMonth,
                UsesShock = treated.Spec.UsesShockSchedule,
            },
            Checks = checks,
        };
    }

    static List<MonthSample> Post(IReadOnlyList<MonthSample> months) =>
        months.Where(m => m.Month > BurnInMonths).ToList();

    static List<MonthSample> Early(IReadOnlyList<MonthSample> months) =>
        months.Where(m => m.Month <= EarlyCivicMonths).ToList();

    static double Mean(IReadOnlyList<MonthSample> xs, Func<MonthSample, double> f) =>
        xs.Count == 0 ? 0 : xs.Average(f);

    static double RelChange(double start, double end) =>
        start <= 0 ? 0 : (end - start) / start;
}
