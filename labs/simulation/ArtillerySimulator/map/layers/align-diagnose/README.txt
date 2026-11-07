ArtillerySimulator layer align diagnose
======================================
Water: PDF Norgeskart silhouette (pixel frame)
PDF roads: layers/roads.png (same pixel frame as water)
NVDB: nvdb-roads.bin placed with shared WGS84 equirectangular → meters

Legend
------
01: water + PDF roads (should lock if PDF georef is consistent)
02: water + NVDB (runtime roads)
03: water + PDF(green) + NVDB(red) — look for red/green separation
04: elevation preview + NVDB
05: magenta=overlap, red=NVDB-only, green=PDF-only

PDF↔NVDB primary match (n=3214)
  mean |nvdb-pdf| = 62.0 m
  mean (nvdb - pdf) = (4.6, 1.8) m
  → positive X = NVDB east of PDF; positive Z = NVDB north of PDF

If 01 is tight but 02/03 show red west of green/coast → NVDB placement offset.
If 01 is also loose → water vs PDF roads disagree (unexpected).
If collage locks but Silk still drifts → runtime view / wrong bin loaded.