# AstroSmoke

Console smoke test for **Novolis.Astro.*** and **Novolis.Physics.Astro** from GitHub Packages (`2026.1.*`).

Exercises a synthetic catalog → prototype-compatible range bands → Dijkstra route → habitability/strategic assessment → overlay aliases → SVG/TSV plot, plus ly↔m conversions.

## Run

```powershell
dotnet restore
dotnet run --project labs/astro/AstroSmoke
```

## What it exercises

| Step | Packages |
|------|----------|
| Catalog + neighbors | `Novolis.Astro.Catalog` |
| Hop graph + route | `Novolis.Astro.Routing` |
| Habitability + profiles | `Novolis.Astro.Assessment` |
| Alias overlay | `Novolis.Astro.Overlay` |
| Route plot export | `Novolis.Astro.Plotting` |
| SI conversion | `Novolis.Physics.Astro` |

## Related

| App | Role |
|-----|------|
| [StarMapLab](StarMapLab/) | Interactive Avalonia star map |
| [NearSolPolity](../economy/NearSolPolity/) | Astro catalog → economy hubs |
