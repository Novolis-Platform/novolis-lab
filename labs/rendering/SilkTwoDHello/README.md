# SilkTwoDHello

Orthographic 2D platformer sample lab **Novolis.Rendering.TwoD**, **Novolis.Rendering.Backends.TwoD.Silk**, **Novolis.Rendering.Presentation.Abstractions**, **Novolis.Math.Geometry**, and **Novolis.Math.Topology** (`TwoDCollisionWorld`, HUD, menus).

## Run

```powershell
cd novolis-lab
dotnet run --project labs/rendering/SilkTwoDHello
```

## Controls

| Input | Action |
|-------|--------|
| Menu | PLAY / QUIT on startup |
| A / D | Move |
| Space | Jump |

HUD shows position and control hints (`A/D move | Space jump | Esc menu`).

## ProjectRef note

For local iteration against sibling repos, open `Novolis.Platform.slnx` or pass `-p:NovolisUseProjectReferences=true`. Committed `.csproj` files use `PackageReference` from GitHub Packages only.
