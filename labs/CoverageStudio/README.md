# Coverage Studio

Experimental lab host in `novolis-lab`.

Avalonia desktop host for org-wide **test coverage**, **cyclomatic complexity / CRAP**, and **test runs** across `novolis-*` repos.

Built on [`Novolis.Tools.Coverage`](../../../novolis-tools/src/Novolis.Tools.Coverage/README.md) plus Avalonia controls (`SortableDataGrid`, `TreeDetailsView`) and `Novolis.Avalonia.Studio` chrome.

Child `dotnet` / `pwsh` processes always run **hidden** (`CreateNoWindow`). Progress is typed (`WorkRun` / `WorkHostItem`) with compact in-UI bars — no console host pane.

## Run

```powershell
dotnet run --project d:\novolis\novolis-lab\labs\CoverageStudio\CoverageStudio.csproj -p:NovolisUseProjectReferences=true
```

Or from NuGet restore (published packages):

```powershell
dotnet run --project d:\novolis\novolis-lab\labs\CoverageStudio\CoverageStudio.csproj
```

## What it does

| Action | Behavior |
|--------|----------|
| **Discover** | Lists MTP test-host repos from `Novolis.Platform.slnx` (default) or per-repo NuGet mode |
| **Run tests** | Parallel `dotnet test` on selected repos (optional `--no-build`, ProjectRef when Platform mode) |
| **Collect coverage** | `CoverageCollector` → Cobertura + ReportGenerator HTML under the output dir |
| **Analyze CRAP** | Platform fan-in CRAP scores (`CC² × (1−cov)³ + CC`) with cyclomatic complexity |
| **Gaps** | Packages below the 95% line/branch target |
| **Open HTML** | Opens the merged ReportGenerator index |

Defaults: workspace root from `NOVOLIS_ROOT` / walk to `Novolis.Platform.slnx`, output `d:\novolis\coverage`, Platform ProjectRef, skip build, FailBelow disabled (`-1`), **per-host timeout 60s** (0 = no limit).

## Related

- CLI: `novolis-coverage` / `d:\novolis\novolis-tools\src\Novolis.Tools.Coverage.Cli`
- Docs: `d:\novolis\novolis-governance\docs\coverage-report.md`
