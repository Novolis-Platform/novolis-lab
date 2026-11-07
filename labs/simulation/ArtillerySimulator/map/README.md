# Kristiansand map sources

## Source of truth

Only the two Norgeskart PDFs in `kristiansand-norgeskart-source-pdfs.json`.

```powershell
dotnet run --file d:\novolis\novolis-lab\labs\simulation\ArtillerySimulator\scripts\build-kristiansand-map.cs -p:NovolisUseProjectReferences=true
```

## Layers (`layers/`)

| File | From |
| --- | --- |
| `elevation.png` | NDH land height preview (water **transparent**) |
| `water-mask.png` | Binary water silhouette (flat cyan) — **authoritative where-water** |
| `water-heightmap.png` | Water elevation heightmap (sea-connected = 0 m; inland lakes keep NDH) |
| `water-combined.png` | Height shades **only** where `water-mask` says water (mask wins conflicts) |
| `sea-connected.bin` | 1 = water flood-filled from map border (open sea / fjords) |
| `water-elevation.bin` | int16 meters on water cells (`-32768` on land; sea flattened) |
| `elevation.bin` | Raw NDH int16 grid (land + under-water; not sea-flattened) |
| `heightfield-1025.bin` | Game mesh meters (1025²; sea-connected water = 0) |
| `shoreline.png` | Water classification of elevation |
| `roads.png` | Roads PDF, images-only (resampled to elevation grid) |
| `placenames.png` + `.json` | Text spans from roads PDF |
| `raw-relief/` / `raw-roads/` | Every embedded PDF image dump |

Rebuild previews from existing bins (no NDH re-fetch):

```powershell
dotnet run --file d:\novolis\novolis-lab\labs\simulation\ArtillerySimulator\scripts\build-elevation-water-layers.cs
```

Also: `kristiansand-layered-composite.png`, `kristiansand-reference.png`, mask bin/png, `kristiansand-map-stack.json`.
