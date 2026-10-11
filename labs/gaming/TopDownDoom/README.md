# TopDownDoom

Classic Doom WAD geometry and resources rendered as a fixed-orientation
orthographic top-down action game. The runtime keeps a procedural fallback so
the lab remains runnable before a WAD package is generated.

## Play

```bash
dotnet run --project labs/gaming/TopDownDoom/TopDownDoom.csproj
```

With no generated package, the lab uses its built-in teaching level. With an
extracted package under `Assets/ImportedDoom`, it loads `E1M1`, sector floors,
linedef walls, map things, imported textures, and imported sprite angles.

## Extract a map

The original Doom engine source is available from
[id Software’s GPL source release](https://github.com/id-Software/DOOM/), but
the original game WAD is separate copyrighted data. [Freedoom](https://freedoom.github.io/)
provides compatible levels and art under its BSD-style license.

Obtain a Freedoom IWAD from its official project, or provide a locally owned
original Doom IWAD. The WAD itself is never copied into this repository:

The scripted Freedoom path downloads the official 0.13.0 release to a temporary
directory, extracts `freedoom1.wad`, and sends `E1M1` through the same Asset
Studio-backed importer:

```powershell
pwsh -File labs/gaming/TopDownDoom/scripts/fetch-freedoom.ps1
```

That path also stages the release’s `COPYING.txt` and `CREDITS.txt` beside the
generated package. Keep those files with any redistributed Freedoom-derived
package. Original Doom WAD extraction is intentionally a local-only workflow.

For an already downloaded IWAD:

```powershell
pwsh -File labs/gaming/TopDownDoom/scripts/extract-doom.ps1 `
  -InputWad C:\path\to\freedoom.wad `
  -Map E1M1 `
  -ContentProfile freedoom
```

For a locally owned original WAD:

```powershell
pwsh -File labs/gaming/TopDownDoom/scripts/extract-doom.ps1 `
  -InputWad C:\path\to\DOOM.WAD `
  -Map E1M1 `
  -ContentProfile original-local
```

Generated JSON, PNG resources, and previews are ignored under
`Assets/ImportedDoom`. The package contains classic map records
(`VERTEXES`, `LINEDEFS`, `SIDEDEFS`, `SECTORS`, and `THINGS`), palette-decoded
patches, composite wall textures, flats, sprites, a deterministic
`sprites/sprites-atlas.png` plus frame manifest, and a top-down preview. The
extractor validates generated images through Asset Studio’s bounded importer
and packs sprite frames through Visual Baking.

The runtime selects `E1M1` from `Assets/ImportedDoom` by default. Override the
map or package location for another generated classic map with startup
environment variables:

```powershell
$env:NOVOLIS_TOPDOWN_DOOM_MAP = 'E1M1'
$env:NOVOLIS_TOPDOWN_DOOM_PACKAGE = 'C:\path\to\ImportedDoom'
dotnet run --project labs/gaming/TopDownDoom/TopDownDoom.csproj
```

The first importer milestone supports classic Doom-format maps only. Hexen and
UDMF maps are intentionally not accepted yet.

## Optional: CC0 fallback art

```powershell
pwsh -File scripts/fetch-fun-art.ps1
```

Downloads [square CC0 characters](https://opengameart.org/content/hand-drawn-square-characters-animated-8-directions-top-down-free-cc0).

## Controls

| Input | Action |
|-------|--------|
| WASD | Move |
| Mouse | Aim |
| LMB | Shoot |
| Shift | Dash |
| E | Cycle weapon |
| Esc | Pause |

## Packages and tooling

`Novolis.Game.Scenes`, `Novolis.Game.MenuFlows`, `Novolis.Silk` — GPR `2026.1.*`.

The extractor is the `novolis-doom` developer tool in
`novolis-tools/src/Novolis.Tools.Doom.Cli`.
