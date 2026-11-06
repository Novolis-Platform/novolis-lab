# ArtillerySimulator

**The Defense of Kristiansand.** A howitzer on Odderøya stops staggered hostile columns while four friendly defenders hold downtown chokepoints. Any defender casualty loses the scenario.

Elevations are a 21×21 grid aligned to the Norgeskart cutout (`map/kristiansand-norgeskart-cutout.json`). **Default regeneration** samples [Kartverket NDH](https://hoydedata.no/) **DTM 1 m** (`NHM_DTM_25833` ImageServer export) inside the cutout’s EPSG:25833 box. Use `--hillshade` for a greyscale relief JPG (approximate heights) or `--open-meteo` for a coarse fallback. For full municipality GeoTIFF exports (1 m DTM), use [Høydedata.no → Export](https://hoydedata.no/LaserInnsyn2/) and clip to municipality 4204 in QGIS/GDAL; a future `--geotiff` path can ingest that master raster. OSM vectors use the same cutout bbox. The wire mesh uses the original heightfield colors and 128 draw cells. Rivers and roads are OpenStreetMap ways for that box, © OpenStreetMap contributors, draped on the mesh. Water areas render as a blue grid overlay. Units use flat NATO-style symbols, not pin posts. Burst radii (200 m / 340 m) are game rules, not weapon effects.

Rendering is **Raylib** in this lab host (draped shaded DEM mesh + OSM linework). **Silk map twin** (full-window meter raster, north up): `d:\novolis\novolis-lab\labs\simulation\ArtilleryMapSilkLab` — `dotnet run --project d:\novolis\novolis-lab\labs\simulation\ArtilleryMapSilkLab\ArtilleryMapSilkLab.csproj -p:NovolisUseProjectReferences=true` (press **R** to rebuild the frame after re-running extract).

Flight uses `Novolis.Physics.Ballistics` with boat-tail drag, ISA at altitude, and Coriolis at ~58.1°N east-up-north. The column, shell, and crew share the 120 Hz clock.

Regenerate map sources:

```powershell
dotnet run --file d:\novolis\novolis-governance\scripts\extract-artillery-map.cs -- --scenario kristiansand
```

Cutout bbox comes from `map/kristiansand-norgeskart-cutout.json`. **Ground truth** (peaks, spawn roads, bridge heights): `map/kristiansand-ground-truth.json`. Regenerate with NDH + anchor IDW + optional hillshade fusion:

```powershell
dotnet run --file d:\novolis\novolis-governance\scripts\extract-artillery-map.cs -- --scenario kristiansand --fuse-hillshade
dotnet run --file d:\novolis\novolis-governance\scripts\analyze-artillery-map-images.cs -- --write-alignment
```

`--hillshade …\kristiansand-hillshade.jpg` replaces NDH with luminance-only DEM; `--fuse-hillshade` keeps NDH and blends your grey relief JPG for coast/nodata fixes. `--open-meteo` is coarse fallback.

Each extract also writes **`map/kristiansand-map-preview.png`** (2D top-down: NDH hill tint, blue water, cyan coast, tan roads). Open that file to verify coastline/fjords before launching the 3D view.

## Run

**Preferred (no compile):** launch the last local build:

```powershell
pwsh -File d:\novolis\novolis-lab\labs\simulation\ArtillerySimulator\Run-ArtillerySimulator.ps1
```

Or run the executable directly:

```powershell
d:\novolis\novolis-lab\artifacts\bin\ArtillerySimulator\debug\ArtillerySimulator.exe
```

Use `dotnet run --project … -p:NovolisUseProjectReferences=true` only when you need a rebuild from `d:\novolis` with ProjectReference mode. A plain `dotnet run` from `C:\Users\…` without that flag (or with .NET SDK 11 preview alone) often fails on MSBuild task-host errors in codegen/analyzer projects.

Startup camera matches **[Kommunekart 3D](https://3d.kommunekart.com/?x=58.038077082078786&y=8.00709090969718&z=18111.017634206884&head=356.5381777796763&pitch=-58.52425910026143&roll=0.006390997054148519)** (`map/kommunekart-3d-view.json`) — same harbor/Skagerrak angle as the municipal 3D map. **M** = that view again, **V** = bbox overview, **G** = gun.

**Map stack (layers + markers)** — `map/kristiansand-map-stack.json` (`novolis.artillery.map-stack/1`):

- **North is up**; pixel origin **north-west**; scenario meters from **south-west**.
- Shared **markers** (`nw`/`ne`/`sw`/`se`): WGS84 corners ↔ pixel corners on every layer (relief, roads, …).
- **Map area** = PDF map tile union (cropped), not the full print page with legend.
- **One scale**: world `eastMeters` × `northMeters` matches relief crop aspect (no letterbox when stack is synced).

Set your two Norgeskart PDF paths in `map/kristiansand-norgeskart-source-pdfs.json`, then:

```powershell
dotnet run --file d:\novolis\novolis-governance\scripts\sync-artillery-map-stack.cs
dotnet run --file d:\novolis\novolis-governance\scripts\extract-artillery-map.cs -- --scenario kristiansand
```

Or pass PDFs on the command line (overrides the config):

```powershell
dotnet run --file d:\novolis\novolis-governance\scripts\sync-artillery-map-stack.cs -- --heightmap-pdf "C:\Users\frank\Downloads\<heightmap>.pdf" --roads-pdf "C:\Users\frank\Downloads\<roads>.pdf"
```

Exports from the PDF are **north-up** (row 0 = north, column 0 = west), matching `pixelOrigin: topLeftNorthWest` in `kristiansand-map-stack.json`. If your tree still has an older upside-down `kristiansand-reference.png`, re-run **sync** then **extract** so pixels and the land/sea mask agree.

Edit marker WGS84 when you have grid labels from the PDF; re-run sync + extract. Optional: `InspectNorgeskartPdf --dump-spans <pdf>` for label positions (font decode still weak).

`kristiansand-map-stack-preview.png` is a **multi-panel QA sheet** (relief, overlay, mask stacked). For one image that matches the chart window, open `kristiansand-reference.png`, or run `compose-artillery-map-stack-preview.cs` to also emit **`kristiansand-relief-roads-composite.png`** (single relief + roads overlay).

**Land/sea** mask is pixel-aligned to the **relief** layer via the stack. QA: `kristiansand-land-sea-mask.png`.

The window listens on `http://127.0.0.1:18770`. Snapshot lists threats, green defenders, Kvadraturen, rivers, and roads. `--agent` runs headless flat terrain without the Kristiansand mission.

## Controls

| Input | Action |
|-------|--------|
| Left drag | Lay the piece |
| Shift | Fine lay |
| Q / E | Traverse |
| W / S | Elevate / depress with follow camera |
| Space | Fire when crew ready |
| 1–4 | Charge zones |
| D | Toggle drag |
| , / . | Pace |
| C | Follow / free camera |
| V | Scenario bbox overview |
| M | Kommunekart 3D camera (harbor) |
| G | Gun / battle camera |
| Wheel, H / M | Zoom |
| F | Flat vacuum check |

For local iteration: `Novolis.Platform.slnx` or `-p:NovolisUseProjectReferences=true`.
