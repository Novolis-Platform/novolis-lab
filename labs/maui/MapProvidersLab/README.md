# Map Providers Lab

Windows MAUI smoke host for the shared `Novolis.Maui.Map` control and every
keyless `Novolis.IO.Maps` raster preset.

Manual smoke path:

1. Open each provider tab and confirm attribution and the initial map load.
2. Switch tabs rapidly; only the active tab should request tiles.
3. Drag, wheel-zoom, double-tap, resize the window, and repeat in narrow
   portrait-like and wide landscape-like layouts.
4. Use Retry after disabling network access and confirm the error state is
   recoverable.
5. Close and reopen the window; confirm no stale native input handler or tile
   activity remains.

Automated coverage stays provider-neutral and offline. Live provider access is
limited to this manual smoke host.
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
