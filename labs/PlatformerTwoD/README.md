# PlatformerTwoD

Same tile demo as **PlatformerHop**, but planar XZ via `PlanarAgent` and **Silk TwoD** drawing.

Dogfoods **Novolis.Rendering.TwoD**, **Novolis.Rendering.Backends.TwoD.Silk**, **Novolis.Rendering.Presentation.Abstractions**, **Novolis.Physics.Abstractions**, **Novolis.Simulation.Kinematics**, **Novolis.Simulation.World**, **Novolis.Math.*** (Arrays, Geometry), and in-repo **Novolis.Lab.TwoD**.

## Run

```powershell
cd novolis-lab
dotnet run --project labs/PlatformerTwoD
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
