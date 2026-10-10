# PlatformerPlanar

Same tile demo as **PlatformerHop**, but planar XZ via `PlanarAgent` and **Silk Planar** drawing.

Dogfoods **Novolis.Game.Scenes**, **Novolis.Silk**, **Novolis.Physics.Abstractions**, **Novolis.Simulation.Kinematics**, and **Novolis.Simulation.World**.

## Run

```powershell
cd novolis-lab
dotnet run --project labs/simulation/PlatformerPlanar
```

## Controls

| Input | Action |
|-------|--------|
| A / D | Move |
| Space / W | Jump |
| R | Reset level |

HUD shows position on the shared side-scroller layout.

## ProjectRef note

For local iteration against sibling repos, open `Novolis.Platform.slnx` or pass `-p:NovolisUseProjectReferences=true`. Committed `.csproj` files use `PackageReference` from GitHub Packages only.
