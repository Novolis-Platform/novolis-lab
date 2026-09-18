# EconomyBoard

Avalonia dogfood board for the vertically integrated commodity chain (Raw → Mid → Fin → retail).

Consumes published `Novolis.Economy.Core` + ops packages from GitHub Packages (`2026.1.*`). PackageId `Novolis.Economy` is retired.

## Run

```powershell
dotnet run --project labs/economy/EconomyBoard
```

## UI

| Control | Behavior |
|---------|----------|
| **+1 hour / +24 hours** | Step the deterministic economy kernel |
| **Run / Pause** | Machine-speed playback |
| Live panels | Inventory bars, ledger roles, restock shipments, recent events |

## Related

| App | Role |
|-----|------|
| [TrampFreighterPlay](../economy/TrampFreighterPlay/) | Interactive Spectre tramp freighter |
| [TrampFreighterSim](../economy/TrampFreighterSim/) | Autopilot observer |
| [NearSolPolity](../economy/NearSolPolity/) | Astro catalog → economy hubs |
