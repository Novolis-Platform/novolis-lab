# Typed Solution Intelligence

This console lab demonstrates the solution-intelligence stack against a real `.slnx` file:

```text
IWorkspace → SolutionWorkspace → SLNX topology → MSBuild evaluation → Roslyn facts → SolutionCatalog
```

`IWorkspace` is only a typed directory root. The succeeding types add information without
collapsing a directory, solution, evaluated project, and C# semantic model into one abstraction.

## Run

Read solution topology only. This does not evaluate MSBuild or load Roslyn:

```powershell
dotnet run --project d:\novolis\novolis-lab\labs\workspaces\TypedSolutionIntelligence\TypedSolutionIntelligence.csproj -p:NovolisUseProjectReferences=true -- d:\novolis\novolis-workspaces\Novolis.Workspaces.slnx
```

Add evaluated MSBuild properties, items, and imports:

```powershell
dotnet run --project d:\novolis\novolis-lab\labs\workspaces\TypedSolutionIntelligence\TypedSolutionIntelligence.csproj -p:NovolisUseProjectReferences=true -- d:\novolis\novolis-workspaces\Novolis.Workspaces.slnx --evaluate --framework net10.0
```

Add the C# semantic projection (documents and declared types). This enables an MSBuild
design-time load, so use it only for a solution you trust:

```powershell
dotnet run --project d:\novolis\novolis-lab\labs\workspaces\TypedSolutionIntelligence\TypedSolutionIntelligence.csproj -p:NovolisUseProjectReferences=true -- d:\novolis\novolis-workspaces\Novolis.Workspaces.slnx --semantic --framework net10.0
```

Omit `-p:NovolisUseProjectReferences=true` to consume the published packages from GitHub Packages.
