<!-- novolis-marketing:start -->
<p align="center">
  <a href="https://github.com/Novolis-Platform">
    <img src="https://raw.githubusercontent.com/Novolis-Platform/.github/main/brand/logo-brand-transparent.svg" width="360" alt="Novolis"/>
  </a>
</p>

<p align="center">
  <img src="https://raw.githubusercontent.com/Novolis-Platform/.github/main/brand/banners/novolis-lab.svg" width="100%" alt="novolis-lab"/>
</p>

<p align="center">
  <strong>Fast integration labs</strong><br/>
  Quick experiments, package demos, smokes, and benchmarks for the Novolis platform.
</p>

<p align="center">
  <a href="https://novolis-platform.github.io/.github/novolis-lab/"><img src="https://img.shields.io/badge/docs-portfolio-0a7ea3" alt="docs"/></a>
  <a href="https://github.com/Novolis-Platform/novolis-lab/actions"><img src="https://img.shields.io/github/actions/workflow/status/Novolis-Platform/novolis-lab/merge.yml?branch=main&label=merge&logo=github" alt="merge"/></a>
  <a href="https://github.com/orgs/Novolis-Platform/packages?repo_name=novolis-lab"><img src="https://img.shields.io/badge/packages-GitHub%20Packages-0a7ea3?logo=nuget" alt="packages"/></a>
  <a href="https://github.com/Novolis-Platform"><img src="https://img.shields.io/badge/org-Novolis--Platform-111827" alt="org"/></a>
</p>

<p align="center">
  <a href="https://novolis-platform.github.io/.github/novolis-lab/">Docs</a>
  ·
  <a href="https://nuget.pkg.github.com/Novolis-Platform/index.json"><code>https://nuget.pkg.github.com/Novolis-Platform/index.json</code></a>
  ·
  <a href="https://github.com/Novolis-Platform/.github/blob/main/profile/README.md">Org landing</a>
  ·
  <a href="https://github.com/Novolis-Platform/novolis-governance">Governance</a>
</p>

---
<!-- novolis-marketing:end -->
# novolis-lab

