# StarMapLab

Avalonia lab lab **Novolis.Avalonia.StarMap** with **Novolis.Astro** (catalog, routing, assessment, overlay) and **Novolis.Physics.Astro** from GitHub Packages (`2026.1.*`).

## Features

- **84 real nearby stars** (≤ ~20.5 ly) with Johnston (2022) galactic XYZ
- Pan/zoom map on the X–Y galactic plane
- From/To routing with prototype bands (**short ≤10 ly @ 1×**, **long ≤12 ly @ 3×**)
- Selectable jumps with `key=value` parsable detail panel
- Assessment and overlay side panel

Default route: Sol → Altair. Check **List all graph jumps** to inspect any hop edge.

## Run

```powershell
dotnet run --project novolis-lab/labs/astro/StarMapLab
```

For local platform iteration with ProjectReference mode:

```powershell
dotnet run --project novolis-lab/labs/astro/StarMapLab -p:NovolisUseProjectReferences=true
```

## Related

| App / package | Role |
|---------------|------|
| [AstroSmoke](AstroSmoke/) | Headless console pipeline smoke |
| `Novolis.Avalonia.StarMap` | Map control and viewport |
| [NearSolPolity](../economy/NearSolPolity/) | Economy bridge from stellar hubs |
