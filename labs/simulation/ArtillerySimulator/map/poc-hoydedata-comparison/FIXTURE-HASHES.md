# Layer inventory freeze (phase 0)

Recorded before hoydedata→render pipeline cutover. SHA256 prefixes are first 16 hex chars.

| File | Bytes | SHA256[:16] |
| --- | ---: | --- |
| heightfield-1025.bin | 2101258 | 58798DC3A5535202 |
| water-mask.bin | 8138168 | F2A7E784422CDFF8 |
| water-mask.png | 175196 | 94911609242B82AE |
| roads.png (legacy PDF) | 842893 | D11D313465AA9E62 |
| elevation.bin | 16276344 | 6482F6DD4839BAFB |
| sea-connected.bin | 8138168 | D0ECA9DA07176053 |
| ndh-dtm.tif | 29365519 | 34EF1F41CEB058CC |

## Acceptance (binaries first)

1. After rebuild: `nvdb-roads.bin` present; segment counts logged (E/R/S/F vs K/P).
2. `heightfield-1025.bin` regenerates; open-sea ≈ 0 m; inland lakes not forced to 0.
3. Runtime loads `nvdb-roads.bin` only (no PNG road decode).
4. Optional debug PNGs may be deleted; sim still playable.
5. POC `COMPARE-sheet.png` remains debug-only under this folder.
