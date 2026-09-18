# TrampFreighterSim

Observational tramp freighter circuit — **job evaluation + variable speed**.

Self-contained. Consumes `Novolis.Economy.Core` + ops packages from GitHub Packages (`2026.1.*`). PackageId `Novolis.Economy` is retired.

## Run

```powershell
dotnet run --project novolis-lab/labs/economy/TrampFreighterSim
```

## Controls

| Key | Speed / action |
|-----|----------------|
| `Space` | Pause / resume |
| `1` … `6` | ½× → Warp |
| `Q` | Quit |

## Autopilot behavior

Quotes Ore F→C and Parts C→F (rev − cog − fuel − toll − crew), accepts only if Δ ≥ min margin, sells delivered cargo before the next haul, and rejects Sparse Rim as tank-infeasible instead of flying it for drama.

## Related

| App | Role |
|-----|------|
| [TrampFreighterPlay](TrampFreighterPlay/) | Interactive keyboard tramp |
| [EconomyBoard](EconomyBoard/) | Avalonia kernel visualization |
