# ArtillerySimulator

Silk-only Kristiansand artillery lab (single executable). Vertical-slice layout: **Host** composes **Features** over the **Kristiansand** geographic kernel.

```powershell
dotnet run --project d:\novolis\novolis-lab\labs\simulation\ArtillerySimulator\ArtillerySimulator.csproj -p:NovolisUseProjectReferences=true
```

Headless timing:

```powershell
dotnet run --project d:\novolis\novolis-lab\labs\simulation\ArtillerySimulator\ArtillerySimulator.csproj -p:NovolisUseProjectReferences=true -c Release -- --profile 180
```

## Layout

| Folder | Role |
| --- | --- |
| `Host/` | Composition root (`ArtilleryApp`), frame profiler |
| `Features/Battery/` | Howitzer, crew drill, charge units |
| `Features/Defense/` | Mission loop, columns, opening fire plan, draw, reports |
| `Features/Presentation/` | Camera, HUD, silk wires / NATO symbols |
| `Features/Agent/` | Live agent surface |
| `Kristiansand/Geodesy/` | Georef, fort, NDH elevation sampler |
| `Kristiansand/Terrain/` | Heightfield world, land/sea, atmosphere |
| `Kristiansand/Roads/` | Primary road graph + PDF road draping |
| `Kristiansand/Cartography/` | Optional OSM leftovers (agent map entities) |
| `map/` | Norgeskart + NDH layer assets |
| `scripts/` | Map rebuild file-based apps (`MapPipeline.Support`) |

Shared georef math: `Novolis.Math.Geometry.MapRasterBasis`. Sea flood-fill for scripts: `scripts/MapPipeline.Support`. Do not use `Novolis.Mapping` for geography (that package is DTO mapping).

## Map rebuild

```powershell
dotnet run --file d:\novolis\novolis-lab\labs\simulation\ArtillerySimulator\scripts\build-kristiansand-map.cs -p:NovolisUseProjectReferences=true
dotnet run --file d:\novolis\novolis-lab\labs\simulation\ArtillerySimulator\scripts\build-elevation-water-layers.cs
```

Controls: right-drag orbit, middle-drag or Shift+left pan, wheel zoom, `G` gun, `R` fort snap, `V`/`O` map overview, `M` Kommunekart harbor view, `C` free/follow, `Q`/`E` traverse, `W`/`S` elevate (follow cam), Space fire, `1`–`4` charge, `,`/`.` pace.
