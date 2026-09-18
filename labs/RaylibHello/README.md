# RaylibHello

Minimal **Novolis.Raylib** window: clear + title text via `RayGame.Run`.

## Run

```powershell
cd novolis-lab
dotnet run --project labs/RaylibHello
```

Opens an 800×600 window titled “Novolis Dogfood” with gray status text. Close the window to exit.

## ProjectRef note

For local iteration against sibling repos, open `Novolis.Platform.slnx` or pass `-p:NovolisUseProjectReferences=true`. Committed `.csproj` files use `PackageReference` from GitHub Packages only.
