# Kristiansand map sources

## Source of truth

Only the two Norgeskart PDFs in `kristiansand-norgeskart-source-pdfs.json`.

```powershell
dotnet run --file d:\novolis\novolis-lab\labs\simulation\ArtillerySimulator\scripts\build-kristiansand-map.cs -p:NovolisUseProjectReferences=true
```

## Layers (`layers/`)

| File | From |
| --- | --- |
| `elevation.png` | Relief PDF, **images-only** crop (north-up) |
| `shoreline.png` | Water classification of elevation |
| `roads.png` | Roads PDF, images-only (resampled to elevation grid) |
| `placenames.png` + `.json` | Text spans from roads PDF |
| `raw-relief/` / `raw-roads/` | Every embedded PDF image dump |

Also: `kristiansand-layered-composite.png`, `kristiansand-reference.png`, mask bin/png, `kristiansand-map-stack.json`.
