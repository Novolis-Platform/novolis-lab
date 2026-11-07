# Calypso CAD

Lab host that generates the Calypso lock companions from `docs/internals/CAL-INT-GA-001.json` and the manufacturer OML in `docs/manufacturer/CAL-HULL-CAD-001.json`. It explores plan / orbit / interior, writes headless PNGs, and serves the `calypso.*` agent profile.

Ship Designer Lab (`d:\novolis\novolis-lab\labs\cad\ShipDesignerLab`) imports the generated `.cadjson`. This host stays generate / validate / dump.

## Source layout

| Folder | Role |
|--------|------|
| `Generation/` | Lock → CAD emit, BH/frames, acceptance, fab orchestration (Calypso-specific) |
| `Blueprints/` | ISO HTML + structure detail sheets |
| `Host/` | Session, Silk window, viewport controller, view/layer modes |
| `Shell/` | Avalonia `--explorer` chrome + Silk viewport (GraphicalProfile) |
| `Rendering/` | Calypso Silk renderer + exterior draw rules |
| `Export/` | Headless PNG / build-steps / structure audit |
| `Agent/` | `calypso.*` agent surface |

Shared CAD/fab/Silk primitives live in libraries: `Novolis.Cad.Primitives` (sidecars + `CadJson` + bundle IO), `Novolis.Ship.Structure` (DXF / nest / unfold), `Novolis.Silk` (`SilkCadWorld`, headless capture, `RgbaPng`).

## Canon lock

| Spec | Value |
|------|-------|
| Outer hull | `docs/manufacturer/CAL-HULL-CAD-001.json` OML. Interiors nest inside the IML. |
| Hull envelope | **69 m LOA · 20 m beam · 12 m OAH** (`L_fore=17` / `L_mid=48` / `L_aft=4`) |
| Shell | **AISI 316L · t=8 mm** flat plate. No bends, no nacelles, no side pods. |
| Frames | Rings at `hullLoft` stations ST0, ST1, ST2, ST3, MB1, MB2, AFT, plus four stringers |
| Hold | HILS-C40 **5×1×3** · aft door **14×8.5** · sill **0.25** · `ramp-aft` · `coaming-aft-*` |
| Engineering | `ENG` atrium |
| Decks | **−1 @ 0.5 · 0 @ 4.0 · +1 @ 8.0** · room clear ~3.2 m |
| Corridors | `CORR_P` / `CORR_S` clear **3.0 m** |
| Cabins | DK0 `CREW_1…5` + DK+1 `PAX_1…5` — clear **1.92×7.2** |
| Outfit | `outfit-{compartment}-{role}` boxes from the lock `outfit` schedule |
| Deck SoT | `docs/internals/CAL-INT-DK-001.html` + `CAL-INT-GA-001.json` |

**CAL-MFG-002** (`--fab-release`) emits profile-based structure DXF, hull/ partition nests, weld program JSON, and `calypso-fab-manifest.json`. STEP AP242 / GD&T are **CAL-MFG-003** — see `docs/internals/CAL-MFG-002.md`.

Holistic fab gate:

```powershell
dotnet run --project d:\novolis\novolis-lab\labs\cad\CalypsoCad\CalypsoCad.csproj -p:NovolisUseProjectReferences=true -- --acceptance
dotnet run --project d:\novolis\novolis-lab\labs\cad\CalypsoCad\CalypsoCad.csproj -p:NovolisUseProjectReferences=true -- --fab-release
dotnet run --file d:\novolis\novolis-governance\scripts\verify-calypso-fab-release.cs
dotnet run --file d:\novolis\novolis-governance\scripts\verify-calypso-visual-gate.cs
```

## Holistic quality gate

After geometry or outfit changes, run the multi-pass loop (Cursor skill `calypso-cad-holistic-review`):

1. `--acceptance` (document + poke-through assert + furnished cabins)
2. Headless dumps: plan deck 0, cutaway-long, interiors `CREW_1` and `ENG`
3. Refresh functional canvas (acceptance table + curated PNGs):

```powershell
dotnet run --project d:\novolis\novolis-lab\labs\cad\CalypsoCad\CalypsoCad.csproj -p:NovolisUseProjectReferences=true -- --headless
dotnet run --file d:\novolis\novolis-governance\scripts\Emit-CalypsoFunctionalCanvas.cs
```

Writes `C:\Users\frank\.cursor\projects\d-novolis\canvases\calypso-functional-vessel.canvas.tsx`. Legacy `calypso-cad-views.canvas.tsx` remains optional.

Generator `outfit-*` boxes are authoritative; the renderer skips duplicate compartment props for furnished spaces.

## Run

