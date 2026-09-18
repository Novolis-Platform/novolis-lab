# TDS character art (optional)

Purchase and extract [Top Down Shooter: Main Characters](https://free-game-assets.itch.io/top-down-shooter-main-characters) into this folder.

The game auto-detects animation folders whose names contain:

| Role   | Folder name tokens (case-insensitive) |
|--------|----------------------------------------|
| Player | `walk` + `gun` (walk loop), `gun` + `shot` (shoot, optional) |
| Fodder | `walk` + `knife` (falls back to player walk if missing) |

Example layout after unzip:

```
Assets/TdsCharacters/
  Man/
    Walk with gun/
      frame_001.png
      ...
    Gun shot/
      ...
  Woman/
    Walk with knife/
      ...
```

PNG frames are packed into a horizontal atlas at runtime. Without this folder, **procedural** marine/zombie sprites are used instead.

**License:** follow the itch.io / Craftpix terms for the pack you purchased. Do not commit purchased PNGs to a public repo unless your license allows it.
