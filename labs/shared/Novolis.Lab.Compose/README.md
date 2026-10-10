# Novolis.Lab.Compose

In-repo shared library (not published). Compose-layer bridges that must not live in Simulation, Rendering, or Modeling.

## Consumers

Referenced by dogfood apps that need `ViewPose` → `CameraSnapshot` or `.nov3djson` → path-trace `Scene` conversion.

## API

- `ViewPoseRenderingBridge.ToCameraSnapshot(ViewPose, aspectRatio)`
- `SceneDocumentRenderingBridge.ToScene(SceneDocument | LookCache)` — meshes + lights only; cameras stay `ViewPose` / `CameraNode`

## ProjectRef note

Same-repo `ProjectReference` only. Novolis packages resolve from GitHub Packages (`2026.1.*`) unless you build via `Novolis.Platform.slnx` or `-p:NovolisUseProjectReferences=true`.
