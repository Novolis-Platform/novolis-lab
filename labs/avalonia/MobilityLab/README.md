# MobilityLab

Controlled tax–mobility science lab on Civics / Economy / Geopolitics (Avalonia). Wave 1 kernels only — this app is a **design + estimation harness**, not new demography physics.

## Abstract

Holding geography and initial stocks fixed, does raising Alpha’s household tax above Economy tax-push / Civics emigration thresholds cause population outflow, higher pressure, and civic/fiscal tradeoffs vs twin Beta, with Gamma as haven?

Default **science battery**: 48 months, baseline/shock at month 12, α treatment `0.38`, β/γ low tax, dose grid, placebo high twin, seeds `{42,43,44}`.

## Study design (battery)

| Arm | Role |
|-----|------|
| Primary | Shock (or static) treatment tax on Alpha; β/γ low |
| Counterfactual | Alpha tax = Beta tax (ATT baseline) |
| Dose grid | α ∈ {0.22, 0.28, 0.32, 0.38, 0.45} vs CF |
| Placebo high twin | α = β = treatment tax — twin DID should collapse |
| Ensemble | Primary+CF across seeds — same-sign robustness |

**Shock schedule:** months before `ShockMonth` lock Alpha at Beta tax; from shock onward Alpha gets treatment tax. Pre-trend DID should be near zero.

Spatial authority: [demography-coupling.md](d:\novolis\novolis-civics\docs\demography-coupling.md). Kernel Wave 2 (unrest/HD→productivity) is **out of scope** here.

## Estimands

| Estimand | Meaning |
|----------|---------|
| ATT Alpha pop % | Treated − CF end pop / baseline |
| Twin DID growth | Alpha vs Beta relative Δpop in primary |
| ATT mean push | Post-burn-in pressure treated − CF |
| Event study | Pre vs post-shock mean net mig / push / approval |
| Dose curve | ATT pop % and push vs α tax; monotonicity |
| Placebo DID | Should be ≪ primary DID |
| Fiscal ATT | Cumulative tax revenue, mean production, end cash |

## How to reproduce

```powershell
dotnet build d:\novolis\novolis-lab\labs\avalonia\MobilityLab\MobilityLab.csproj -p:NovolisUseProjectReferences=true

dotnet run --project d:\novolis\novolis-lab\labs\avalonia\MobilityLab\MobilityLab.csproj -p:NovolisUseProjectReferences=true

dotnet run --project d:\novolis\novolis-lab\labs\avalonia\MobilityLab\MobilityLab.csproj -p:NovolisUseProjectReferences=true -- --headless 48
```

Headless defaults to the **battery** report. Single-arm:

```powershell
dotnet run --project d:\novolis\novolis-lab\labs\avalonia\MobilityLab\MobilityLab.csproj -p:NovolisUseProjectReferences=true -- --headless 48 --single
```

Static (no shock) single arm:

```powershell
dotnet run --project d:\novolis\novolis-lab\labs\avalonia\MobilityLab\MobilityLab.csproj -p:NovolisUseProjectReferences=true -- --headless 36 --single --static
```

Lab UI: **Battery study** checked by default → Run; **Copy markdown report** for chat paste.

## Study PASS/FAIL

| Check | Pass when |
|-------|-----------|
| identification | CF valid, war/agents off, tax locked, twin balance |
| ATT primary | ATT pop % &lt; −2% |
| pre-trend | \|pre-shock DID\| small (shock design) |
| dose responds | Higher tax → more negative ATT (monotonic) |
| placebo symmetry | \|placebo DID\| ≪ \|primary DID\| |
| fiscal tradeoff | ATT tax revenue &gt; 0 while ATT pop &lt; 0 |
| ensemble sign | All seeds same ATT sign |

Primary scientific claim remains **ATT primary** with war off. Dose/placebo/ensemble are what make the lab explore a policy surface instead of a single smoke test.
