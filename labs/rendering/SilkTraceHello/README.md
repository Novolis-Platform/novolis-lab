# SilkTraceHello

Path-traced hello scene on **Silk** lab **Novolis.Rendering.DependencyInjection** (env backend selection), **Novolis.Rendering.PathTrace.Demos**, and **Novolis.Rendering.Presentation.Silk**.

## Run

```powershell
cd novolis-lab
dotnet run --project labs/rendering/SilkTraceHello
```

Backend is chosen from environment via `AddRayTracingFromEnvironment()` (same family of `NOVOLIS_RAY_BACKEND` / device vars as other rendering demos).

## Controls

| Input | Action |
|-------|--------|
| Space | Toggle auto-orbit |
| R | Reset accumulation |

Window title updates with backend label, sample count, and orbit mode.

## ProjectRef note

For local iteration against sibling repos, open `Novolis.Platform.slnx` or pass `-p:NovolisUseProjectReferences=true`. Committed `.csproj` files use `PackageReference` from GitHub Packages only.
