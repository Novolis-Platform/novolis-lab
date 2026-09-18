# PulseStrip

Anti-grav combat racer (Wipeout / [BallisticNG](https://store.steampowered.com/app/473770/BallisticNG/)-inspired). Solid ribbon tracks, Synert ship meshes, weapons/boost, ML opponents.

Ship FBX from [Synert/WipeoutClone](https://github.com/Synert/WipeoutClone) (MIT) — see `Content/CREDITS.md`. BallisticNG is closed-source; we match its AG camera/HUD language only.

## Platforms

| Platform | Status |
|----------|--------|
| **Windows** (`win-x64`) | Supported — Raylib + miniaudio |
| **Linux** (`linux-x64`) | Supported — Raylib + miniaudio |
| **Android** | **Deferred** — Novolis.Raylib has no Android RID/host yet; shared logic in `PulseStrip.Core` |

## Run

```powershell
dotnet run --project d:\novolis\novolis-lab\labs\raylib\PulseStrip -p:NovolisUseProjectReferences=true
```

Headless smoke:

```powershell
$env:NOVOLIS_RAYLIB_HEADLESS = '1'
dotnet run --project d:\novolis\novolis-lab\labs\raylib\PulseStrip -p:NovolisUseProjectReferences=true -- --smoke
```

## Controls

- **W/Up** throttle, **S/Down** brake, **A/D** steer
- **Shift** boost, **Space** fire plasma
