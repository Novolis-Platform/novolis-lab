# Map Providers Lab

Windows MAUI comparison host for the shared raster map stack.

Each tab uses `Novolis.Maui.Map.MapView` with one keyless `MapPresets` source:

- Kartverket topographic, greyscale, raster, and nautical maps
- OpenStreetMap Standard
- OpenTopoMap
- CyclOSM

Run from the repository checkout with:

```powershell
dotnet run --project labs/maui/MapProvidersLab/MapProvidersLab.csproj -p:NovolisUseProjectReferences=true
```

Tiles are cached under the platform cache directory. Provider attribution is
shown in each map surface. Network access and each provider's terms apply.
