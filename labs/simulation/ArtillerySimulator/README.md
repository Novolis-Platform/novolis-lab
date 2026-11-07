# K9 DOG — Howitzer defence simulation game

Educational Silk artillery sandbox (single executable). **K9 DOG** is a cheeky lab name for a fictional 155 mm L52-class piece — not a manufacturer product, not an official training system.

Levels are pluggable (`GameLevelCatalog` / `IGameLevel`). Shipped: **Defense of Kristiansand** (`kristiansand.defense`).

```powershell
dotnet run --project d:\novolis\novolis-lab\labs\simulation\ArtillerySimulator\ArtillerySimulator.csproj -p:NovolisUseProjectReferences=true
dotnet run --project d:\novolis\novolis-lab\labs\simulation\ArtillerySimulator\ArtillerySimulator.csproj -p:NovolisUseProjectReferences=true -- --list-levels
dotnet run --project d:\novolis\novolis-lab\labs\simulation\ArtillerySimulator\ArtillerySimulator.csproj -p:NovolisUseProjectReferences=true -- --level kristiansand.defense
```

Splash → ←/→ pick level → load → play. **Esc / P** pause, **L** licenses. Full text: `assets/NOTICE.txt`.

Optional: `--seed <int>` for reproducible PE / convoy RNG (also shown as `SeedHash` on agent snapshots).

If MSBuild dies with `MSB4166` (child node exited prematurely), clear the sticky servers and retry:

```powershell
dotnet build-server shutdown
dotnet run --project d:\novolis\novolis-lab\labs\simulation\ArtillerySimulator\ArtillerySimulator.csproj -p:NovolisUseProjectReferences=true
```

Headless agent (skips splash):

```powershell
dotnet run --project d:\novolis\novolis-lab\labs\simulation\ArtillerySimulator\ArtillerySimulator.csproj -p:NovolisUseProjectReferences=true -c Release -- --headless
```

## Disclaimer (short)

Educational sandbox. Fiction. Not affiliated with any manufacturer, armed force, or government. Ballistic PE / MV / met / warhead radii are open approximations — not firing tables. Kristiansand geography is a fictional defense-exercise map. See `assets/NOTICE.txt`.

## Layout

| Folder | Role |
| --- | --- |
| `Host/` | Composition root (`ArtilleryApp`), frame profiler |
| `Features/Battery/` | Howitzer, crew drill, expandable mil-tech specs |
| `Features/Product/` | Title, legal copy, shell phases |
| `Features/Levels/` | `IGameLevel`, catalog, Kristiansand level |
| `Features/Defense/` | Mission loop, columns, opening fire plan |
| `Features/Presentation/` | Camera, HUD, splash/pause overlays |
| `Features/Agent/` | Live agent surface |
| `Kristiansand/` | Geodesy, terrain, roads, atmosphere |
| `assets/` | Splash, icons, NOTICE.txt |
| `map/` | Norgeskart + NDH layer assets |
| `scripts/` | Map rebuild + icon bake |

## Icons

```powershell
dotnet run --file d:\novolis\novolis-lab\labs\simulation\ArtillerySimulator\scripts\bake-k9-dog-icons.cs
```

Controls: right-drag orbit, middle-drag or Shift+left pan, wheel zoom, `WASD` lay, Space fire, `1`–`4` charge, `F` shell cam, `Esc`/`P` pause, `L` licenses.