Integration workspace that **consumes published Novolis packages** from [GitHub Packages](https://github.com/orgs/Novolis-Platform/packages) (`PackageReference` only).

This repo does not publish packages. Pull-request and merge CI build only changed labs; the lab is for local integration against what is already on the feed.

Per-app READMEs live under `labs/<name>/README.md` (see also [labs/README.md](labs/README.md) for a short index).

## Quick start

```powershell
git clone https://github.com/Novolis-Platform/novolis-lab.git
cd novolis-lab

# One-time per machine: user NuGet.Config (not repo nuget.config)
..\novolis-governance\scripts\configure-gpr-user-nuget.ps1

dotnet restore
dotnet build --no-restore
dotnet run --project labs/MathGridDemo
```

Feed: `https://nuget.pkg.github.com/Novolis-Platform/index.json` (see `nuget.config`).

Novolis package versions use floating `2026.1.*` in `Directory.Packages.props`. Org setup: [github-packages-org-settings.md](../novolis-governance/docs/github-packages-org-settings.md).

If restore returns 401, re-run `configure-gpr-user-nuget.ps1` (credentials live in `%APPDATA%\NuGet\NuGet.Config`).

**Local iteration:** open `Novolis.Platform.slnx` or pass `-p:NovolisUseProjectReferences=true` when building/running apps against sibling checkouts. Committed consumers use GitHub Packages only.

## Selected library checkouts

The lab is empty of library submodules by default. Add only the library needed for an experiment:

```powershell
pwsh -File scripts/Add-LabLibrary.ps1 -Repo novolis-logging
pwsh -File scripts/Sync-LabLibraries.ps1
```

If `d:\novolis\novolis-logging` already exists, synchronization uses that sibling checkout and does not clone a second copy. For a recorded submodule checkout, set `-p:NovolisLibraryRoot=.../submodules` when enabling ProjectReference mode. Lab project files must remain `PackageReference`-only; CI does not initialize submodules.

Remove a recorded checkout with `scripts/Remove-LabLibrary.ps1`. Create a new experiment with `scripts/New-Lab.ps1 -Name FooLab -Stack console|avalonia|raylib|spectre`.

## Labs

| App | Folder | Novolis packages exercised |
|-----|--------|---------------------------|
| `MathGridDemo` | `labs/MathGridDemo` | Math.Arrays |
| `RaylibHello` | `labs/RaylibHello` | Raylib |
| `HelloGame` … `HelloRaygui` | `labs/raylib/Hello*` | Raylib API walkthroughs |
| `RenderingAvalonia` | `labs/avalonia/RenderingAvalonia` | Avalonia.Rendering + Avalonia.Raylib |
| `MobilityLab` | `labs/avalonia/MobilityLab` | Tax–mobility Civics/Economy/Geopolitics Avalonia UI |
| `MovieMakerLab` | `labs/avalonia/MovieMakerLab` | Video.Edit full demo (images/audio/transitions/text/export) |
| `MusicMakerLab` | `labs/avalonia/MusicMakerLab` | Audio.Edit multi-track (library/waveforms/fades/export) |
| `MinimalWorkspaceTimeline` | `labs/workspaces/MinimalWorkspaceTimeline` | Workspaces + Timeline |
| `ProjectTimelineBench` | `labs/workspaces/ProjectTimelineBench` | Workspaces.Projects.Timeline |
| `TypedSolutionIntelligence` | `labs/workspaces/TypedSolutionIntelligence` | Workspaces.DotNet catalog → generated typed project/namespace/type façade |
| `XFighter` | `labs/raylib/XFighter` | Raylib, Audio (Core, Effects, Playback, Voice) |
| `ArtillerySimulator` | `labs/ArtillerySimulator` | Raylib, Physics.Ballistics, Physics.Collision, Simulation |
| `BouncingBall` | `labs/BouncingBall` | Raylib, Math.Arrays, Simulation, Physics.Collision |
| `DoomLite3D` | `labs/DoomLite3D` | Raylib, Math, Simulation (World, View, Kinematics) |
| `RagdollPlay` | `labs/RagdollPlay` | Raylib, Simulation, Physics.Joints, Physics.Collision |
| `ClothPlay` | `labs/ClothPlay` | Raylib, Simulation, Physics.Joints cloth sheet, Physics.Collision |
| `RandoriFight` | `labs/RandoriFight` | Raylib, Simulation.View, Simulation.Humanoid |
| `PlatformerHop` | `labs/PlatformerHop` | Raylib, Simulation.Kinematics, Simulation.View |
| `PlatformerTwoD` | `labs/PlatformerTwoD` | Rendering.TwoD, Backends.TwoD.Silk, Simulation |
| `RtsLite` | `labs/RtsLite` | Raylib, Simulation (Kinematics, View, World) |
| `RtsLiteTwoD` | `labs/RtsLiteTwoD` | Rendering.TwoD, Backends.TwoD.Silk, Simulation.Kinematics |
| `RaytraceHello` | `labs/rendering/RaytraceHello` | Raylib.Game, Rendering (ILGPU + DI + Presentation.Raylib) |
| `SilkTraceHello` | `labs/rendering/SilkTraceHello` | Rendering (env backend + PathTrace.Demos + Presentation.Silk) |
| `SilkTraceStudio` | `labs/rendering/SilkTraceStudio` | Rendering backends + PathTrace.Demos + Presentation.Silk |
| `SilkTwoDHello` | `labs/rendering/SilkTwoDHello` | Rendering.TwoD, Backends.TwoD.Silk |
| `MeshBench` (Mesh Studio) | `labs/rendering/MeshBench` | Workspaces, Timeline, Snapshots, Rendering, Audio |
| `GamingSmoke` | `labs/gaming/GamingSmoke` | Game.Identity, Game.MenuFlows, Game.Multiplayer.Abstractions |
| `TopDownDoom` | `labs/gaming/TopDownDoom` | Rendering.TwoD, Game flows |
| `TapDuelFootball` | `labs/gaming/TapDuelFootball` | Rendering.TwoD, Game.MenuFlows — hotseat tap duel |
| `NeuralRacing` | `labs/NeuralRacing` | Simulation.Racing, MachineLearning.Neural |
| `VoiceSmoke` | `labs/audio/VoiceSmoke` | Audio.Voice, Voice.Atc (Sherpa Piper TTS) |
| `StudioChromeLab` | `labs/avalonia/StudioChromeLab` | Controls dialogs/lists/jobs + Studio focus/dirty chrome |
| `AvaloniaAgentMcp` | `labs/AvaloniaAgentMcp` | Avalonia.Agent.Protocol, Transports.LocalIpc, Agent.Core/Surface |
| `SketchLab` | `labs/avalonia/SketchLab` | SketchControl freehand canvas + PNG/SVG export |
| `ViewportBench` | `labs/avalonia/ViewportBench` | Shared-camera CAD wireframe (OpenGL/CPU/Vulkan/Raylib) |
| `SceneLab` | `labs/avalonia/SceneLab` | Avalonia 3D scene lab |
| `HumanoidLab` | `labs/avalonia/HumanoidLab` | Simulation.Humanoid, Humanoid.Physics |
| `CharacterLab` | `labs/avalonia/CharacterLab` | Drill/salute rig + character/rifle parade scene |
| `KatoriLab` | `labs/avalonia/KatoriLab` | TSKSR-inspired kenjutsu wire + bokken hold IK |
| `KatoriLab.Tests` | `labs/avalonia/KatoriLab.Tests` | Kata correctness (timeline, holds, walk hang) |
| `FriendLab` | `labs/avalonia/FriendLab` | Find-a-Friend prototype — multi-window users, 3-of-5 interests + geo |
| `CalypsoCad` | `labs/cad/CalypsoCad` | CAD deckplan generation |
| `CalypsoInternalsCad` | `labs/cad/CalypsoInternalsCad` | CAL-INT drawings → CAD + OBJ 3D |
| `AstroSmoke` | `labs/astro/AstroSmoke` | Astro catalog/routing/assessment/overlay/plotting |
| `StarMapLab` | `labs/astro/StarMapLab` | Avalonia.StarMap + Astro route planner |
| `EconomyBoard` | `labs/economy/EconomyBoard` | Economy kernel — Avalonia board |
| `TrampFreighterPlay` | `labs/economy/TrampFreighterPlay` | Economy logistics — interactive Spectre |
| `TrampFreighterSim` | `labs/economy/TrampFreighterSim` | Economy logistics — observer Spectre |
| `NearSolPolity` | `labs/economy/NearSolPolity` | Astro catalog bridged to Economy |
| `PolityTriad` | `labs/civics/PolityTriad` | Civics + Economy + Geopolitics composed month |
| `IoSmoke` | `labs/io/IoSmoke` | IO.Paths, Recovery, Watching, Processes, Git |
| `ManuscriptSmoke` | `labs/manuscript/ManuscriptSmoke` | Markup.Manuscript, Voice.Manuscript |
| `BridgeCommander` | `labs/BridgeCommander` | Commands + Audio.Voice (Spectre console) |

The first utility wave graduated to
`d:\novolis\novolis-utilities`: Adb, WireFish, Torrent, and VoiceStudio.

## Graduation

Labs do not ship. Use `scripts/Graduate-Lab.ps1 -Name FooLab -To tools|utilities|apps` to validate shared-project references and emit a checklist plus destination manifest fragment. The script does not copy or rewrite files. Keep experiments here until a destination repository accepts the host.

## Shared in-repo libraries

| Library | Folder | Purpose |
|---------|--------|---------|
| `Novolis.Lab.Compose` | `labs/shared/Novolis.Lab.Compose` | ViewPose → rendering camera bridge |
| `Novolis.Lab.TwoD` | `labs/shared/Novolis.Lab.TwoD` | TwoD platform/camera helpers |
| `Novolis.Lab.Voice` | `labs/shared/Novolis.Lab.Voice` | ATC voice DI for demos |

