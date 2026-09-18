# Novolis.Lab.TwoD

In-repo shared library (not published). Helpers for orthographic **TwoD** dogfood apps.

Dogfoods **Novolis.Rendering.TwoD**, **Novolis.Math.Arrays**, **Novolis.Math.Geometry**, **Novolis.Math.Topology**.

## Consumers

- **PlatformerTwoD**, **RtsLiteTwoD**, and other Silk TwoD samples

## API

- `DenseGridPlatforms` — build platforms/colliders from occupancy grids
- `OrthoPanCamera` — top-down pan + zoom for RTS-style views

## ProjectRef note

Same-repo `ProjectReference` only. Novolis packages resolve from GitHub Packages (`2026.1.*`) unless you build via `Novolis.Platform.slnx` or `-p:NovolisUseProjectReferences=true`.
