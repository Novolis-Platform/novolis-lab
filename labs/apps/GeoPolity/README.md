# GeoPolity

Experimental lab host in `novolis-lab`.

Full-world geopolitics session host for `Novolis.Geopolitics` — Avalonia theatre, Spectre console, or headless report. Local dogfood only (not in the release catalog).

## Theatre vocabulary (UI-only)

These terms are **presentation only**. Core keeps `Polity` / `Province` / `Continent` — no map coords, no habitat kinds.

| UI term | Core type | Meaning |
|---------|-----------|---------|
| **System** | `Polity` | StarMap node / playable power |
| **Habitat** | `Province` | Local holdings; labels from `ResourceWeights` + `Coastal` |
| **Cluster** | Continent | Geographic grouping (scorecard legend) |

StarMap layout is computed in-app (`TheatreMapProjection`) from polity id + continent. One **player system** is chosen at load (highest GDP). AI runs everyone else via fiscal policy; force stocks grow through Core `CivicEngine`. The player may also order Land / Air / Naval builds (treasury spend → `ForceExpansion` event).

## Run

```powershell
dotnet run --project d:\novolis\novolis-lab\labs\apps\GeoPolity\GeoPolity.csproj -p:NovolisUseProjectReferences=true
```

| Mode | Args |
|------|------|
| Avalonia theatre (default) | *(none)* or `--mode avalonia` |
| Spectre console | `--mode spectre` or `spectre` |
| Headless report | `--headless --years 50` |
| Attach Agent Surface | add `--agent` (headless) — Avalonia/Spectre attach by default |

Local multi-repo: use `-p:NovolisUseProjectReferences=true` (or open `d:\novolis\Novolis.Platform.slnx`). Package restore uses nuget.org + GitHub Packages only.

## Avalonia layout

`StudioWorkspace` + `StarMapControl` (no Gaming / Planar scene):

- **Center** — systems as points, inter-system borders as edges; ship marker on the player system
- **Left** — clock, scorecard (wars / blocs / clusters), top power
- **Right** — selected system briefing, habitats, force totals, mil-share slider, Build Land/Air/Naval
- **Bottom** — headline feed (`WAR` / `PEACE` / `BUILD`)

Stable Avalonia agent ids: `geopolity.map`, `geopolity.build.land|air|naval`, `geopolity.milshare`, `geopolity.habitats`, plus run/speed controls.

## Agent Surface (`geopolity`)

HTTP **18857** / TCP **18858** when attached.

| Action | Params |
|--------|--------|
| `pause` / `resume` / `toggle` | — |
| `setspeed` | `preset` 1–5 |
| `step` | `days` |
| `advanceyears` | `years` |
| `selectsystem` | `systemId` |
| `setmilshare` | `share` 0.05–0.7 |
| `build` | `domain` land\|air\|naval, `amount` |

```powershell
# while a live host is attached:
Invoke-RestMethod http://127.0.0.1:18857/agent/document
Invoke-RestMethod http://127.0.0.1:18857/agent/command -Method POST -ContentType 'application/json' -Body '{"actionId":"build","params":{"domain":"naval","amount":"10"}}'
```

UI pipe (Avalonia): `novolis-avalonia-agent-geopolity`.

## Tests

```powershell
dotnet test d:\novolis\novolis-lab\labs\apps\GeoPolity\tests\GeoPolity.Unit\GeoPolity.Unit.csproj -p:NovolisUseProjectReferences=true
```
