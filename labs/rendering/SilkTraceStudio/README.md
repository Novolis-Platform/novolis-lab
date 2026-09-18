# SilkTraceStudio

Studio-style path-tracing showcase lab **Novolis.Rendering.Backends.*** (Cpu, Ilgpu, Vulkan), **Novolis.Rendering.PathTrace.Demos**, **Novolis.Rendering.Presentation.*** (Abstractions, Silk), **Novolis.Rendering.Runtime**, and **Novolis.Simulation.View** (orbit rig).

Glass/emissive studio scene with status strip, FPS smoothing, and hot backend switching.

## Run

```powershell
cd novolis-lab
dotnet run --project labs/rendering/SilkTraceStudio
```

## Controls

| Input | Action |
|-------|--------|
| LMB drag | Orbit camera |
| Scroll | Zoom |
| Space | Toggle auto-orbit |
| B | Cycle ray-tracing backend |
| 1 / 2 / 3 | ILGPU / Vulkan / CPU |
| R | Reset accumulation |

Title bar shows backend, samples, FPS, and control hints.

## ProjectRef note

For local iteration against sibling repos, open `Novolis.Platform.slnx` or pass `-p:NovolisUseProjectReferences=true`. Committed `.csproj` files use `PackageReference` from GitHub Packages only.
