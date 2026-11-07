# Calypso CAD

Lab host: lock → one-model CAD, explore / validate / dump. Ship Designer Lab consumes generated `.cadjson`.

Shared CAD primitives stay in libraries (`Novolis.Cad.*`, `Novolis.Ship.*`, `Novolis.Silk`). This host is **Calypso-only**, sliced by module.

## Modules (source of truth)

| Module | Namespace | Owns |
|--------|-----------|------|
| **Structure** | `CalypsoCad.Structure` | Lock → cadjson: BH, open web frames, girders, sandwich decks, nest, drafting |
| **Fabrication** | `CalypsoCad.Fabrication` | CAL-MFG-002 emit, cut collect, hull fab, material map, fab gates |
| **Review** | `CalypsoCad.Review` | `--acceptance`, DNV-style design review |
| **Blueprints** | `CalypsoCad.Blueprints` | ISO internals / structure detail HTML |
| **Session** | `CalypsoCad.Session` | Session, layers, view/wire modes, Silk host, viewport controller |
| **Viewport** | `CalypsoCad.Viewport` | Silk 3D draw (renderer + exterior) |
| **Explorer** | `CalypsoCad.Explorer` | Avalonia `--explorer` chrome |
| **Export** | `CalypsoCad.Export` | Headless PNG / walkthrough / orbit / structure audit |
| **Canvas** | `CalypsoCad.Canvas` | Cursor canvas TSX emitters |
| **Agent** | `CalypsoCad.Agent` | `calypso.*` agent surface |

```text
Program.cs          CLI router only
docs/               lock + manufacturer + renders (content)
tools/              one-off scripts (not compiled into the host)
```

Dependency intent (downward only):

```text
Agent / Explorer / Canvas / Export / Blueprints / Review / Fabrication
        ↓
    Session ← Viewport
        ↓
    Structure
```

Do not put generic CAD types here — extend `Novolis.Cad.Primitives` / `Novolis.Ship.Structure` instead.

## Canon lock

| Spec | Value |
|------|-------|
| Outer hull | `docs/manufacturer/CAL-HULL-CAD-001.json` OML; interiors nest in IML |
| Envelope | **69 m LOA · 20 m beam · 12 m OAH** |
| Frames | Open transverse web frames at `frames.bayM` (ring + chords); few pressure `bh-*` |
| Hold | HILS-C40 **5×3**; aft door **14×8.5** |
| Decks | **−1 @ 0.5 · 0 @ 4.4 · +1 @ 8.0** (lock plate tops) |

## Run

| Mode | Invocation | Does |
|------|------------|------|
| **GUI** | (default; optional `--explorer` / `--silk`) | Avalonia chrome + Silk 3D |
| **Headless export** | `--headless-export` | Generate + blueprints + descriptors + 8K stills + ship OBJ/STL/GLB (+ FBX/DAE when Assimp supports) + 4K construction-orbit and walkthrough video |

```powershell
dotnet run --project d:\novolis\novolis-lab\labs\cad\CalypsoCad\CalypsoCad.csproj -p:NovolisUseProjectReferences=true
dotnet run --project d:\novolis\novolis-lab\labs\cad\CalypsoCad\CalypsoCad.csproj -p:NovolisUseProjectReferences=true -- --headless-export
dotnet run --project d:\novolis\novolis-lab\labs\cad\CalypsoCad\CalypsoCad.csproj -p:NovolisUseProjectReferences=true -- --generate-only --json-only
dotnet run --project d:\novolis\novolis-lab\labs\cad\CalypsoCad\CalypsoCad.csproj -p:NovolisUseProjectReferences=true -- --acceptance
dotnet run --project d:\novolis\novolis-lab\labs\cad\CalypsoCad\CalypsoCad.csproj -p:NovolisUseProjectReferences=true -- --fab-release
dotnet run --project d:\novolis\novolis-lab\labs\cad\CalypsoCad\CalypsoCad.csproj -p:NovolisUseProjectReferences=true -- --blueprints
```

Holistic fab / visual gates:

```powershell
dotnet run --file d:\novolis\novolis-governance\scripts\verify-calypso-fab-release.cs
dotnet run --file d:\novolis\novolis-governance\scripts\verify-calypso-visual-gate.cs
dotnet run --file d:\novolis\novolis-governance\scripts\verify-calypso-blueprints.cs
```

## Repair map

Cursor skill `calypso-generator-map` — symptom → owning module file.
