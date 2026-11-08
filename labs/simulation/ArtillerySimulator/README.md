# K9 DOG — Howitzer defence simulation game

Educational Silk artillery sandbox (single executable). **K9 DOG** is a cheeky lab name for a fictional 155 mm L52-class piece — not a manufacturer product, not an official training system.

Levels are pluggable (`GameLevelCatalog` / `IGameLevel`). Shipped: **Defense of Kristiansand** (`kristiansand.defense`).

## Demo runbook

```powershell
# Desktop play (VSync on while the window is visible)
dotnet run --project d:\novolis\novolis-lab\labs\simulation\ArtillerySimulator\ArtillerySimulator.csproj -p:NovolisUseProjectReferences=true -c Release -- --level kristiansand.defense --seed 42

# Headless exit-code smoke (load → aim → fire → impact)
dotnet run --project d:\novolis\novolis-lab\labs\simulation\ArtillerySimulator\ArtillerySimulator.csproj -p:NovolisUseProjectReferences=true -c Release -- --smoke --seed 42
echo $LASTEXITCODE

# Headless agent HTTP (always attaches surface)
dotnet run --project d:\novolis\novolis-lab\labs\simulation\ArtillerySimulator\ArtillerySimulator.csproj -p:NovolisUseProjectReferences=true -c Release -- --headless --seed 42

# Desktop + agent (opt-in; default desktop does not open the port)
$env:ARTILLERY_ATTACH_AGENT = "1"
dotnet run --project d:\novolis\novolis-lab\labs\simulation\ArtillerySimulator\ArtillerySimulator.csproj -p:NovolisUseProjectReferences=true -c Release -- --seed 42 --attach-agent
```

Splash → ←/→ pick level → load → play. **Esc / P** pause, **L** licenses (scrollable full `NOTICE.txt` + `assets/sfx/LICENSE.txt`).

`--seed` makes PE sampling and convoy RNG reproducible (`SeedHash` on agent snapshots).

If MSBuild dies with `MSB4166`, clear sticky servers and retry:

```powershell
dotnet build-server shutdown
```

## CLI

```powershell
dotnet run --project d:\novolis\novolis-lab\labs\simulation\ArtillerySimulator\ArtillerySimulator.csproj -p:NovolisUseProjectReferences=true -- --help
dotnet run --project d:\novolis\novolis-lab\labs\simulation\ArtillerySimulator\ArtillerySimulator.csproj -p:NovolisUseProjectReferences=true -- --list-levels
dotnet run --project d:\novolis\novolis-lab\labs\simulation\ArtillerySimulator\ArtillerySimulator.csproj -p:NovolisUseProjectReferences=true -- --profile 180 --json d:\novolis\novolis-lab\labs\simulation\ArtillerySimulator\artifacts\profile.json
```

| Flag | Behavior |
| --- | --- |
| `--smoke` | Invisible window; exit `0` on impact, `1` on failure/timeout |
| `--headless` / `--agent` | Invisible + agent HTTP `:18770` |
| `--attach-agent` | Desktop play with agent HTTP (or `ARTILLERY_ATTACH_AGENT=1`) |
| `--profile [n]` | Headless timed frames; optional `--json path` |

## Runtime notes

| Concern | Behavior |
| --- | --- |
| Sim clock | `FixedStepClock` @ 120 Hz — independent of render FPS |
| Present | Visible window uses VSync (`SilkWindow`); sim still wall-capped |
| Session | `BattleSession` owns seed + piece + `DeliverySampler` |
| Mission end | `MissionOutcome` (Open/Held/ObjectiveLost/Fratricide); auto-pause; fire blocked |
| Map payload | Runtime bins only in output (~heightfield, NVDB, water masks) |
| Audio HUD | Bottom bar shows `RADIO OFF` / `SCORE OFF` / `AUDIO OFF` when missing |
| Aim preview | Real TOF + collision sweep; rebuild throttled ~10 Hz |
| Agent | Opt-in on desktop; always on `--headless` |

## Disclaimer (short)

Educational sandbox. Fiction. Not affiliated with any manufacturer, armed force, or government. Ballistic PE / MV / met / warhead radii are open approximations — not firing tables. Kristiansand geography is a fictional defense-exercise map. See `assets/NOTICE.txt`.

## Layout (single assembly, clean rings)

| Folder | Role |
| --- | --- |
| `Host/` | `Program`, DI, CLI, `ArtilleryGameRunner`, smoke driver, profiler |
| `Domain/` | `BattleSession`, clock, `Battery/` (gun, crew, ballistics loop, piece tech) |
| `Application/` | `BattleWorld`, `FrameViewModel`, commands + pump, `BattleFxSink`, ports |
| `Adapters/Silk/` | `FrameDirector`, renderer, shell, input, presentation binder |
| `Adapters/Agent/` | `ArtilleryLiveHost` → `ChannelWriter<BattleCommand>` |
| `Features/Product/` | Title, legal, MenuFlows shell screens |
| `Features/Levels/` | `IGameLevel`, catalog |
| `Features/Defense/` | Mission loop, columns, opening fire plan |
| `Features/Presentation/` | Camera, HUD Skia, particles (drawn via adapters) |
| `Features/Audio/` | SFX, score, voice |
| `Kristiansand/` | Geodesy, terrain, roads, atmosphere |

Physics (`TickPhysics`) and render (`CreateFrameViewModel` → draw) are budget-isolated. Commands use `Novolis.Messaging.Channels` — no MediatR.

## Icons

```powershell
dotnet run --file d:\novolis\novolis-lab\labs\simulation\ArtillerySimulator\scripts\bake-k9-dog-icons.cs
```

Controls: right-drag orbit, middle-drag or Shift+left pan, wheel zoom, `WASD` lay, Space fire, `1`–`4` charge, `F` shell cam, `Esc`/`P` pause, `L` licenses.
