# ArtillerySimulator

**The Defense of Kristiansand.** A howitzer on Odderøya’s south fort ridge (`58.1345°N 8.0085°E`) stops staggered hostile columns while four friendly defenders hold downtown chokepoints.

Host is **Silk only** (`Novolis.Silk`) — no Raylib references. Wire mesh: green land, flat sea blue, elevated lakes cyan (PDF water mask with hillside false-positive rejection). Elevations are Kartverket NDH (`NHM_DTM_25833`) via `map/layers/heightfield-1025.bin`.

```powershell
dotnet run --project d:\novolis\novolis-lab\labs\simulation\ArtillerySimulator\ArtillerySimulator.csproj -p:NovolisUseProjectReferences=true
```

Or launch the last local build:

```powershell
pwsh -File d:\novolis\novolis-lab\labs\simulation\ArtillerySimulator\Run-ArtillerySimulator.ps1
```

Regenerate map sources:

```powershell
dotnet run --file d:\novolis\novolis-lab\labs\simulation\ArtillerySimulator\scripts\build-kristiansand-map.cs -p:NovolisUseProjectReferences=true
```

Controls: right-drag orbit, middle-drag or Shift+left pan, wheel zoom, `G` gun, `R` fort snap, `V`/`O` map overview, `M` Kommunekart harbor view, `C` free/follow, `Q`/`E` traverse, `W`/`S` elevate (follow cam), Space fire, `1`–`4` charge, `,`/`.` pace.
