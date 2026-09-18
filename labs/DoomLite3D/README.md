# DoomLite3D

First-person maze shooter lab **Novolis.Raylib**, **Novolis.Math.Arrays**, **Novolis.Math.Geometry**, **Novolis.Simulation.*** (World, View, Kinematics).

## Run

```powershell
cd novolis-lab
dotnet run --project labs/DoomLite3D
```

## Controls

| Input | Action |
|-------|--------|
| WASD | Move |
| Mouse | Look |
| Space | Jump |
| LMB / Ctrl | Shoot |
| R | Reload |
| F1 | Regenerate maze |
| F3 | Diagnostics overlay |
| Esc | Quit |

HUD shows ammo, health, minimap, and enemy count.

## ProjectRef note

For local iteration against sibling repos, open `Novolis.Platform.slnx` or pass `-p:NovolisUseProjectReferences=true`. Committed `.csproj` files use `PackageReference` from GitHub Packages only.
