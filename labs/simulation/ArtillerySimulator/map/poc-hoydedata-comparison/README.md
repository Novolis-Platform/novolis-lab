# Hoydedata POC comparison (not imported)

Visual-only fetch from Kartverket ImageServer. **Does not** update
`elevation.bin`, `heightfield-1025.bin`, or the running sim.

## Bbox
WGS84 `7,825739,58,061816` → `8,234261,58,208184` (EPSG:25833 export)
Fetch raster: 1200×813

## Fetched DTM land stats
samples=602000, range=-0,3–264,2 m (mask water excluded)

## Files
| File | Meaning |
| --- | --- |
| `COMPARE-sheet.png` | Side-by-side panels |
| `00-current-lab-stack.png` | Current elevation + water-mask + roads |
| `01-fetched-dtm-land.png` | Live `NHM_DTM_25833` |
| `02-fetched-dom.png` | Live `NHM_DOM_25833` |
| `03-fetched-topobathy.png` | Live `NHM_DTM_TOPOBATHY_25833` |
| `04-dom-minus-dtm.png` | Surface above bare ground |
| `poc-nhm-*.tif` | Raw float32 GeoTIFF exports |

## Rebuild
```powershell
dotnet run --file d:\novolis\novolis-lab\labs\simulation\ArtillerySimulator\scripts\poc-hoydedata-comparison.cs
```