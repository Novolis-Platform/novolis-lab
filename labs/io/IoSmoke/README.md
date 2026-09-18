# IoSmoke

Headless dogfood for the core **Novolis.IO.*** packages (from GitHub Packages `2026.1.*`, or ProjectReference mode).

## Exercises

| Package | What runs |
|---------|-----------|
| `Novolis.IO.Paths` | `RootFinder` against lab repo markers |
| `Novolis.IO.Recovery` | Write / get latest / clear snapshots |
| `Novolis.IO.Watching` | Debounced change on a temp file |
| `Novolis.IO.Processes` | Queue `dotnet --version` |
| `Novolis.IO.Git` | `GetStatus` on the lab repo (skipped if no `.git`) |

For Android ADB / APK install, use the **Adb utility** in
`novolis-utilities`.

## Run

```powershell
cd novolis-lab
dotnet restore
dotnet run --project labs/io/IoSmoke

# Local unreleased IO APIs:
dotnet run --project labs/io/IoSmoke -p:NovolisUseProjectReferences=true
```

Exit `0` prints `IoSmoke OK`.
