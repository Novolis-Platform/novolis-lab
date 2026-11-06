# ArtillerySimulator map tools

File-based C# apps that own Kristiansand map rasters for this lab. Outputs go to `../map/` and generated sources under `../Game/`.

| Script | Role |
| --- | --- |
| `sync-artillery-map-stack.cs` | Two Norgeskart PDFs → relief/roads PNGs + `kristiansand-map-stack.json` |
| `extract-artillery-map.cs` | DEM/OSM + land/sea mask + game C# sources |
| `compose-artillery-map-stack-preview.cs` | QA stack / composite PNGs |
| `import-artillery-norgeskart-pdf.cs` | One-off PDF raster import |
| `analyze-artillery-map-images.cs` | Alignment / image QA report |
| `InspectNorgeskartPdf/` | PDF tile stitch/render library used by sync |

Canonical PDF paths: `../map/kristiansand-norgeskart-source-pdfs.json`.

```powershell
dotnet run --file d:\novolis\novolis-lab\labs\simulation\ArtillerySimulator\tools\sync-artillery-map-stack.cs
dotnet run --file d:\novolis\novolis-lab\labs\simulation\ArtillerySimulator\tools\extract-artillery-map.cs -- --scenario kristiansand
dotnet run --file d:\novolis\novolis-lab\labs\simulation\ArtillerySimulator\tools\compose-artillery-map-stack-preview.cs
```
