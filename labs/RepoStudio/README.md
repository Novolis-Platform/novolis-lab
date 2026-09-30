# Repo Studio

Experimental lab host in `novolis-lab`.

Hybrid CLI + Avalonia host for multi-repo Git over the Novolis workspace.

- Domain: `Novolis.IO.Git`
- Chrome: `Novolis.Avalonia.Git`

## Run

```powershell
dotnet run --project d:\novolis\novolis-lab\labs\RepoStudio\RepoStudio.csproj -p:NovolisUseProjectReferences=true
dotnet run --project d:\novolis\novolis-lab\labs\RepoStudio\RepoStudio.csproj -p:NovolisUseProjectReferences=true -- --mode spectre status --json
dotnet run --project d:\novolis\novolis-lab\labs\RepoStudio\RepoStudio.csproj -p:NovolisUseProjectReferences=true -- --mode spectre fetch --parallel 8
dotnet run --project d:\novolis\novolis-lab\labs\RepoStudio\RepoStudio.csproj -p:NovolisUseProjectReferences=true -- daemon --interval 600
```

This host remains a lab artifact while the command surface is being explored.
If it becomes a supported developer product, its final home can be decided
between `novolis-tools` and `novolis-utilities`.
