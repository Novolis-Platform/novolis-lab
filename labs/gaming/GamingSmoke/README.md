# GamingSmoke

Headless console smoke for **Novolis.Game.Identity**, **Novolis.Game.MenuFlows**, and **Novolis.Game.Multiplayer.Abstractions**.

## Run

```powershell
cd novolis-lab
dotnet run --project labs/gaming/GamingSmoke
```

Creates a guest player, pushes main + pause screens on `GameScreenStack`, seeds an in-memory lobby, and prints one status line, e.g. `Player=… Screen=main LobbyPlayers=1`. Exit code `0`.

No CLI flags.

## ProjectRef note

For local iteration against sibling repos, open `Novolis.Platform.slnx` or pass `-p:NovolisUseProjectReferences=true`. Committed `.csproj` files use `PackageReference` from GitHub Packages only.
