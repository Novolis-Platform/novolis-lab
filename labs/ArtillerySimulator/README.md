# ArtillerySimulator

155 mm educational ballistics demo lab **Novolis.Raylib**, **Novolis.Physics.Ballistics**, **Novolis.Physics.Collision.Simple**, and **Novolis.Simulation.*** (World, Builders, View).

## Run

```powershell
cd novolis-lab
dotnet run --project labs/ArtillerySimulator
```

## Controls

| Input | Action |
|-------|--------|
| Shift / Ctrl | Raise / lower gun elevation |
| Q / E | Azimuth left / right |
| 1 / 2 / 3 | Charge tier |
| D | Toggle aerodynamic drag |
| Space | Fire (when not in flight) |
| C | Toggle freecam / orbit camera |
| F | Toggle flat vs procedural terrain |
| T | Cycle terrain style (when not flat) |
| R | Reseed terrain |
| WASD + mouse | Fly / look in freecam |

On-screen HUD shows elevation, charge, atmosphere, trajectory, and impact stats.

## ProjectRef note

For local iteration against sibling repos, open `Novolis.Platform.slnx` or pass `-p:NovolisUseProjectReferences=true`. Committed `.csproj` files use `PackageReference` from GitHub Packages only.
