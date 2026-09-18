# Economy dogfood

Self-contained scenarios exercising the Novolis economy kernel and ops packages from GitHub Packages (`2026.1.*`). PackageId `Novolis.Economy` is retired — use `Novolis.Economy.Core` + ops packages.

## Apps

| App | Package surface |
|-----|-----------------|
| [EconomyBoard](EconomyBoard/) | Commodity-chain Avalonia board (`Economy.*` kernel) |
| [TrampFreighterPlay](TrampFreighterPlay/) | Spectre tramp freighter (interactive keys) |
| [TrampFreighterSim](TrampFreighterSim/) | Spectre tramp observer (variable speed + autopilot) |
| [NearSolPolity](NearSolPolity/) | Near-Sol polity — Astro catalog → Economy hubs/production/tramp |

## Run

```powershell
dotnet run --project labs/economy/EconomyBoard
dotnet run --project labs/economy/TrampFreighterPlay
dotnet run --project labs/economy/TrampFreighterSim
dotnet run --project labs/economy/NearSolPolity
```

Scenarios are intentionally independent. **NearSolPolity** is the Astro↔Economy bridge at the dogfood layer.

For **Civics + Economy + Geopolitics** composition, see [PolityTriad](../civics/PolityTriad/).

## Related

| Repo | Role |
|------|------|
| [novolis-economy](https://github.com/Novolis-Platform/novolis-economy) | Published economy packages |
| [AstroSmoke](../astro/AstroSmoke/) | Stellar catalog smoke (feeds NearSolPolity) |
| [PolityTriad](../civics/PolityTriad/) | Civics ↔ Economy ↔ Geopolitics triad |
