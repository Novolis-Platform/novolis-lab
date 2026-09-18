# RtsLiteTwoD

Top-down RTS on orthographic **TwoD** — shared sim types with **RtsLite** (sand field, tiberium patches, tank markers).

Dogfoods **Novolis.Rendering.TwoD**, **Novolis.Rendering.Backends.TwoD.Silk**, **Novolis.Rendering.Presentation.Abstractions**, **Novolis.Simulation.Kinematics**, **Novolis.Math.*** (Arrays, Geometry, Topology), and in-repo **Novolis.Lab.TwoD**.

## Run

```powershell
cd novolis-lab
dotnet run --project labs/RtsLiteTwoD
```

## Controls

| Input | Action |
|-------|--------|
| WASD | Pan camera |
| Wheel / +/- | Zoom |
| 1–5 | Select building type |
| B | Cancel build |
| LMB | Select / drag / place building |
| RMB | Move order |

Classic diagonal RA camera + sprites: run **RtsLite** (Raylib).

## ProjectRef note

For local iteration against sibling repos, open `Novolis.Platform.slnx` or pass `-p:NovolisUseProjectReferences=true`. Committed `.csproj` files use `PackageReference` from GitHub Packages only.
