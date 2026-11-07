# Kristiansand map sources

## Source of truth

| Layer | Authority |
| --- | --- |
| Water silhouette | Norgeskart **relief** PDF → `layers/water-mask.bin` (+ PNG debug twin) |
| Land meters | Kartverket **NHM_DTM_25833** → `ndh-dtm.tif` → `elevation.bin` → `heightfield-1025.bin` |
| Roads | **NVDB** vegnett → `layers/nvdb-roads.bin` (E/R/S/F main, K/P local) |

PNG heatmaps / `roads-debug.png` / legacy PDF `roads.png` are **debug only**. Deleting them must leave the sim playable.

**Output copy:** `ArtillerySimulator.csproj` ships only runtime binaries (`heightfield-1025.bin`, `nvdb-roads.bin`, water masks, `kommunekart-3d-view.json`). NHM TIFFs, align-diagnose, and POC sheets stay in the source tree and are not copied to `bin/`.

## Rebuild

```powershell
dotnet run --file d:\novolis\novolis-lab\labs\simulation\ArtillerySimulator\scripts\build-kristiansand-map.cs -p:NovolisUseProjectReferences=true
```

Roads-only refresh:

```powershell
dotnet run --file d:\novolis\novolis-lab\labs\simulation\ArtillerySimulator\scripts\fetch-nvdb-roads.cs
```

NHM TIFF refresh:

```powershell
dotnet run --file d:\novolis\novolis-lab\labs\simulation\ArtillerySimulator\scripts\fetch-hoydedata-nhm.cs
```

## Layers (`layers/`)

| File | Role |
| --- | --- |
| `nvdb-roads.bin` | **Runtime roads** (polylines + vegkategori) |
| `heightfield-1025.bin` | **Runtime height** (1025²; sea-connected = 0 m) |
| `water-mask.bin` | **Runtime water** byte grid |
| `ndh-dtm.tif` | F32 DTM authority |
| `ndh-dom.tif` / `ndh-topobathy.tif` | QA (optional) |
| `elevation.bin` / `sea-connected.bin` / `water-elevation.bin` | Build intermediates |
| `elevation.png`, `water-*.png`, `roads-debug.png` | Debug previews |
| `roads.png` | Legacy PDF raster (debug) |

Also: `kristiansand-map-stack.json`, KLSM `../kristiansand-land-sea-mask.bin`.
