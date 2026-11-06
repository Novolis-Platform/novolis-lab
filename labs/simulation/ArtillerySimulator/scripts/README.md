# ArtillerySimulator map scripts

File-based apps (excluded from the game `.csproj` compile). Prefer this folder over any old `tools/` tree.

## Build map from the two Norgeskart PDFs

Edit `../map/kristiansand-norgeskart-source-pdfs.json`, then:

```powershell
dotnet run --file d:\novolis\novolis-lab\labs\simulation\ArtillerySimulator\scripts\build-kristiansand-map.cs -p:NovolisUseProjectReferences=true
```

Pipeline (`Novolis.WorkflowEngine.Pipeline`):

1. Load PDF paths  
2. Export every embedded PDF image → `../map/layers/raw-*`  
3. Images-only cropped rasters → elevation, shoreline, roads, placenames  
4. Binary layer data  
5. Transparent layered composite  
6. Mesh / game elevation + land-sea mask  

Generic PDF map APIs live in `Novolis.Pdf.Rendering` / `Novolis.Pdf.Rendering.Skia` (`PdfMapPageRasterizer`, `PdfEmbeddedImageExporter`, `PdfPaintFlags.ImagesOnly`).
