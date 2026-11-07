# Calypso internals drawings (retired Node HTML)

HTML blueprint sheets are emitted by **C#** only:

```powershell
dotnet run --project d:\novolis\novolis-lab\labs\cad\CalypsoCad\CalypsoCad.csproj -p:NovolisUseProjectReferences=true -- --blueprints
```

`--blueprints` also refreshes the manufacturer HTML under `docs/manufacturer`. CAD + Wavefront OBJ from this lock:

```powershell
dotnet run --project d:\novolis\novolis-lab\labs\cad\CalypsoInternalsCad\CalypsoInternalsCad.csproj -p:NovolisUseProjectReferences=true
```

| Sheet | Content |
| --- | --- |
| `index.html` | Package index (links all sheets + detail pack) |
| `CAL-INT-GA-001.html` | Profile + deck 0 plan (ISO 128-15 layout) |
| `CAL-INT-DK-001.html` | Decks −1 / 0 / +1 — CAD figured dimensions (C0n clear / module O/A, corridors, INF/GAL/STORE) |
| `CAL-INT-DK-002.html` | Same deck plans with Type C0n expected-size overlays (1.92 clear / 2.0 with walls) |
| `CAL-INT-DKM1-001.html` | Full Deck −1 plan |
| `CAL-INT-DK0-001.html` | Full Deck 0 plan |
| `CAL-INT-DKP1-001.html` | Full Deck +1 plan |
| `CAL-INT-PRF-001.html` | Side elevation (CL) full |
| `CAL-INT-SEC-001.html` | Transverse sections (looking aft) |
| `CAL-INT-SCH-001.html` | Hatch schedule + hold pack |
| `CAL-INT-HTC-001.html` | Full hatch schedule + typical leaf elevations |
| `CAL-INT-HOLD-001.html` | Cargo hold arrangement |
| `CAL-INT-PASS-001.html` | Passageways / corridors (DK0 + DK+1) |
| `details/` | Modular BH / frame / space crops |

Standards: ISO 5457 A1 landscape, ISO 7200-style title block, ISO 128-15 stern-left/bow-right.

`CAL-INT-GA-001.json` remains the machine lock (do not invent geometry in the SVG). Legacy `generate-internals.mjs` must not be used for sheets (Node forbidden).