```powershell
# Human default: Avalonia explorer chrome + Silk “Calypso CAD” 3D window
dotnet run --project d:\novolis\novolis-lab\labs\cad\CalypsoCad\CalypsoCad.csproj -p:NovolisUseProjectReferences=true
# Explicit explorer / GLFW-only
dotnet run --project d:\novolis\novolis-lab\labs\cad\CalypsoCad\CalypsoCad.csproj -p:NovolisUseProjectReferences=true -- --explorer
dotnet run --project d:\novolis\novolis-lab\labs\cad\CalypsoCad\CalypsoCad.csproj -p:NovolisUseProjectReferences=true -- --silk
dotnet run --project d:\novolis\novolis-lab\labs\cad\CalypsoCad\CalypsoCad.csproj -p:NovolisUseProjectReferences=true -- --generate-only --json-only
dotnet run --project d:\novolis\novolis-lab\labs\cad\CalypsoCad\CalypsoCad.csproj -p:NovolisUseProjectReferences=true -- --structure-audit
dotnet run --file d:\novolis\novolis-governance\scripts\verify-calypso-bh-spine.cs
dotnet run --project d:\novolis\novolis-lab\labs\cad\CalypsoCad\CalypsoCad.csproj -p:NovolisUseProjectReferences=true -- --acceptance
dotnet run --project d:\novolis\novolis-lab\labs\cad\CalypsoCad\CalypsoCad.csproj -p:NovolisUseProjectReferences=true -- --blueprints
dotnet run --file d:\novolis\novolis-governance\scripts\verify-calypso-blueprints.cs
dotnet run --project d:\novolis\novolis-lab\labs\cad\CalypsoCad\CalypsoCad.csproj -p:NovolisUseProjectReferences=true -- --headless
dotnet run --project d:\novolis\novolis-lab\labs\cad\CalypsoCad\CalypsoCad.csproj -p:NovolisUseProjectReferences=true -- --mcp
# optional human glance (not structure acceptance):
# --build-steps
```

**Structure acceptance** = audit JSON + `verify-calypso-bh-spine` + blueprints/`docs/internals/details/` — not Silk FPS. SoT freeze: `docs/internals/sot-table.json`.

On startup, companions land in `%LocalAppData%\Novolis\CalypsoCad\generated\`.

Agent actions: `calypso.lock`, `calypso.describe`, `calypso.validate`, `calypso.dump`, and the approval-gated `calypso.generate`, `calypso.outfit`, `calypso.structure`.

Headless layer exports: `layer-cutaway-structure.png` (S-STRUCT + cyan passageway voids), `layer-cutaway-fitout.png` (partitions/outfit), `interior-cutaway-portCorridor.png` (corridor structure read). Interactive host: **G** cycles layer presets (orbit: all → structure+cutouts → fitout+cutouts → shell+frames; interior: fitout ↔ corridor+structure).

**Frame-first structure:** `CalypsoShipbuildingFrames` — closed octagon ring webs, deck longitudinals, transverse deck beams. Verify: `dotnet run --file d:\novolis\novolis-governance\scripts\verify-calypso-frame-first.cs`. Cursor skill: `calypso-shipbuilding-frames`.

**DNV-style design review** (classification-style third-party read — not plan approval):

```powershell
dotnet run --project d:\novolis\novolis-lab\labs\cad\CalypsoCad\CalypsoCad.csproj -p:NovolisUseProjectReferences=true -- --dnv-review --headless
```

Writes `%LocalAppData%\Novolis\CalypsoCad\generated\CAL-DNV-REVIEW-001.html`, `calypso-dnv-review.json`, and embeds layer-cutaway annex PNGs when `--headless` is included. Acceptance includes `DNV review: …` checks.

## Headless tour

PNG renders are **source-controlled** under `docs/renders/` (not LocalAppData):

```powershell
dotnet run --project d:\novolis\novolis-lab\labs\cad\CalypsoCad\CalypsoCad.csproj -p:NovolisUseProjectReferences=true -- --headless
```

- `plan-deck-m1.png` / `plan-deck-0.png` / `plan-deck-p1.png`
- `orbit-bow-quarter.png`, `orbit-broadside.png`, `orbit-stern-quarter.png`, `orbit-stern-on.png`, `orbit-ramp-close.png`
- `orbit-cutaway-long.png` / `orbit-cutaway-beam.png` / `layer-structure-wire.png`
- Interior: `interior-solid-bridge`, `interior-solid-cabin1` (`CREW_1`), `interior-solid-galley`, `interior-solid-engineering` (`ENG`), `interior-solid-cargoVoid` (`HOLD`)

### Structure build-step slide deck

Same camera as `layer-structure-wire` — hull, then each frame station, longitudinals, then each hatch (TodoList canvas):

```powershell
dotnet run --project d:\novolis\novolis-lab\labs\cad\CalypsoCad\CalypsoCad.csproj -p:NovolisUseProjectReferences=true -- --build-steps --emit-build-steps-canvas
```

Writes `docs/renders/build-steps/*.png` + `calypso-structure-build-deck.canvas.tsx`.

## Explore

| Key | Action |
|-----|--------|
| P / O / I | Plan / orbit / interior |
| W / C / S | Wire / cutaway / solid |
| [ ] | Slide the cut plane |
| L / B | Longitudinal / beam cut |
| 1 / 2 / 3 / 0 | Deck −1 / 0 / +1 / all |
| F | Fit orbit camera |
| E | Export the current PNG |
