# RtsLite

Pseudo-3D C&amp;C-style RTS lab **Novolis.Raylib** (+ Bindings, Game, Runtime), **Novolis.Math.Arrays**, **Novolis.Simulation.Kinematics**, **Novolis.Simulation.View**, and **Novolis.Simulation.World**.

Prefer orthographic top-down: see **RtsLiteTwoD**.

## Run

```powershell
cd novolis-lab
dotnet run --project labs/RtsLite
```

## Controls

| Input | Action |
|-------|--------|
| LMB | Select / drag box / place building |
| RMB | Move order / cancel build |
| MMB drag | Pan camera |
| Edge scroll / WASD | Pan |
| 1–5 | Select building type |
| B | Cancel build mode |

Build panel and minimap in HUD; PNG billboard sprites for structures.

## ProjectRef note

For local iteration against sibling repos, open `Novolis.Platform.slnx` or pass `-p:NovolisUseProjectReferences=true`. Committed `.csproj` files use `PackageReference` from GitHub Packages only.
