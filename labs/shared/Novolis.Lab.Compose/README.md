# Novolis.Lab.Compose

In-repo shared library (not published). Bridges **Novolis.Simulation.View** poses to **Novolis.Rendering.Runtime** cameras without a Simulation↔Rendering package dependency.

## Consumers

Referenced by dogfood apps that need `ViewPose` → `CameraSnapshot` conversion (e.g. path-trace / viewport benches).

## API

- `ViewPoseRenderingBridge.ToCameraSnapshot(ViewPose, aspectRatio)`

## ProjectRef note

Same-repo `ProjectReference` only. Novolis packages resolve from GitHub Packages (`2026.1.*`) unless you build via `Novolis.Platform.slnx` or `-p:NovolisUseProjectReferences=true`.
