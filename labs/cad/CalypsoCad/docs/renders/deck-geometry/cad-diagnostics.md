# Deck CAD diagnostics

## Per-deck component counts

| Deck | Face tops | Face bots | Rib long | Rib ath | Peri | Other |
| --- | --- | --- | --- | --- | --- | --- |
| `0` | 18 | 18 | 193 | 42 | 72 | 0 |
| `1` | 18 | 18 | 193 | 42 | 72 | 0 |
| `m1` | 17 | 17 | 235 | 42 | 68 | 0 |

## Overlap / duplication

| Duplicate names | Coplanar face overlaps | Ribs past peri (est.) | Placement failures |
| --- | --- | --- | --- |
| 0 | 0 | 0 | 0 |

## Notes

- Total deck-sandwich boxes: 1065
- Face-top panels: 53 (each panel should be one bay footprint, not ~1 m slices)
- No coplanar face-sheet overlaps detected.
- No duplicate deck-sandwich names.
- Ribs appear within face half-extents (AABB estimate).
- Deck 0: faces=18, longRibs=193, athRibs=42, peri=72 (~13.1 ribs/panel)
- Deck 1: faces=18, longRibs=193, athRibs=42, peri=72 (~13.1 ribs/panel)
- Deck m1: faces=17, longRibs=235, athRibs=42, peri=68 (~16.3 ribs/panel)
