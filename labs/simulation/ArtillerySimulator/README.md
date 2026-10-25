# ArtillerySimulator

155 mm educational ballistics demo lab **Novolis.Raylib**, **Novolis.Physics.Ballistics**, **Novolis.Physics.Collision.Simple**, and **Novolis.Simulation.*** (World, Builders, View).

## Run

```powershell
cd novolis-lab
dotnet run --project labs/simulation/ArtillerySimulator
```

The window listens on `http://127.0.0.1:18770`. Snapshot `GET /agent/snapshot`, then `POST /agent/command` with `artillery.aim`, `artillery.charge`, `artillery.drag`, `artillery.fire`, `artillery.pace`, or `artillery.camera`. `--agent` runs the same gun and ground without the window.

## Controls

| Input | Action |
|-------|--------|
| Space | Fire. The follow camera rides the arc |
| Right mouse | Look around. The cursor stays visible |
| Shift / Ctrl | Raise / lower gun elevation |
| Q / E | Azimuth left / right |
| 1 / 2 / 3 | Charge tier |
| D | Toggle aerodynamic drag |
| , / . | Slower or faster flight |
| C | Toggle follow camera and freecam |
| H / M | Zoom the follow camera |
| F | Toggle flat vs procedural terrain |
| T | Cycle terrain style (when not flat) |
| R | Reseed terrain |
| WASD + mouse | Fly / look in freecam |

The opening view sits behind the piece. The card shows the predicted range before the shot and the impact range after it. The ground mesh is the original heightfield.

## ProjectRef note

For local iteration against sibling repos, open `Novolis.Platform.slnx` or pass `-p:NovolisUseProjectReferences=true`. Committed `.csproj` files use `PackageReference` from GitHub Packages only.
