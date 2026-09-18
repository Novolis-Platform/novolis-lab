# ViewportBench

Compare CAD wireframe presenters with **one shared scene**, **one shared orbit camera**, and present-time benchmarks.

**Verdict / product choice:** use **OpenGL** for CAD and interactive 3D. Other panes are for regression and API parity only.

## Run

```powershell
dotnet run --project novolis-lab/labs/avalonia/ViewportBench -p:NovolisUseProjectReferences=true
dotnet run --project novolis-lab/labs/avalonia/ViewportBench -p:NovolisUseProjectReferences=true -- --lights
dotnet run --project novolis-lab/labs/avalonia/ViewportBench -p:NovolisUseProjectReferences=true -- --sample
```

Flags: `--gallery` (default), `--lights`, `--edit`, `--array`, `--boolean`, `--sample` / `--keel`.

## What it shows

2×2 grid: OpenGL (CAD default) | CPU / Vulkan | Raylib — same mesh edges, grid, light + camera gizmos.

- Orbit/zoom on **any** pane updates **all** panes (shared `SceneViewportCamera`).
- HUD: last / avg / max present ms and implied FPS per backend; **orbit avg/max** while the camera is moving.
- **Auto-orbit stress** forces continuous camera motion (Vulkan is throttled — full readback is not a CAD path).

## ProjectRef note

`-p:NovolisUseProjectReferences=true` does not transitively copy NuGet graphs of substituted projects. ViewportBench therefore PackageReferences `Silk.NET.OpenGL` / `Silk.NET.Vulkan` / Shaderc explicitly so those natives land in the output (same pattern as Raylib on SceneLab). Without them, OpenGL and Vulkan panes stay black at ~0 fps.
