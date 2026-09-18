# SceneLab

Dogfood host for **Novolis.Avalonia.ThreeD** — CAD 3D editor / renderer (scene hierarchy, primitives, mesh edit, array/boolean, lights, cameras).

## Run

```powershell
dotnet run --project labs/avalonia/SceneLab -p:NovolisUseProjectReferences=true
dotnet run --project labs/avalonia/SceneLab -p:NovolisUseProjectReferences=true -- --edit
dotnet run --project labs/avalonia/SceneLab -p:NovolisUseProjectReferences=true -- --gallery
dotnet run --project labs/avalonia/SceneLab -p:NovolisUseProjectReferences=true -- --array
dotnet run --project labs/avalonia/SceneLab -p:NovolisUseProjectReferences=true -- --boolean
dotnet run --project labs/avalonia/SceneLab -p:NovolisUseProjectReferences=true -- --lights
dotnet run --project labs/avalonia/SceneLab -p:NovolisUseProjectReferences=true -- --sample
dotnet run --project labs/avalonia/SceneLab -p:NovolisUseProjectReferences=true -- --spatial-smoke
```

### Viewport renderer

**OpenGL is the CAD / 3D default** (Avalonia `OpenGlControlBase` + Silk). Use it for authoring.

**Render group** (main chrome): **Render…** opens a shaded preview pop-up (Lambert + scene lights, ambient, exposure, clear color). **Save PNG…** writes the preview; **Studio** ensures Key/Fill/Rim lights.

CPU / Vulkan / Raylib exist for `ViewportBench` and `--compare` only — not recommended for daily CAD:

```powershell
dotnet run --project labs/avalonia/SceneLab -p:NovolisUseProjectReferences=true -- --gallery
dotnet run --project labs/avalonia/SceneLab -p:NovolisUseProjectReferences=true -- --cpu --gallery
dotnet run --project labs/avalonia/SceneLab -p:NovolisUseProjectReferences=true -- --vulkan --gallery
dotnet run --project labs/avalonia/SceneLab -p:NovolisUseProjectReferences=true -- --raylib --gallery
dotnet run --project labs/avalonia/SceneLab -p:NovolisUseProjectReferences=true -- --compare --gallery
```

Also: `--renderer gl|cpu|vulkan|raylib` or env `SCENELAB_RENDERER` (default `gl`). Path-trace Vulkan remains a separate compute backend; SceneLab’s Vulkan option is graphics wire + readback.
When using `-p:NovolisUseProjectReferences=true`, SceneLab PackageReferences Raylib/Silk natives explicitly (ProjectRef mode is non-transitive for NuGet graphs).

## Demo mesh sample (optional)

Procedural bake tool for a dense interior/exterior mesh used as a viewport stress sample:

```powershell
dotnet run --project labs/avalonia/SceneLab/tools/KeelTransportBuilder -p:NovolisUseProjectReferences=true -- `
  labs/avalonia/SceneLab/samples/keel-stages `
  labs/avalonia/SceneLab/samples/keel-transport.nov3djson
```

`--sample` / `--keel` opens `keel-transport.nov3djson`. Prefer the wireframe-friendly **Chisel Corvette** (Expanse-style chisel + drive cup, original design):

```powershell
dotnet run --project labs/avalonia/SceneLab/tools/ChiselCorvetteBuilder -p:NovolisUseProjectReferences=true -- `
  labs/avalonia/SceneLab/samples/chisel-stages `
  labs/avalonia/SceneLab/samples/chisel-corvette.nov3djson
```

Then open via session HTTP (no app rebuild):

```bash
curl -X POST http://127.0.0.1:18785/session/command -H "content-type: application/json" \
  -d "{\"actionId\":\"open\",\"path\":\"D:/novolis/novolis-lab/labs/avalonia/SceneLab/samples/chisel-corvette.nov3djson\"}"
```

## Dumps / exports

Toolbar **Dump** (or agent `dump` / `dumpall`) writes under `bin/.../dumps/` (tooltip on the Dump control). Use **▾** for viewport / window / scene / mesh only.

| Artifact | Action |
|---|---|
| Viewport PNG (GL readback) | `dumpviewport` |
| Window UI PNG | `dumpwindow` |
| Scene `.nov3djson` copy | `dumpscene` |
| Mesh `.obj` + stats JSON | `dumpmesh` |
| Manifest `last-artifact.json` | always |

**VLM multimodal context:** call `dumpviewport` (optional `path`), then read the PNG path from the command result / `dumps/last-artifact.json` and pass that image to an external vision model. Do not embed CLIP/LERF in Rendering packages.

```bash
curl -X POST http://127.0.0.1:18785/session/command -H "content-type: application/json" \
  -d "{\"actionId\":\"dumpviewport\"}"
```

Spatial helpers (deterministic document tools — not LLM calls):

```bash
curl -X POST http://127.0.0.1:18785/session/command -H "content-type: application/json" \
  -d "{\"actionId\":\"describescene\"}"
curl -X POST http://127.0.0.1:18785/session/command -H "content-type: application/json" \
  -d "{\"actionId\":\"groundphrase\",\"phrase\":\"Beacon\"}"
curl -X POST http://127.0.0.1:18785/session/command -H "content-type: application/json" \
  -d "{\"actionId\":\"importtriangles\",\"path\":\"…/samples/triangle-soup.json\"}"
```

Radar: `novolis-governance/docs/research-radar/awesome-llm-3d.md`.

## Modeling

- **Edit modes:** Object / Point / Edge / Polygon (+ Make Editable)
- **Display:** Wireframe / Points / Isoline
- **Viewport:** click to pick (Shift multi-select), **Alt+LMB or MMB** orbit, wheel zoom, drag gizmo axes to translate
- **Primitives:** Box, Sphere, Cylinder, Cone, Plane, Capsule, Torus, Pyramid, Disc, Tube, Platonics, Landscape
- **Mesh tools:** Extrude, Inset, Bevel, Bridge, Dissolve, Knife, Weld, Optimize, Subdiv

## Session

HTTP **18785** + TCP JSONL **18786**.

```bash
curl http://127.0.0.1:18785/session/hello
curl -X POST http://127.0.0.1:18785/session/command -H "content-type: application/json" \
  -d "{\"actionId\":\"open\",\"path\":\"D:/novolis/novolis-lab/labs/avalonia/SceneLab/samples/keel-transport.nov3djson\"}"
```

MCP (AvaloniaAgentMcp): `scene_hello`, `scene_snapshot`, `scene_actions`, `scene_command`, `scene_definition`, `scene_hosts`, `scene_http_connect`.

## Corellian freighter (YT-1300 homage)

Screen-landmark interior+exterior bake keyed to Haynes / Wookieepedia YT-1300 layout (saucer, mandibles, offset cockpit + tube, ring corridor, main hold + game table, engineering/hyperdrive/escape pods, gunwells, sensor dish, boarding ramp). Scale ≈ 1 unit = 1 m (~34.75 m OAL). Original procedural mesh — not a licensed asset; film sets historically exceed exterior volume, so this prioritizes published deck landmarks over literal set measurements.

```powershell
dotnet run --project labs/avalonia/SceneLab/tools/CorellianFreighterBuilder -p:NovolisUseProjectReferences=true -- `
  labs/avalonia/SceneLab/samples/freighter-stages `
  labs/avalonia/SceneLab/samples/corellian-freighter.nov3djson
```
