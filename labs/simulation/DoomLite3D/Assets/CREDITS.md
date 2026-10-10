# Asset credits — DoomLite3D

## Kenney — Tiny Dungeon

- **Source:** https://kenney.nl/assets/tiny-dungeon
- **License:** [CC0 1.0 Universal](https://creativecommons.org/publicdomain/zero/1.0/)
- **Official download:** https://kenney.nl/assets/tiny-dungeon (file: `kenney_tiny-dungeon.zip`)

The following files are **64×64 stand-in PNGs** generated for the Novolis dogfood build so CI and clones run without a manual download. Replace them with sprites from the official Kenney pack (recommended filenames):

| File | Status |
|------|--------|
| `enemies/imp.png` | Procedural stand-in (grunt) |
| `enemies/demon.png` | Procedural stand-in (grunt) |
| `enemies/brute.png` | Procedural stand-in (pack rooms) |
| `enemies/boss.png` | Procedural stand-in (boss room) |
| `weapon.png` | Procedural stand-in |

| File in repo | Suggested Kenney tile (Tilemap / PNG) |
|--------------|----------------------------------------|
| `enemies/imp.png` | Enemy / character sprite |
| `enemies/demon.png` | Alternate enemy sprite |
| `enemies/brute.png` | Heavy enemy sprite |
| `enemies/boss.png` | Large enemy sprite |
| `weapon.png` | Weapon or UI icon |

Run `scripts/generate-placeholder-assets.ps1` to create stand-ins. Run `scripts/fetch-kenney-assets.ps1` to copy from a Kenney zip when available.

## Novolis

Procedural level layout and game code are MIT-licensed as part of [novolis-lab](https://github.com/Novolis-Platform/novolis-lab).
