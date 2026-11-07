# Hoydedata POC comparison (not imported)

Visual-only fetch from Kartverket ImageServer. **Does not** update
`elevation.bin`, `heightfield-1025.bin`, or the running sim.

## Bbox
WGS84 `7,825739,58,061816` → `8,234261,58,208184` (EPSG:25833 export)
Fetch raster: 1200×813

## Fetched DTM land stats
samples=602000, range=-0,3–264,2 m (mask water excluded)

## Important
Hoydedata NHM ImageServers are **meters only**. LaserInnsyn’s **Samferdsel**
checkbox is a separate Geonorge WMS (`wms.kartdata`: `kd_veger`, `kd_jernbane`,
`kd_ferger`) — fetched live into `poc-samferdsel-wms.png` and overlaid on DTM/DOM.
Panel 1 still shows the old lab `roads.png` (Norgeskart PDF) for contrast.

## Files
| File | Meaning |
| --- | --- |
| `COMPARE-sheet.png` | Side-by-side panels |
| `00-current-lab-stack.png` | Current elevation + water-mask + lab `roads.png` |
| `01-fetched-dtm-land.png` | Live `NHM_DTM_25833` (meters only) |
| `01b-fetched-dtm-plus-roads.png` | DTM + live WMS Samferdsel |
| `02-fetched-dom.png` | Live `NHM_DOM_25833` (meters only) |
| `02b-fetched-dom-plus-roads.png` | DOM + live WMS Samferdsel |
| `03-fetched-topobathy.png` | Live `NHM_DTM_TOPOBATHY_25833` |
| `04-dom-minus-dtm.png` | Surface above bare ground |
| `05-fetched-samferdsel-only.png` | Live WMS overlay alone |
| `poc-samferdsel-wms.png` | Raw WMS GetMap PNG |
| `poc-nhm-*.tif` | Raw float32 GeoTIFF exports |

## Rebuild
```powershell
dotnet run --file d:\novolis\novolis-lab\labs\simulation\ArtillerySimulator\scripts\poc-hoydedata-comparison.cs
```