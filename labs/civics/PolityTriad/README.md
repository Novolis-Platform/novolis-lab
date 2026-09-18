# PolityTriad

Spectre dogfood that **composes Economy + Civics + Geopolitics** on a three-polity frontier theatre.

| Polity | Government | Kernels |
|--------|------------|---------|
| **Alpha** | Democracy | Industrial Economy period → Civics via `PeriodContextFromDelivery` + force from capability demand |
| **Beta** | Autocracy | Same Economy→Civics path (different fiscal mix / higher mil share) |
| **Gamma** | Multiparty | Geo `CivicEngine.ApplyMonth` only; Common Market / R&D partner with Alpha |

## Month loop (intricate)

1. **Fiscal agents** tweak household tax / military share intent (`HeuristicFiscalAgent`)
2. **Trade clearing** writes province resource balances / shortages
3. **Economy periods** for α and β (ore→widgets, wages, tax, transfers); wartime ore drain on Alpha
4. **Civics** from observed delivery + geo facts (control, wars, occupation, shortages, research ×)
5. **Force growth** from `ForceCapabilityDemand`
6. **Gamma** geo civic month + `TreatyEffects`
7. **Conflict** fronts when α–β at war (`ConflictResolver.TryResolveFront`)
8. History sample for sparkline arcs

## Run

```powershell
dotnet run --project d:\novolis\novolis-lab\labs\civics\PolityTriad\PolityTriad.csproj -p:NovolisUseProjectReferences=true
dotnet run --project d:\novolis\novolis-lab\labs\civics\PolityTriad\PolityTriad.csproj -p:NovolisUseProjectReferences=true -- --headless 36
```

| Keys | Action |
|------|--------|
| `Space` | Pause / resume |
| `1`–`4` | Speed |
| `W` / `P` | Declare war / offer peace (α–β) |
| `C` / `R` | Sign Alpha–Gamma Common Market / Research Partnership |
| `A` | Toggle fiscal agents |
| `Q` | Quit |

Headless prints an **evidence report**: milestone timeline, Economy peacetime-vs-wartime flows, Civics delivery match + war-fatigue, **population/mobility**, Geopolitics map/captures/trade, and PASS/FAIL cross-layer coupling checks (including tax→emigration).
