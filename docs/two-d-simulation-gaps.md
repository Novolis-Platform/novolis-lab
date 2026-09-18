# TwoD rendering + Simulation — dogfood gaps

Orthographic 2D lives in **`Novolis.Rendering.TwoD`** (+ **`Backends.TwoD.Silk`**). Simulation stays **BCL `Vector3` / `Quaternion`** on the **XZ plane** (`Y = 0`). Do not add `Vector2`, suffix-3 types, or TwoD types to Math or Physics.

## Dogfood apps (new)

| App | Stack | Purpose |
|-----|-------|---------|
| `labs/rendering/SilkTwoDHello` | Rendering.TwoD + Silk | Collision, platforms, HUD, menus — no Simulation |
| `labs/PlatformerTwoD` | Simulation.Kinematics + World + Rendering.TwoD | Same tile demo as `PlatformerHop`, planar XZ via `PlanarAgent` |
| `labs/RtsLiteTwoD` | Shared RTS sim + Rendering.TwoD | Top-down orthographic RTS (Raylib `RtsLite` = pseudo-3D + PNG billboards) |

Run:

```bash
dotnet run --project labs/rendering/SilkTwoDHello
dotnet run --project labs/PlatformerTwoD
dotnet run --project labs/RtsLiteTwoD
```

| Raylib (3D-style) | TwoD (orthographic) |
|-------------------|---------------------|
| `PlatformerHop` | `PlatformerTwoD` |
| `RtsLite` | `RtsLiteTwoD` |

Shared helpers: `labs/shared/Novolis.Lab.TwoD` (`DenseGridPlatforms`, `OrthoPanCamera`).

## Side-view vs planar XZ

| Convention | Horizontal | Vertical | Depth |
|------------|------------|----------|-------|
| Raylib `PlatformerHop` | `Vector3.X` | `Vector3.Y` | `Z ≈ 0` |
| Simulation + TwoD | `Vector3.X` | `Vector3.Z` | `Y = 0` |

Bridge at the **app layer** only (no Simulation → Rendering package reference):

```csharp
// Side-view position (Raylib) → planar (Simulation / TwoD)
static Vector3 SideToPlanar(Vector3 side) => new(side.X, 0f, side.Y);
```

`PlanarHopPlayer` uses `PlanarAgent.Move` + `SideLevel` grid where grid row `y` maps to world **Z**.

## Simulation: what exists vs gaps

### Already aligned (XZ, BCL)

| Package | API | Role for 2D |
|---------|-----|-------------|
| `Novolis.Simulation.Kinematics` | `PlanarAgent.Move` | Grid + optional BVH sweep on XZ |
| `Novolis.Simulation.World` | `PlanarOccupancy` | Circle vs `DenseGrid<byte>` |
| `Novolis.Simulation.View` | `ViewPose`, orbit/free rigs | **3D** cameras — not orthographic 2D |

### Gaps (no new Math/Physics types)

1. **No side-view camera in Simulation.View** — `ViewPose` targets perspective 3D. Orthographic 2D uses `TwoDViewport` in Rendering at compose time. Optional future: document-only “side rig” recipe in dogfood, not a new Simulation type.
2. **Duplicate collision paths** — `PlanarOccupancy` (Simulation) vs `TwoDCollisionWorld` (Rendering). Apps should pick one per game: PlatformerTwoD uses Simulation for motion; SilkTwoDHello uses Rendering collision only.
3. **No Simulation → TwoD scene builder** — tile grids are app-wired (`AddPlatform` per cell). A shared **dogfood helper** (not a platform package) could emit platforms from `DenseGrid<byte>` if more 2D apps appear.
4. **ViewPose bridge** — still app-only for path tracing ([simulation-viewpose-to-rendering-bridge](../../novolis-governance/docs/imports-todo/internal-novolis-audit/simulation-viewpose-to-rendering-bridge.md)); irrelevant for orthographic TwoD.
5. **NeuralRacing / headless sims** — no visualization; 2D would be a new app if needed.
6. **RtsLite / top-down** — `RtsLiteTwoD` dogfoods orthographic RTS; PNG billboards still Raylib-only in `RtsLite`.
7. **Assets** — no committed PNGs under lab for `SilkTwoDPngLoader`; polygon/HUD dogfood works without art.

### Explicit non-goals

- `Vector2`, `Vector3d`, or planar types in Math/Physics
- `Novolis.Simulation.*` referencing `Novolis.Rendering.*`
- `Quaternion` helpers for 2D (Y-axis spin only if ever needed — use BCL `Quaternion` in apps)

## Avalonia hosts

Package **`Novolis.Avalonia.Rendering`** (`novolis-avalonia`):

| Control | Renders |
|---------|---------|
| `TwoDSceneControl` | `TwoDScene` via OpenGL (`SilkTwoDRenderer`) |
| `Rgba32FrameControl` | CPU `Rgba32` frames (`IFramePresenter`) — path trace preview |

Sample: `novolis-lab/labs/avalonia/RenderingAvalonia`. PackageReference `Novolis.Avalonia.Rendering` (no cross-repo `ProjectReference`).

## Dropped: TerraFX

Low-level GPU interop (VMA, D3D12) was considered and **not** adopted — Novolis stays on **Silk.NET** for Vulkan/OpenGL presenters.

## Rendering gaps (for follow-up)

- **Raylib TwoD backend** — not planned; Silk-only host for `Rendering.TwoD`
- **Procedural / solid-color sprites** without PNG — today use `TwoDStaticPolygon` or register a 1×1 texture
- **PlatformerHop axis** — migrating the Raylib app would require coordinate remap or a documented side-view profile

## Related

- [design-two-d.md](../../novolis-rendering/docs/design-two-d.md)
- [library-boundaries.md](../../novolis-governance/docs/library-boundaries.md)
