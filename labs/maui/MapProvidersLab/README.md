# Map Providers Lab

Windows MAUI comparison host for the provider-neutral `Novolis.Maui.Map`
raster surface and its projected-scene companion.

## Raster and procedural sources

The terrestrial tabs use `Novolis.Maui.Map.MapView` with one source:

- Kartverket topographic, greyscale, raster, and nautical maps
- OpenStreetMap Standard, OpenTopoMap, and CyclOSM
- Procedural Starfield, a deterministic offline raster source

Each map tab demonstrates opt-in geographic features: POI metadata and
clipboard actions, wheel/pinch/double-tap/keyboard navigation, selectable
overlays, erase requests, drawing tools, under-map shape catalog and
measurements, and tile/resource counters.

The procedural tab remains the proof that a tile-addressed source is not
limited to terrestrial cartography.

## Sol-centered catalog scenes

`Near-Sol 100` and `HYG Local 1901` tabs load their generated, packageable
NDJSON snapshots through `Novolis.IO.Ndjson`; no catalog list is copied into
the lab. They show source provenance, Sol-origin normalization, selected-star
coordinates, and three explicit celestial projections:

- Sol-centered Cartesian
- Equatorial plate
- Polar azimuthal

The scenes use `ProjectedSceneView`, not Web Mercator. They support pan,
pinch, double-tap, Fit stars, point selection, and Windows mouse-wheel zoom.
The cache path includes the catalog assembly version and module identity so a
local regenerated catalog cannot reuse a stale snapshot.

## Manual smoke path

1. Open every raster tab, confirm attribution and initial load, then switch
   rapidly; inactive tabs must stop tile activity.
2. Select a POI, use Ctrl+C and Ctrl+Shift+C, draw every shape kind, select it
   from both map and catalog, then erase it using the list and Delete.
3. Resize to narrow and wide layouts; drawing controls and the shape catalog
   must remain usable.
4. On the procedural tab, verify that no network request is needed.
5. On each celestial tab, switch all projections, Fit stars, wheel zoom,
   pan, select Sol and another star, and verify the shown source and
   Sol-relative coordinates.
6. Disable the network on a raster tab, verify Retry is recoverable, then
   close and reopen the window to confirm no stale input handlers or tile work.

Run from a multi-repository checkout:

```powershell
dotnet run --project d:\novolis\novolis-lab\labs\maui\MapProvidersLab\MapProvidersLab.csproj -p:NovolisUseProjectReferences=true
```

Network sources are cached under the platform cache directory and remain
subject to their provider terms. Procedural and catalog snapshots are local.
