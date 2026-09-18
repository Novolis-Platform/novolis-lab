# TopDownDoom

Doom combat in orthographic top-down — closets, barrels, blue key, weapon upgrades, juice.

## Play (no setup)

```bash
dotnet run --project labs/gaming/TopDownDoom/TopDownDoom.csproj
```

Built-in **8-way** sprites (no spinning), explosions, screen shake. Start with pistol; first ammo box gives the shotgun.

Characters pick the correct facing frame (idle / jump-walk) instead of rotating a single “down” sprite.

## Optional: prettier CC0 art (one command)

```powershell
cd labs/gaming/TopDownDoom
pwsh -File scripts/fetch-fun-art.ps1
dotnet run
```

Downloads [square CC0 characters](https://opengameart.org/content/hand-drawn-square-characters-animated-8-directions-top-down-free-cc0) (Hero / Skeleton / Monster + death FX).

## Controls

| Input | Action |
|-------|--------|
| WASD | Move |
| Mouse | Aim |
| LMB | Shoot |
| Shift | Dash |
| E | Cycle weapon |
| Esc | Pause |

## Packages

`Novolis.Rendering.TwoD`, `Novolis.Rendering.Backends.TwoD.Silk`, `Novolis.Game.MenuFlows` — GPR `2026.1.*`.
