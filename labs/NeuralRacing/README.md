# NeuralRacing

Headless evolutionary racing trainer lab **Novolis.MachineLearning.Neural** and **Novolis.Simulation.Racing** (built-in tracks, episode loop). Training glue lives in this app, not a library package.

## Run

```powershell
cd novolis-lab
dotnet run --project labs/NeuralRacing
```

Prints generations run, best fitness, and champion network name (default: `MicroCircle` track, 24 agents × 8 generations).

## Tests

```powershell
dotnet test labs/NeuralRacing.Tests
```

TUnit validation of small training runs and network I/O sizes.

## ProjectRef note

For local iteration against sibling repos, open `Novolis.Platform.slnx` or pass `-p:NovolisUseProjectReferences=true`. Committed `.csproj` files use `PackageReference` from GitHub Packages only.
