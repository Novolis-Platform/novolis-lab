# K9 DOG — Howitzer defence simulation game

Educational Silk artillery sandbox (single executable). **K9 DOG** is a cheeky lab name for a fictional 155 mm L52-class piece — not a manufacturer product, not an official training system.

Levels are pluggable (`GameLevelCatalog` / `IGameLevel`). Shipped: **Defense of Kristiansand** (`kristiansand.defense`).

## Run

```powershell
dotnet run --project d:\novolis\novolis-lab\labs\simulation\ArtillerySimulator\ArtillerySimulator.csproj -p:NovolisUseProjectReferences=true
dotnet run --project d:\novolis\novolis-lab\labs\simulation\ArtillerySimulator\ArtillerySimulator.csproj -p:NovolisUseProjectReferences=true -- --help
dotnet run --project d:\novolis\novolis-lab\labs\simulation\ArtillerySimulator\ArtillerySimulator.csproj -p:NovolisUseProjectReferences=true -- --list-levels
dotnet run --project d:\novolis\novolis-lab\labs\simulation\ArtillerySimulator\ArtillerySimulator.csproj -p:NovolisUseProjectReferences=true -- --level kristiansand.defense --seed 42
```

Splash → ←/→ pick level → load → play. **Esc / P** pause, **L** licenses. Full text: `assets/NOTICE.txt`.

`--seed` makes PE sampling and convoy RNG reproducible (`SeedHash` on agent snapshots).

If MSBuild dies with `MSB4166`, clear sticky servers and retry:

```powershell
dotnet build-server shutdown
```

## Headless agent

```powershell
dotnet run --project d:\novolis\novolis-lab\labs\simulation\ArtillerySimulator\ArtillerySimulator.csproj -p:NovolisUseProjectReferences=true -c Release -- --headless --seed 42
```

- Fire returns immediately; impact arrives as `artillery.impact` (correlation id preserved).
- Snapshot fields: `outcome`, `seed`, `piece`, live met, PE residuals.

## Runtime notes

| Concern | Behavior |
| --- | --- |
| Sim clock | `FixedStepClock` @ 120 Hz — independent of render FPS |
| Session | `BattleSession` owns seed + piece + `DeliverySampler` |
| Mission end | `MissionOutcome` (Open/Held/ObjectiveLost/Fratricide); auto-pause; fire blocked |
| Map payload | Runtime bins only in output (~heightfield, NVDB, water masks) |
| Agent | Aim/charge/fire while rounds are airborne; finite numeric validation |

## Disclaimer (short)

Educational sandbox. Fiction. Not affiliated with any manufacturer, armed force, or government. Ballistic PE / MV / met / warhead radii are open approximations — not firing tables. Kristiansand geography is a fictional defense-exercise map. See `assets/NOTICE.txt`.

## Layout

| Folder | Role |
| --- | --- |
| `Host/` | `ArtilleryApp`, `BattleSession`, `FixedStepClock`, profiler |
| `Features/Battery/` | Howitzer, crew drill, mil-tech specs |
| `Features/Product/` | Title, legal copy, `Novolis.Game.MenuFlows` shell screens |
| `Features/Levels/` | `IGameLevel`, catalog, Kristiansand level |
| `Features/Defense/` | Mission loop, outcomes, columns, opening fire plan |
| `Features/Presentation/` | Camera, HUD, splash/pause overlays |
| `Features/Agent/` | Live agent surface |
| `Kristiansand/` | Geodesy, terrain, roads, atmosphere |
| `assets/` | Splash, icons, NOTICE.txt |
| `map/` | Runtime layers + pipeline sources (see `map/README.md`) |
| `scripts/` | Map rebuild + icon bake + agent play |

## Icons

```powershell
dotnet run --file d:\novolis\novolis-lab\labs\simulation\ArtillerySimulator\scripts\bake-k9-dog-icons.cs
```

Controls: right-drag orbit, middle-drag or Shift+left pan, wheel zoom, `WASD` lay, Space fire, `1`–`4` charge, `F` shell cam, `Esc`/`P` pause, `L` licenses.
