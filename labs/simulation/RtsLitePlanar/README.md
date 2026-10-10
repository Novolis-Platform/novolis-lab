# RtsLitePlanar

Top-down RTS on orthographic **Planar** — shared sim types with **RtsLite** (sand field, tiberium patches, tank markers).

Dogfoods **Novolis.Game.Scenes**, **Novolis.Silk**, and **Novolis.Simulation.Kinematics**.

## Run

```powershell
cd novolis-lab
dotnet run --project labs/simulation/RtsLitePlanar
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
