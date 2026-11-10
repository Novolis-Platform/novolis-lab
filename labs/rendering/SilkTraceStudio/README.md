# SilkTraceStudio

Studio-style path-tracing showcase over **Novolis.Rendering.PathTrace.Demos** (Cpu / Ilgpu / Vulkan), **Novolis.Silk**, and **Novolis.Simulation.View**.

The default scene is the **Materials capability diorama**: every `MaterialPresets.Showcase` look (Cornell walls, checker tiles, matte/metal/glass/skin, cyan lightsaber + magenta neon, softbox) compiled through `Novolis.Rendering.Materials` → path-trace NEE.

## Fast local run

**Do not** run `dotnet … --project d:\…\SilkTraceStudio.csproj` from `C:\Users\frank` (or any cwd without a Novolis `global.json`). That picks **SDK 11 preview** instead of lab-pinned **10.0.403** and makes Restore much slower.

```powershell
pwsh -File d:\novolis\novolis-lab\labs\rendering\SilkTraceStudio\run.ps1
```

Or:

```powershell
Set-Location d:\novolis\novolis-lab\labs\rendering\SilkTraceStudio
dotnet run
```

After the first restore in a session:

```powershell
Set-Location d:\novolis\novolis-lab\labs\rendering\SilkTraceStudio
dotnet run --no-restore
```

Do **not** F5 against `Novolis.Lab.slnx` / `Novolis.Platform.slnx` — that restores the whole forest.

## Why Restore was 20–90s (measured)

| Situation | SilkTraceStudio restore |
| --- | --- |
| cwd = Studio dir (SDK 10), machine quiet, node reuse off | **~3s** |
| cwd = `C:\Users\frank` (SDK 11 preview), quiet | **~7s** |
| Parallel restores / Platform.slnx + MSBuild node farm (~100 `dotnet` procs) | **~27s+** (UI shows “Restore” the whole time) |

Timestamped logs under contention show: print `Determining projects to restore…`, then **~20s of silence**, then `_GenerateProjectRestoreGraph` for the 24 ProjectRef projects, then “up-to-date” in milliseconds. **No package download** in that gap — it is CPU/scheduler contention evaluating the restore graph.

Orphan MSBuild workers (`/nodemode:1 /nodeReuse:true`) from SDK 10 **and** 11 accumulate because node reuse is on by default and parents exit. Clear with:

```powershell
dotnet build-server shutdown
```

`Directory.Build.rsp` + launch profile set ProjectRef, disable NuGetAudit/SourceLink GitHub queries, and disable node reuse for this host.

## Controls

| Input | Action |
| --- | --- |
| LMB drag | Orbit camera |
| Scroll | Zoom |
| Space | Toggle auto-orbit |
| B | Cycle ILGPU ↔ CPU |
| 1 / 2 / 3 | ILGPU / Vulkan shim / CPU |
| R | Reset accumulation |

Title bar shows backend, samples, FPS, and control hints. Console gets a 5s `ILogger` perf pulse.

## Headless still

```powershell
dotnet run --project d:\novolis\novolis-lab\labs\rendering\SilkTraceStudio\SilkTraceStudio.csproj -p:NovolisUseProjectReferences=true -c Release -- --headless-png d:\novolis\novolis-lab\labs\rendering\SilkTraceStudio\docs\studio-diorama.png --width 960 --height 540 --samples 256
```
