# ArtillerySimulator

Silk-only Kristiansand artillery lab (single executable). Mission loop, NDH terrain, PDF roads, defense scenario.

```powershell
dotnet run --project d:\novolis\novolis-lab\labs\simulation\ArtillerySimulator\ArtillerySimulator.csproj -p:NovolisUseProjectReferences=true
```

## Layout

| Folder | Role |
| --- | --- |
| `Game/` | Host loop (`ArtillerySimulatorGame`) |
| `Game/Hud/` | On-screen wire HUD + stick font |
| `Game/Gun/` | Howitzer model, crew drill, units |
| `Game/Mission/` | Defense scenario, NATO symbols |
| `Game/World/` | Terrain heightfield, atmosphere, FPS |
| `Game/Map/` | Kristiansand georef, masks, roads, elevation |
| `Game/Camera/` | Orbit / freecam |
| `Game/Agent/` | Live agent surface |
| `Game/Render/` | Silk wire batch helper |
| `map/` | Norgeskart + NDH layer assets |
| `scripts/` | Map rebuild file-based apps (`MapPipeline.Support`) |

Shared georef math lives in `Novolis.Math.Geometry.MapRasterBasis`. Sea-connected flood-fill for map scripts lives in `scripts/MapPipeline.Support` (`WaterSeaConnected`). Geographic map packages are **not** `Novolis.Mapping` (that is DTO mapping) — further cartography helpers should graduate to a dedicated geo package when reused outside this lab.

## Map rebuild

```powershell
dotnet run --file d:\novolis\novolis-lab\labs\simulation\ArtillerySimulator\scripts\build-kristiansand-map.cs -p:NovolisUseProjectReferences=true
dotnet run --file d:\novolis\novolis-lab\labs\simulation\ArtillerySimulator\scripts\build-elevation-water-layers.cs
```

Controls: right-drag orbit, middle-drag or Shift+left pan, wheel zoom, `G` gun, `R` fort snap, `V`/`O` map overview, `M` Kommunekart harbor view, `C` free/follow, `Q`/`E` traverse, `W`/`S` elevate (follow cam), Space fire, `1`–`4` charge, `,`/`.` pace.
