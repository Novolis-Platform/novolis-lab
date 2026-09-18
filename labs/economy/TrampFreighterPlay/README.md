# TrampFreighterPlay

Interactive dogfood for economic transport — independent tramp on a starport hub network (space skin, **no** Astro packages).

Consumes published `Novolis.Economy.Core` + ops packages from GitHub Packages (`2026.1.*`). PackageId `Novolis.Economy` is retired. Requires hubs/corridors/`PlanShipment` on the feed.

## Run

```powershell
dotnet run --project labs/economy/TrampFreighterPlay
```

## Controls

| Key | Action |
|-----|--------|
| `1` | Advance 1 hour |
| `D` | Advance 24 hours |
| `J` | Speculative ore job Frontier → Core |
| `R` | Return parts Core → Frontier |
| `X` | Attempt Sparse Rim (expect plan failure) |
| `B` | Procure bunker fuel at Waystation |
| `S` | Post ore retail ask at Core + run sales |
| `Q` | Quit |

## Related

| App | Role |
|-----|------|
| [TrampFreighterSim](TrampFreighterSim/) | Autopilot observer with variable speed |
| [EconomyBoard](EconomyBoard/) | Avalonia commodity-chain board |
| [NearSolPolity](NearSolPolity/) | Astro-backed hub network |
