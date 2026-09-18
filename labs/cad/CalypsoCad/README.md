# Calypso CAD

Lab host that generates **Calypso** Rev F lock companions from `docs/internals/CAL-INT-GA-001.json` (deck authority **`CAL-INT-DK-001.html`**) and explores plan / orbit / interior. **Author freighters in [Ship Designer](https://github.com/Novolis-Platform/novolis-apps)** (`d:\novolis\novolis-apps\src\ShipDesigner`) — Open/Save `.cadjson`, hatches, airtight validation. This host stays generate / headless PNG / walkthrough / Ship.* API experimentation.

## Canon lock

| Spec | Value |
|------|-------|
| **Outer hull** | **`docs/manufacturer/CAL-HULL-CAD-001.json` OML** (scaled to lock envelope if needed). Interiors nest **inside IML**. |
| Hull envelope | **69 m LOA · 20 m beam · 12 m OAH** (midbody stretch; `L_fore=17` / `L_aft=4` fixed) |
| Shell | **AISI 316L · t=8 mm** flat-plate pepakura (`Novolis.Ship.Structure` BOM/mass ~243 t skin) |
| Hold | HILS-C40 **5×1×3** · pack length **19.692 m** · aft door **14×8.5** |
| Engineering | Full-height atrium **8.25 m** F–A |
| Decks | **−1 @ 0.5 · 0 @ 4.0 · +1 @ 8.0** · room clear ~3.2 m |
| Corridors | Twin port/stbd clear **3.0 m** (inner ±5; stack ±5 = 10) — `CAL-INT-DK-001` |
| Cabins | **5× C0n** DK0 `CREW_1…5` + DK+1 `PAX_1…5` — clear **1.92×7.2**, module O/A **2 m** |
| Airlocks | Port/stbd L-airlocks DK0 · D3 vacuum-assisted (pressure-assist seal) |
| Deck SoT | `docs/internals/CAL-INT-DK-001.html` Rev F + `CAL-INT-GA-001.json` |

Out of scope for this dogfood: inventing a second exterior hull (no nacelle/blister placeholders), uniform isotropic rescale (midbody stretch only), full Boolean ring/sill BREP.

## Arrangement (engineering)

```
FP ── hab stack (−1/0/+1) ── WT-BH eng (38.8) ── Engineering OAH ── WT-BH hold (47) ── Hold 9 m ── AP
```

- Junctions emit **VEST-BR / VEST-P / VEST-S** with **three framed openings** each (never open T by deleting walls).
- Opening entities carry schedule `tag`, `W_open` / `H_open` / `H_sill`, `ClearWidth` / `ClearHeight`, `hostWallId`, `connects`.

## What is generated

- Full RevG room inventory with **partitions on every room**
- Individual **Crew Cabin 1–5** and **Berth 1–10** (via `arrayInstance` metadata + derived spaces/walls)
- Stairs / Elev shafts on all three decks
- **One** Engineering full-OAH void + **one** Cargo Void (9 m) with `continuousVoid` (visible across deck filters)
- Fore catwalk (full bay × 3 m) + twin CD hatches on armored hold BH + aft ramp **~12.99 × 4.0 × 3.2**
- **HILS-C40** stow: 15 `box` entities (`C40-c{col}-t{tier}`), 5 abreast × 1 deep × 3 high
- Vestibules + tagged opening schedule (PD-*/AH-*/CD-*/RAMP) with **wall baseline splits** (`OpeningDerivation`)
- Lore hooks: `Bridge`, `OwnerLock`, `PhotoWallBridge`, `ArmoryCrossing`, `GalleyGrowthChart`, `EngCore`, `CargoVoidEye`, `AftRamp`, airlocks, etc.
- **Outer hull** = manufacturer OML mesh; **IML** loft for nest; lock clears clipped to fit inside
- Aft: large cargo **hatch-ramp** (no stern thruster bank)

## Detail pass (renderer)

Immediate-mode cubes/lines/cylinders (no DrawTriangle3D):

- Door / hatch **leaves** + aft **ramp steps**; armored tint on CD-*
- Module prop kits: cabin UI/webbing, galley appliance run, airlock dual hatches + hooks, triple corridor trunks, eng tanks/conduits, lounge bar/stools, cargo gantry/cleats/handrails
- Orbit: manufacturer hull panel mesh + C40 containers in cutaway / cargo interior

## Run

```powershell
dotnet run --project d:\novolis\novolis-lab\labs\cad\CalypsoCad
dotnet run --project d:\novolis\novolis-lab\labs\cad\CalypsoCad -- --generate-only --json-only
dotnet run --project d:\novolis\novolis-lab\labs\cad\CalypsoCad -- --acceptance
```

On startup regenerates under `%LocalAppData%\Novolis\CalypsoCad\generated\`:

- `calypso.cadlayers.json`
- `calypso.cadshapejson`
- `calypso.cadjson`

Import that seed into Ship Designer via **File → Import Calypso seed…**.

Headless Raylib PNG tour:

```powershell
dotnet run --project d:\novolis\novolis-lab\labs\cad\CalypsoCad -- --headless
```

Camera walkthrough via **`Novolis.Raylib.Capture`** `FrameCaptureSession` (PNG frame stream after each `EndDrawing`; ffmpeg assembles MP4/GIF — no BeginVideo binding in this Raylib stack):

```bash
dotnet run --project labs/cad/CalypsoCad -- --walkthrough
```

Both stills and walkthrough (stills **2560×1440**, walkthrough **1920×1080**):

```bash
dotnet run --project labs/cad/CalypsoCad -- --headless --walkthrough
```

JSON only:

```bash
dotnet run --project labs/cad/CalypsoCad -- --generate-only --json-only
```

## Headless tour checklist

Exports land in `generated/exports/` as **stable overwrite names** (`{kind}.png`). Legacy `*-headless-*.png` files are purged on each headless run. Manual **Export PNG (E)** still writes a timestamped snap.

- [ ] `plan-deck-m1.png` / `plan-deck-0.png` / `plan-deck-p1.png`
- [ ] `orbit-bow-quarter.png` — high 3/4 bow
- [ ] `orbit-broadside.png` — low beam elevation
- [ ] `orbit-stern-quarter.png` — aft 3/4: hatch-ramp + side-pod engines
- [ ] `orbit-stern-on.png` — ramp face (no stern thruster bank)
- [ ] `orbit-ramp-close.png` / `orbit-pod-port.png` / `orbit-pod-stbd.png` / `orbit-pod-ftl.png`
- [ ] `orbit-broadside.png` / `orbit-low-pass.png` / `orbit-three-quarter-high.png`
- [ ] `orbit-cutaway-long.png` / `orbit-cutaway-beam.png`
- [ ] Interior solid: `interior-solid-{bridge,crossing,cabin1,galley,infirmary,stairs,engineering,cargoVoid,lounge,airlockPort,airlockStbd}.png`
- [ ] Interior section: `interior-cutaway-bridge.png` / `interior-cutaway-cargoVoid.png` / `interior-cutaway-engineering.png`
- [ ] DK0 catwalk POV: `catwalk-containers.png` / `catwalk-containers-quarter.png` / `catwalk-span.png` / `catwalk-passage-port.png` / `catwalk-passage-stbd.png`
- [ ] Walkthrough: `walkthrough/frame-*.png`, optional `walkthrough.mp4` / `walkthrough.gif`, keyframes `walkthrough-*.png`

**Cutaway (C):** invisible slicing plane — exterior mesh triangles on the camera side are culled so interiors read through. Slide with **[ ]** (or `,` `.`); **L** = longitudinal (default), **B** = beam cut. HUD shows `cut=long|beam@±m`.

**Catwalk presets:** standing eye on mid-deck (DK0) cargo catwalk; ensemble draws hold + C40 stack + twin corridors so you can look aft at containers or forward down a passageway.

## Explore (UI)

| Key / UI | Action |
|----------|--------|
| P / Plan | Top-down plan |
| O / Orbit | Exterior orbit (drag / wheel) |
| I / Interior | Camera inside selected space |
| W / C / S | Wire / cutaway / solid |
| [ ] or , . | Slide cutaway plane |
| L / B | Longitudinal / beam cut axis |
| 1 / 2 / 3 | Deck −1 / 0 / +1 filter |
| 0 | All decks |
| F | Fit orbit camera |
| E | Export current PNG |
| Spaces / Hooks lists | Select room or lore hook for interior camera |

Walls carry **two-sided** `shapeId`s (side A = left of baseline with +Y up).

## Packages

PackageReference only (`2026.1.*`): Avalonia, `Novolis.Avalonia.Raylib`, `Novolis.Raylib`, `Novolis.Raylib.Capture`, `Novolis.Rendering.Presentation.Silk`, `Novolis.Avalonia.Studio`.
