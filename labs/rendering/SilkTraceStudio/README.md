# SilkTraceStudio

Studio-style path-tracing showcase over **Novolis.Rendering.PathTrace.Demos** (Cpu / Ilgpu / Vulkan), **Novolis.Silk**, and **Novolis.Simulation.View**.

## Fast local run (ProjectRef)

Do **not** F5 against `Novolis.Lab.slnx` / `Novolis.Platform.slnx` — that restores the whole forest and can sit on Restore for a minute+.

Open the filtered solution, then run:

```powershell
dotnet run --project d:\novolis\novolis-lab\labs\rendering\SilkTraceStudio\SilkTraceStudio.csproj
```

`Directory.Build.rsp` forces ProjectRef and turns off NuGetAudit / SourceLink GitHub queries.

A clean Studio restore is a **~24-project ProjectRef graph** (typically several seconds for “Determining projects…”, not package download). The multi‑tens‑of‑seconds hangs happen when leftover MSBuild node-reuse / Rider / VS workers contend on NuGet — clear them first:

```powershell
dotnet build-server shutdown
```

Day-to-day after the first restore:

```powershell
dotnet run --project d:\novolis\novolis-lab\labs\rendering\SilkTraceStudio\SilkTraceStudio.csproj --no-restore
```

Or open `SilkTraceStudio.slnx` and F5.

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
