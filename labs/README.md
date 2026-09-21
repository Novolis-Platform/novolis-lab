# Lab hosts

Small experimental hosts that consume **published Novolis packages** from GitHub Packages (`PackageReference` in each `.csproj`).

Add a project under `labs/` (or `labs/<repo>/` for grouped labs like `rendering/`), declare packages in `Directory.Packages.props`, and register it in `Novolis.Lab.slnx` under the solution folder for the **primary** Novolis repo it exercises (`/raylib/`, `/rendering/`, `/simulation/`, …).

API walkthroughs (`HelloGame`, `HelloRuntime`, …) live under `labs/raylib/Hello*`. Library repos keep packable `src/`, tests, and `tools/` only — no `samples/` or product hosts.

## Typed Solution Intelligence

Console walkthrough of `Novolis.Workspaces.DotNet.*`: typed directory root, Git relation, SLNX
topology, optional MSBuild evaluation, Roslyn facts, then a **compile-time** typed
exploration façade (`solution.Projects.<Project>.<Namespace>.<Type>` as `SemanticType`).
No arguments opens the committed two-project fixture and binds
`solution.Projects.DemoLib.Services.IdentityService` without `dynamic`.

```powershell
dotnet run --project d:\novolis\novolis-lab\labs\workspaces\TypedSolutionIntelligence\TypedSolutionIntelligence.csproj -p:NovolisUseProjectReferences=true
dotnet run --project d:\novolis\novolis-lab\labs\workspaces\TypedSolutionIntelligence\TypedSolutionIntelligence.csproj -p:NovolisUseProjectReferences=true -- d:\novolis\novolis-workspaces\Novolis.Workspaces.slnx --semantic --framework net10.0
```

## FriendLab

Multi-window Find-a-Friend prototype (3-of-5 interest overlap + geo radius). Control window opens one Avalonia window per simulated app user.

```bash
dotnet run --project labs/avalonia/FriendLab
```

## SketchLab

Freehand integration host for `Novolis.Avalonia.Controls` `SketchControl`. Clipboard export only (transparent PNG or SVG text).

```bash
dotnet run --project labs/avalonia/SketchLab -p:NovolisUseProjectReferences=true
```

## ViewportBench

Same CAD wireframe on OpenGL / CPU / Vulkan / Raylib with one shared orbit camera and present-time HUD (idle + orbit motion).

```bash
dotnet run --project labs/avalonia/ViewportBench -p:NovolisUseProjectReferences=true
dotnet run --project labs/avalonia/ViewportBench -p:NovolisUseProjectReferences=true -- --lights
```

## Calypso CAD

Hand-ported Rev G deckplans → `.cadjson` / `.cadshapejson` / `.cadlayers.json`, with two-sided walls and interior views.

```bash
dotnet run --project labs/cad/CalypsoCad
dotnet run --project labs/cad/CalypsoCad -- --generate-only
```

## Calypso Internals CAD

CAL-INT lock + manufacturer hull → Novolis CAD companions + Wavefront OBJ (optional Raylib orbit view).

```powershell
dotnet run --project d:\novolis\novolis-lab\labs\cad\CalypsoInternalsCad\CalypsoInternalsCad.csproj -p:NovolisUseProjectReferences=true
dotnet run --project d:\novolis\novolis-lab\labs\cad\CalypsoInternalsCad\CalypsoInternalsCad.csproj -p:NovolisUseProjectReferences=true -- --view
```

## FreightWing

X-Wing Alliance–inspired dual-role campaign (freighter → X-wing transfer). Bake content from a local Steam install via the **local-only** `novolis-experimental` `Xwa.Cli` tree (not on GitHub), then run the app (no Steam/experimental at runtime).

```powershell
$env:XWA_INSTALL_DIR = "D:\Steam\steamlabs\common\Star Wars X-Wing Alliance"
dotnet run --project d:\novolis\novolis-experimental\src\Novolis.Experimental.Xwa.Cli -- all --out d:\novolis\novolis-lab\labs\raylib\FreightWing\Content
dotnet run --project d:\novolis\novolis-lab\labs\raylib\FreightWing -p:NovolisUseProjectReferences=true
```

## SilkTwoDHello

Orthographic 2D sample (`Rendering.TwoD` + Silk): platforms, `TwoDCollisionWorld`, HUD, menus.

```bash
dotnet run --project labs/rendering/SilkTwoDHello
```

## PlatformerTwoD

Same tile demo as PlatformerHop, but **planar XZ** via `PlanarAgent` and **Silk TwoD** drawing (pairs with Raylib `PlatformerHop`).

```bash
dotnet run --project labs/PlatformerTwoD
```

## RtsLiteTwoD

Top-down RTS on **orthographic TwoD** (shared sim with `RtsLite`; sand field + tiberium patches, tank markers). **Mouse:** LMB select, RMB orders. Classic **diagonal RA camera + sprites:** `RtsLite` (Raylib).

```bash
dotnet run --project labs/RtsLiteTwoD
```

## RtsLite (Raylib)

Pseudo-3D C&amp;C-style camera + building sprites — kept for Raylib/billboard experimentation.

```bash
dotnet run --project labs/RtsLite
```

## IoSmoke

```bash
dotnet run --project labs/io/IoSmoke
```

- `IoSmoke` — Paths, Recovery, Watching, Processes, Git (`labs/io/IoSmoke/README.md`)

The ADB utility now lives in
`d:\novolis\novolis-utilities\src\Adb\Adb.csproj`.

## Tap Duel Football

Portrait hotseat tap-tug football (`Rendering.TwoD` + `Game.MenuFlows`), recreation of [tap-duel-football](https://github.com/frankhaugen/tap-duel-football).

```powershell
dotnet run --project labs/gaming/TapDuelFootball -p:NovolisUseProjectReferences=true
```

## PulseStrip

Anti-grav spline-circuit racer (Wipeout homage): weapons/boost, procedural FX/SFX, evolutionary ML opponents. Windows + Linux; Android deferred (see app README).

```powershell
dotnet run --project d:\novolis\novolis-lab\labs\raylib\PulseStrip -p:NovolisUseProjectReferences=true
dotnet run --project d:\novolis\novolis-lab\labs\raylib\PulseStrip -p:NovolisUseProjectReferences=true -- --smoke
```

## WorkflowEngineLab

Deterministic generic-host workflow showing a channel trigger, typed transform,
and terminal sink backed by `Novolis.WorkflowEngine`.

```powershell
dotnet run --project d:\novolis\novolis-lab\labs\workflows\WorkflowEngineLab\WorkflowEngineLab.csproj -p:NovolisUseProjectReferences=true
```
