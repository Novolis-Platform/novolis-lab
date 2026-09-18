# MathGridDemo

Headless console dogfood for **Novolis.Math.Arrays** (`DenseGrid`, `GridIndex`).

## Run

```powershell
cd novolis-lab
dotnet run --project labs/MathGridDemo
```

Prints a one-line grid summary, e.g. `Dogfood 8x8: No…`.

## ProjectRef note

For local iteration against sibling repos, open `Novolis.Platform.slnx` or pass `-p:NovolisUseProjectReferences=true`. Committed `.csproj` files use `PackageReference` from GitHub Packages only.
