# RandoriFight

Side-view katana randori lab **Novolis.Raylib** (+ Game, Bindings), **Novolis.Simulation.View**, and **Novolis.Simulation.Humanoid**.

## Run

```powershell
cd novolis-lab
dotnet run --project labs/RandoriFight
```

## Controls

| Input | Action |
|-------|--------|
| A / D | Ma-ai (distance) |
| U / I / O / J / K / L | Attacks (men, kesa, tsuki, do, kote, kirioroshi) |
| H | Parry (uke) |
| Tab | Toggle rig debug |
| R | Reset bout |

Banner shows round outcome; on-screen hint lists technique keys.

## ProjectRef note

For local iteration against sibling repos, open `Novolis.Platform.slnx` or pass `-p:NovolisUseProjectReferences=true`. Committed `.csproj` files use `PackageReference` from GitHub Packages only.
