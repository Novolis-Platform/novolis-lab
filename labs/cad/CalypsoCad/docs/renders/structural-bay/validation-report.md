# Calypso two-bay structural validation

Overall: **PASS**

## Checks

| Check | Result | Detail |
| --- | --- | --- |
| two open frames present | PASS | ringBars=24 (expect ≥16 for 3×8) |
| no wall-like ordinary frames / BH in bay proof | PASS | wallLike=0 |
| longitudinal girders present | PASS | girders=6 |
| hull stringers present | PASS | stringers=16 platingJoins=16 |
| shell panels present | PASS | shell=16 |
| plating-join longitudinals at 8 octagon knuckles | PASS | misses=0 (expect 0 of 16 join seats) |
| cellular deck cores present | PASS | deckParts=48 |
| frame ring edge continuity (geometric) | PASS | edgeMisses=0 |
| keel girder continuous across bays | PASS | keelSegs=2 gaps=0 |
| joint mating geometry | PASS | joints=84 geomFail=0 |
| connected primary assembly (geom joints) | PASS | isolated=0 girderGaps=0 freeEnds=0 |
| habitable mid-bay volume clear of wall solids | PASS | clutter=0 |

## Joints (geometric mating)

| Id | A | B | Geometry | Detail |
| --- | --- | --- | --- | --- |
| `j-frame-F24-chord-dk0-ring` | `frame-F24-chord-dk0` | `frame-F24` | PASS | gap=0 secB=0 |
| `j-frame-F28-chord-dk0-ring` | `frame-F28-chord-dk0` | `frame-F28` | PASS | gap=0 secB=0 |
| `j-frame-F32-chord-dk0-ring` | `frame-F32-chord-dk0` | `frame-F32` | PASS | gap=0 secB=0 |
| `j-stringer-join-v0-bay0-fore` | `stringer-join-v0-bay0` | `frame-F24` | PASS | gap=0 secB=0 |
| `j-stringer-join-v0-bay0-aft` | `stringer-join-v0-bay0` | `frame-F28` | PASS | gap=0 secB=0 |
| `j-stringer-join-v1-bay0-fore` | `stringer-join-v1-bay0` | `frame-F24` | PASS | gap=0 secB=0 |
| `j-stringer-join-v1-bay0-aft` | `stringer-join-v1-bay0` | `frame-F28` | PASS | gap=0 secB=0 |
| `j-stringer-join-v2-bay0-fore` | `stringer-join-v2-bay0` | `frame-F24` | PASS | gap=1.128 secB=1.173 |
| `j-stringer-join-v2-bay0-aft` | `stringer-join-v2-bay0` | `frame-F28` | PASS | gap=1.128 secB=1.173 |
| `j-stringer-join-v3-bay0-fore` | `stringer-join-v3-bay0` | `frame-F24` | PASS | gap=1.128 secB=1.173 |
| `j-stringer-join-v3-bay0-aft` | `stringer-join-v3-bay0` | `frame-F28` | PASS | gap=1.128 secB=1.173 |
| `j-stringer-join-v4-bay0-fore` | `stringer-join-v4-bay0` | `frame-F24` | PASS | gap=0 secB=0 |
| `j-stringer-join-v4-bay0-aft` | `stringer-join-v4-bay0` | `frame-F28` | PASS | gap=0 secB=0 |
| `j-stringer-join-v5-bay0-fore` | `stringer-join-v5-bay0` | `frame-F24` | PASS | gap=0 secB=0 |
| `j-stringer-join-v5-bay0-aft` | `stringer-join-v5-bay0` | `frame-F28` | PASS | gap=0 secB=0 |
| `j-stringer-join-v6-bay0-fore` | `stringer-join-v6-bay0` | `frame-F24` | PASS | gap=1.128 secB=1.173 |
| `j-stringer-join-v6-bay0-aft` | `stringer-join-v6-bay0` | `frame-F28` | PASS | gap=1.128 secB=1.173 |
| `j-stringer-join-v7-bay0-fore` | `stringer-join-v7-bay0` | `frame-F24` | PASS | gap=1.128 secB=1.173 |
| `j-stringer-join-v7-bay0-aft` | `stringer-join-v7-bay0` | `frame-F28` | PASS | gap=1.128 secB=1.173 |
| `j-girder-keel-bay0-fore` | `girder-keel-bay0` | `frame-F24` | PASS | gap=0.02 secB=0.072 |
| `j-girder-keel-bay0-aft` | `girder-keel-bay0` | `frame-F28` | PASS | gap=0.02 secB=0.072 |
| `j-girder-port-bay0-fore` | `girder-port-bay0` | `frame-F24` | PASS | gap=0.02 secB=0.072 |
| `j-girder-port-bay0-aft` | `girder-port-bay0` | `frame-F28` | PASS | gap=0.02 secB=0.072 |
| `j-girder-stbd-bay0-fore` | `girder-stbd-bay0` | `frame-F24` | PASS | gap=0.02 secB=0.072 |
| `j-girder-stbd-bay0-aft` | `girder-stbd-bay0` | `frame-F28` | PASS | gap=0.02 secB=0.072 |
| `j-stringer-join-v0-bay1-fore` | `stringer-join-v0-bay1` | `frame-F28` | PASS | gap=0 secB=0 |
| `j-stringer-join-v0-bay1-aft` | `stringer-join-v0-bay1` | `frame-F32` | PASS | gap=0 secB=0 |
| `j-stringer-join-v1-bay1-fore` | `stringer-join-v1-bay1` | `frame-F28` | PASS | gap=0 secB=0 |
| `j-stringer-join-v1-bay1-aft` | `stringer-join-v1-bay1` | `frame-F32` | PASS | gap=0 secB=0 |
| `j-stringer-join-v2-bay1-fore` | `stringer-join-v2-bay1` | `frame-F28` | PASS | gap=1.128 secB=1.173 |
| `j-stringer-join-v2-bay1-aft` | `stringer-join-v2-bay1` | `frame-F32` | PASS | gap=1.128 secB=1.173 |
| `j-stringer-join-v3-bay1-fore` | `stringer-join-v3-bay1` | `frame-F28` | PASS | gap=1.128 secB=1.173 |
| `j-stringer-join-v3-bay1-aft` | `stringer-join-v3-bay1` | `frame-F32` | PASS | gap=1.128 secB=1.173 |
| `j-stringer-join-v4-bay1-fore` | `stringer-join-v4-bay1` | `frame-F28` | PASS | gap=0 secB=0 |
| `j-stringer-join-v4-bay1-aft` | `stringer-join-v4-bay1` | `frame-F32` | PASS | gap=0 secB=0 |
| `j-stringer-join-v5-bay1-fore` | `stringer-join-v5-bay1` | `frame-F28` | PASS | gap=0 secB=0 |
| `j-stringer-join-v5-bay1-aft` | `stringer-join-v5-bay1` | `frame-F32` | PASS | gap=0 secB=0 |
| `j-stringer-join-v6-bay1-fore` | `stringer-join-v6-bay1` | `frame-F28` | PASS | gap=1.128 secB=1.173 |
| `j-stringer-join-v6-bay1-aft` | `stringer-join-v6-bay1` | `frame-F32` | PASS | gap=1.128 secB=1.173 |
| `j-stringer-join-v7-bay1-fore` | `stringer-join-v7-bay1` | `frame-F28` | PASS | gap=1.128 secB=1.173 |
| `j-stringer-join-v7-bay1-aft` | `stringer-join-v7-bay1` | `frame-F32` | PASS | gap=1.128 secB=1.173 |
| `j-girder-keel-bay1-fore` | `girder-keel-bay1` | `frame-F28` | PASS | gap=0.02 secB=0.072 |
| `j-girder-keel-bay1-aft` | `girder-keel-bay1` | `frame-F32` | PASS | gap=0.02 secB=0.072 |
| `j-girder-port-bay1-fore` | `girder-port-bay1` | `frame-F28` | PASS | gap=0.02 secB=0.072 |
| `j-girder-port-bay1-aft` | `girder-port-bay1` | `frame-F32` | PASS | gap=0.02 secB=0.072 |
| `j-girder-stbd-bay1-fore` | `girder-stbd-bay1` | `frame-F28` | PASS | gap=0.02 secB=0.072 |
| `j-girder-stbd-bay1-aft` | `girder-stbd-bay1` | `frame-F32` | PASS | gap=0.02 secB=0.072 |
| `j-shell-panel-bay0-e0-fore` | `shell-panel-bay0-e0` | `frame-F24` | PASS | gap=0 secB=0 |
| `j-shell-panel-bay0-e0-aft` | `shell-panel-bay0-e0` | `frame-F28` | PASS | gap=0 secB=0 |
| `j-shell-panel-bay0-e1-fore` | `shell-panel-bay0-e1` | `frame-F24` | PASS | gap=0 secB=0 |
| `j-shell-panel-bay0-e1-aft` | `shell-panel-bay0-e1` | `frame-F28` | PASS | gap=0 secB=0 |
| `j-shell-panel-bay0-e2-fore` | `shell-panel-bay0-e2` | `frame-F24` | PASS | gap=0 secB=0 |
| `j-shell-panel-bay0-e2-aft` | `shell-panel-bay0-e2` | `frame-F28` | PASS | gap=0 secB=0 |
| `j-shell-panel-bay0-e3-fore` | `shell-panel-bay0-e3` | `frame-F24` | PASS | gap=0 secB=0 |
| `j-shell-panel-bay0-e3-aft` | `shell-panel-bay0-e3` | `frame-F28` | PASS | gap=0 secB=0 |
| `j-shell-panel-bay0-e4-fore` | `shell-panel-bay0-e4` | `frame-F24` | PASS | gap=0 secB=0 |
| `j-shell-panel-bay0-e4-aft` | `shell-panel-bay0-e4` | `frame-F28` | PASS | gap=0 secB=0 |
| `j-shell-panel-bay0-e5-fore` | `shell-panel-bay0-e5` | `frame-F24` | PASS | gap=0 secB=0 |
| `j-shell-panel-bay0-e5-aft` | `shell-panel-bay0-e5` | `frame-F28` | PASS | gap=0 secB=0 |
| `j-shell-panel-bay0-e6-fore` | `shell-panel-bay0-e6` | `frame-F24` | PASS | gap=0 secB=0 |
| `j-shell-panel-bay0-e6-aft` | `shell-panel-bay0-e6` | `frame-F28` | PASS | gap=0 secB=0 |
| `j-shell-panel-bay0-e7-fore` | `shell-panel-bay0-e7` | `frame-F24` | PASS | gap=0 secB=0 |
| `j-shell-panel-bay0-e7-aft` | `shell-panel-bay0-e7` | `frame-F28` | PASS | gap=0 secB=0 |
| `j-shell-panel-bay1-e0-fore` | `shell-panel-bay1-e0` | `frame-F28` | PASS | gap=0 secB=0 |
| `j-shell-panel-bay1-e0-aft` | `shell-panel-bay1-e0` | `frame-F32` | PASS | gap=0 secB=0 |
| `j-shell-panel-bay1-e1-fore` | `shell-panel-bay1-e1` | `frame-F28` | PASS | gap=0 secB=0 |
| `j-shell-panel-bay1-e1-aft` | `shell-panel-bay1-e1` | `frame-F32` | PASS | gap=0 secB=0 |
| `j-shell-panel-bay1-e2-fore` | `shell-panel-bay1-e2` | `frame-F28` | PASS | gap=0 secB=0 |
| `j-shell-panel-bay1-e2-aft` | `shell-panel-bay1-e2` | `frame-F32` | PASS | gap=0 secB=0 |
| `j-shell-panel-bay1-e3-fore` | `shell-panel-bay1-e3` | `frame-F28` | PASS | gap=0 secB=0 |
| `j-shell-panel-bay1-e3-aft` | `shell-panel-bay1-e3` | `frame-F32` | PASS | gap=0 secB=0 |
| `j-shell-panel-bay1-e4-fore` | `shell-panel-bay1-e4` | `frame-F28` | PASS | gap=0 secB=0 |
| `j-shell-panel-bay1-e4-aft` | `shell-panel-bay1-e4` | `frame-F32` | PASS | gap=0 secB=0 |
| `j-shell-panel-bay1-e5-fore` | `shell-panel-bay1-e5` | `frame-F28` | PASS | gap=0 secB=0 |
| `j-shell-panel-bay1-e5-aft` | `shell-panel-bay1-e5` | `frame-F32` | PASS | gap=0 secB=0 |
| `j-shell-panel-bay1-e6-fore` | `shell-panel-bay1-e6` | `frame-F28` | PASS | gap=0 secB=0 |
| `j-shell-panel-bay1-e6-aft` | `shell-panel-bay1-e6` | `frame-F32` | PASS | gap=0 secB=0 |
| `j-shell-panel-bay1-e7-fore` | `shell-panel-bay1-e7` | `frame-F28` | PASS | gap=0 secB=0 |
| `j-shell-panel-bay1-e7-aft` | `shell-panel-bay1-e7` | `frame-F32` | PASS | gap=0 secB=0 |
| `j-deck-sandwich-dk0-bay0-fore` | `deck-sandwich-dk0-bay0` | `frame-F24` | PASS | gap=0 secB=0 |
| `j-deck-sandwich-dk0-bay0-aft` | `deck-sandwich-dk0-bay0` | `frame-F28` | PASS | gap=0 secB=0 |
| `j-deck-sandwich-dk0-bay1-fore` | `deck-sandwich-dk0-bay1` | `frame-F28` | PASS | gap=0 secB=0 |
| `j-deck-sandwich-dk0-bay1-aft` | `deck-sandwich-dk0-bay1` | `frame-F32` | PASS | gap=0 secB=0 |
| `j-bay-boundary-F28` | `frame-F28` | `girder-keel` | PASS | gap=0.02 secB=0.072 |
