# Frank Moat — Minigun Range

Single-room **Loaded-style** top-down 2D shooter lab. Simulation stays flat XZ. Walls render with a fixed northwest height extrusion, depth-sorted sprites, and a local fade when Frank walks behind a face.

This is the Level 69 private range from `novolis-lab/docs/todo/frank-moat-versus-the-evil-undead.md` — not the full 69-floor campaign.

## Play

From a directory that contains a Novolis `global.json` (for example `d:\novolis\novolis-lab`), so `dotnet` does not pick an unrelated SDK:

```powershell
dotnet run --project d:\novolis\novolis-lab\labs\gaming\FrankMoat\FrankMoat.csproj -p:NovolisUseProjectReferences=true
```

## Controls

| Input | Action |
|-------|--------|
| WASD | Move (instant start/stop) |
| Mouse | Aim |
| LMB | Shoot (1911 is semi; rotary holds) |
| R | Reload (shotgun tops up one shell; shooting interrupts) |
| 1 / 2 / 3 | M1911 / pump 12 / HRRS rotary |
| E | Cycle weapon |
| H | Inspect (pauses hostiles) |
| + / - | Zoom |
| Esc | Pause |

## What it is proving

- Instant movement before anything else
- 2D collision + ray-segment hits against wall footprints
- Raised wall faces, tops, and occlusion fade
- Cosmetic particles and persistent decals that are **not** gameplay entities
- Three mechanically distinct firearms in one 50×50 m range

## Packages

`Novolis.Rendering.TwoD`, `Novolis.Silk`, `Novolis.Game.MenuFlows`, `Novolis.Math.Geometry`, `Novolis.Math.Topology` — GPR `2026.1.*`.
