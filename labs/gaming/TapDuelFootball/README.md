# Tap Duel Football

Portrait hotseat recreation of [frankhaugen/tap-duel-football](https://github.com/frankhaugen/tap-duel-football) on **Novolis.Rendering.TwoD** + **Novolis.Game.MenuFlows**.

Two players share one device: tap your end of the field to shove the football toward the opponent. First into the far end zone wins.

AdMob / interstitial ads from the Unity original are intentionally omitted.

## Run

```powershell
dotnet run --project d:\novolis\novolis-lab\labs\gaming\TapDuelFootball -p:NovolisUseProjectReferences=true
```

## Controls

| Input | Action |
|-------|--------|
| Click / tap **bottom** half | Player 1 push |
| Click / tap **top** half | Player 2 push |
| `A` / `S` / `↓` | Player 1 push |
| `W` / `↑` | Player 2 push |
| Menu ↑↓ + Enter | Navigate PLAY / RESET / EXIT |

## Packages

`Novolis.Rendering.TwoD`, `Novolis.Rendering.Backends.TwoD.Silk`, `Novolis.Rendering.Presentation.Abstractions`, `Novolis.Game.MenuFlows`, `Novolis.Math.Geometry`, `Novolis.Math.Topology` — GPR `2026.1.*`.

Under `-p:NovolisUseProjectReferences=true`, Silk.NET windowing/input packages are PackageReferenced explicitly (ProjectRef mode does not flow NuGet deps from substituted projects).
