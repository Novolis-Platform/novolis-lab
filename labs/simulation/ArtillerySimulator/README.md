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

Splash → ←/→ pick level → load → play. **Esc / P** pause, **L** licenses (scrollable full `NOTICE.txt` + `assets/sfx/LICENSE.txt`).

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

## Layout (single assembly, clean rings)

| Folder | Role |
| --- | --- |
| `Host/` | `Program`, DI modules, CLI, `ArtilleryGameRunner`, shell/profiler |
| `Domain/` | `BattleSession`, `FixedStepClock` |
| `Application/` | `BattleWorld`, `BattleCommand` + pump, `IPlayQuery` / `IBatteryApplier` |
| `Adapters/Silk/` | `FrameDirector` — drains channel, Silk loop |
| `Adapters/Agent/` | `ArtilleryLiveHost` → `ChannelWriter<BattleCommand>` (no MediatR) |
| `Features/Battery/` | Gun, crew, round pool, aim, battle loop, player input |
| `Features/Product/` | Title, legal, MenuFlows shell screens |
| `Features/Levels/` | `IGameLevel`, catalog, Kristiansand level |
| `Features/Defense/` | Mission loop, outcomes, columns, opening fire plan |
| `Features/Presentation/` | Camera, scene renderer, HUD, particles |
| `Features/Audio/` | SFX, score bake, voice chatter |
| `Kristiansand/` | Geodesy, terrain, roads, atmosphere |
| `assets/` / `map/` / `scripts/` | Runtime assets, layers, bake tools |

Commands use `Novolis.Messaging.Channels` (`AddChannel<BattleCommand>`). The 120 Hz battle loop stays synchronous; continuous WASD lay never allocates channel messages.

## Icons

```powershell
dotnet run --file d:\novolis\novolis-lab\labs\simulation\ArtillerySimulator\scripts\bake-k9-dog-icons.cs
```

Controls: right-drag orbit, middle-drag or Shift+left pan, wheel zoom, `WASD` lay, Space fire, `1`–`4` charge, `F` shell cam, `Esc`/`P` pause, `L` licenses.
