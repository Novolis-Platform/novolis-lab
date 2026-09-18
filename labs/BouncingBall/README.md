# BouncingBall

Wireframe room with stacked physics spheres lab **Novolis.Raylib**, **Novolis.Math.Arrays**, **Novolis.Simulation.World** (+ Builders), and **Novolis.Physics.Collision.Simple**.

## Run

```powershell
cd novolis-lab
dotnet run --project labs/BouncingBall
```

## Controls

| Input | Action |
|-------|--------|
| B | Spawn one ball |
| Ctrl+B | Spawn 10 balls |
| Ctrl+Shift+B | Spawn 100 balls |
| R | Clear and respawn one ball |
| F3 | Toggle diagnostics overlay |

Fixed camera; balls bounce and collide in the room.

## ProjectRef note

For local iteration against sibling repos, open `Novolis.Platform.slnx` or pass `-p:NovolisUseProjectReferences=true`. Committed `.csproj` files use `PackageReference` from GitHub Packages only.
