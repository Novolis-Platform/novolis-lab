# CalypsoCad — agent contract

Lab host for the Calypso tramp freighter CAD (lock → one-model CAD). Not the captain-bridge game.

## Required reading

- [docs/internals/CAL-STRUCT-GLOSSARY.md](docs/internals/CAL-STRUCT-GLOSSARY.md) — structural / CAD terminology (ring frame vs pressure bulkhead, knuckle stringers, sandwich decks, semantic pipeline)
- [README.md](README.md) — modules, run commands, canon lock
- [docs/internals/README.md](docs/internals/README.md) — blueprint emit notes

## Authority

- Lock: `docs/internals/CAL-INT-GA-001.json`
- Outer hull: `docs/manufacturer/CAL-HULL-CAD-001.json` OML; interiors nest in IML
- Envelope: **69 m LOA · 20.8 m beam · 12.4 m OAH** (shell)
- Decks (plate tops): −1 @ 0.5 · 0 @ 4.4 · +1 @ 8.0; tPlate 0.1 m
- Frames: station-driven `FrameSchedule` (BH anchors → fill ≤ 4 m); heavy BH @ 13 / 19 / 23 / 30.28 / 41 / 49.25
- Coordinates: lock `zFromStem` = 0 at bow; CAD +X stbd, +Y up, +Z bow

## Non-negotiables

- C# / PowerShell only — no Node/Python generators
- Regenerate after structure/CAD changes: `--generate-only` → `--blueprints` → `--acceptance` → `--fab-release` → `--headless-export`
- Commit regenerated `docs/**` with the code; push `main` (no maintainer PRs)
- Do not invent a second exterior hull; do not drag Avalonia into Gaming/Simulation libraries
- Transverse annular rings and longitudinal knuckle paths are first-class — do not resurrect mid-bay axis-aligned knuckle boxes

## Cursor skills (when relevant)

`calypso-vessel-lock`, `calypso-cad-pipeline`, `calypso-generator-map`, `calypso-structure-verify`, `calypso-cad-holistic-review`, `novolis-3d-visual-verify`

## Verify

```powershell
dotnet run --project d:\novolis\novolis-lab\labs\cad\CalypsoCad\CalypsoCad.csproj -p:NovolisUseProjectReferences=true -- --generate-only --json-only
dotnet run --project d:\novolis\novolis-lab\labs\cad\CalypsoCad\CalypsoCad.csproj -p:NovolisUseProjectReferences=true -- --blueprints
dotnet run --project d:\novolis\novolis-lab\labs\cad\CalypsoCad\CalypsoCad.csproj -p:NovolisUseProjectReferences=true -- --acceptance
dotnet run --file d:\novolis\novolis-lab\labs\cad\CalypsoCad\scripts\verify-calypso-bh-spine.cs
```
