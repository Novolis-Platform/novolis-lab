# Kristiansand map sources

## Source of truth (Norgeskart PDFs)

Authoritative inputs are **two** Kartverket exports for the **same map window** (same extent, scale, rotation, paper size):

| PDF role | Typical content | Generated PNG |
| --- | --- | --- |
| Relief / land–sea | Hillshade grayscale + open water (light/blue); **no** reliance on OSM for coast | `kristiansand-reference.png` |
| Roads + labels | Same geography with roads, names, symbology | `kristiansand-roads-reference.png` |

**Canonical paths** live in `kristiansand-norgeskart-source-pdfs.json` (`reliefPdf`, `roadsPdf`, optional `dpi`). Edit that file to point at your source-of-truth PDFs, then:

```powershell
dotnet run --file d:\novolis\novolis-governance\scripts\sync-artillery-map-stack.cs
dotnet run --file d:\novolis\novolis-governance\scripts\extract-artillery-map.cs -- --scenario kristiansand
```

CLI still overrides the config:

```powershell
dotnet run --file d:\novolis\novolis-governance\scripts\sync-artillery-map-stack.cs -- --heightmap-pdf "D:\path\to\relief.pdf" --roads-pdf "D:\path\to\roads.pdf"
```

`kristiansand-map-stack.json` records `sourcePdf` (file name) and pixel sizes per layer.

## Alignment

Layers are stack-aligned when both exports share the **same** Norgeskart map frame. If `sync-artillery-map-stack` warns that relief and roads raster sizes differ, the two PDFs used different print windows; re-export so the map area matches, then sync again.

## Derived files

- `kristiansand-land-sea-mask.bin` / `.png` — water mask from relief pixels (`#5182FF` and related sea tones in `extract-artillery-map.cs`).
- `kristiansand-relief-roads-composite.png` — QA single sheet (relief + semi-transparent roads), from `compose-artillery-map-stack-preview.cs`.
- `kristiansand-map-stack-preview.png` — multi-panel QA (not a replacement for the chart PDFs).

North is up; pixel origin is north-west (`topLeftNorthWest` in the stack).
