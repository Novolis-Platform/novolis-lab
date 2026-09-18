# Design

## Purpose

`novolis-lab` validates the Novolis ecosystem **as package consumers**: library repos publish to **GitHub Packages**; this repo restores those packages via `PackageReference` only.

## Non-goals

- Publishing NuGet packages from this repository
- Replacing per-repo unit/integration tests
- `ProjectReference` into sibling `novolis-*` clones (use GitHub Packages instead)
- Local `artifacts/nuget-local` as the primary feed

## Package source

| Source | URL |
|--------|-----|
| GitHub Packages | `https://nuget.pkg.github.com/Novolis-Platform/index.json` |
| nuget.org | Third-party dependencies only (`packageSourceMapping` in `nuget.config`) |

Version pin: `Directory.Packages.props` → floating `2026.1.*` for Novolis packages.

## Local development

1. Once per machine: `..\novolis-governance\scripts\configure-gpr-user-nuget.ps1` (writes `%APPDATA%\NuGet\NuGet.Config` from `gh auth token`)
2. `dotnet restore` then `dotnet build` (Rider: normal **Build Solution**)

## CI

Authenticate with `GITHUB_TOKEN`, `dotnet restore` from `nuget.config`, then build `Novolis.Lab.slnx`.

## 2D rendering dogfood

`Novolis.Rendering.TwoD` + `Novolis.Rendering.Backends.TwoD.Silk` are consumed from GitHub Packages. See [two-d-simulation-gaps.md](two-d-simulation-gaps.md) for Simulation integration and coordinate conventions.

## Compose helpers

`Novolis.Lab.Compose` provides app-layer bridges (e.g. `ViewPose` → `CameraSnapshot`) without forbidden package references between Simulation and Rendering.

## Related

- [two-d-simulation-gaps.md](two-d-simulation-gaps.md)
- [nuget-setup.md](../../novolis-governance/docs/nuget-setup.md)
- [release-policy.md](../../novolis-governance/docs/release-policy.md)
