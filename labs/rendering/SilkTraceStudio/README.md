# SilkTraceStudio

Studio-style path-tracing showcase over **Novolis.Rendering.PathTrace.Demos** (Cpu / Ilgpu / Vulkan), **Novolis.Silk**, and **Novolis.Simulation.View**.

## Fast local run (ProjectRef)

Do **not** F5 against `Novolis.Lab.slnx` / `Novolis.Platform.slnx` — that restores the whole forest and can sit on Restore for a minute+.

Open the filtered solution, then run:

```powershell
dotnet run --project d:\novolis\novolis-lab\labs\rendering\SilkTraceStudio\SilkTraceStudio.csproj -p:NovolisUseProjectReferences=true
```

Or open `d:\novolis\novolis-lab\labs\rendering\SilkTraceStudio\SilkTraceStudio.slnx` in Cursor and F5 (launch profile sets ProjectRef + skips NOV2301).

## Controls

| Input | Action |
|-------|--------|
| LMB drag | Orbit camera |
| Scroll | Zoom |
| Space | Toggle auto-orbit |
| B | Cycle ILGPU ↔ CPU |
| 1 / 2 / 3 | ILGPU / Vulkan shim / CPU |
| R | Reset accumulation |

Title bar shows backend, samples, FPS, and control hints.
