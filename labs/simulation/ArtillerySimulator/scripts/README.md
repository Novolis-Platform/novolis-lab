# ArtillerySimulator map scripts

File-based apps (excluded from the game `.csproj` compile). Prefer this folder over any old `tools/` tree.

## Fidelity rule

| Data | Authority (runtime / build) | PNG |
| --- | --- | --- |
| Meters | `ndh-dtm.tif` → `elevation.bin` → `heightfield-1025.bin` | debug only |
| Water where | `water-mask.bin` / KLSM | debug twin |
| Roads | **`nvdb-roads.bin`** (NVDB E/R/S/F vs K/P) | `roads-debug.png` optional; never loaded by Silk |

## Shared origo

Water-mask pixels, NVDB polylines, and the heightfield all use the same WGS84 box + PDF meter
extents in `kristiansand-map-stack.json` / `layers/georef.json` (equirectangular lon/lat → SW-origin
meters). NHM is fetched in EPSG:25833 for that bbox; UV sampling stays locked to the PDF frame.

## Full rebuild (PDFs + NHM + NVDB)

Edit `../map/kristiansand-norgeskart-source-pdfs.json` (relief PDF required; roads PDF still used for shared frame crop / legacy debug raster), then:

```powershell
dotnet run --file d:\novolis\novolis-lab\labs\simulation\ArtillerySimulator\scripts\build-kristiansand-map.cs -p:NovolisUseProjectReferences=true
```

Flags: `--skip-dom`, `--skip-topobathy`, `--dtm-only`, `--skip-nvdb`, `--skip-debug-png`, `--write-debug-png`.

## Incremental fetches

```powershell
dotnet run --file d:\novolis\novolis-lab\labs\simulation\ArtillerySimulator\scripts\fetch-hoydedata-nhm.cs
dotnet run --file d:\novolis\novolis-lab\labs\simulation\ArtillerySimulator\scripts\fetch-nvdb-roads.cs -- --write-debug-png
```

## Debug POC comparison (not game inputs)

```powershell
dotnet run --file d:\novolis\novolis-lab\labs\simulation\ArtillerySimulator\scripts\poc-hoydedata-comparison.cs
```

Shared helpers: `MapPipeline.Support/` (`NhmImageServer`, `TiledFloatTiff`, `NvdbVegnett`, `NvdbRoadsBinary`, `WaterSeaConnected`).
