# Typed Solution Intelligence

Console walkthrough of the `Novolis.Workspaces.DotNet.*` stack against either a generated
fixture or a real `.slnx` file:

```text
IWorkspace → SolutionWorkspace → SLNX topology → MSBuild evaluation → Roslyn facts → SolutionCatalog
GitRepositoryWorkspace is a sibling discovery (SolutionRepositoryRelation), not inheritance.
```

`IWorkspace` is only a typed directory root. Each following type adds information without
collapsing a directory, Git repository, solution, evaluated project, and C# semantic model
into one abstraction.

With `--semantic` (the no-argument fixture path), the catalog is compiled into a C# façade
whose members are the solution's named projects, namespaces, and types:

```csharp
solution.Projects.DemoLib.Novolis.Sample.Widget
```

That source is emitted, compiled in memory, and executed — not printed as a string dump of
the catalog.

## Run

With no arguments the lab writes a tiny two-project fixture to temp and walks every layer,
including Git relation, MSBuild, Roslyn, and catalog queries:

```powershell
dotnet run --project d:\novolis\novolis-lab\labs\workspaces\TypedSolutionIntelligence\TypedSolutionIntelligence.csproj -p:NovolisUseProjectReferences=true
```

Read solution topology only. This does not evaluate MSBuild or load Roslyn:

```powershell
dotnet run --project d:\novolis\novolis-lab\labs\workspaces\TypedSolutionIntelligence\TypedSolutionIntelligence.csproj -p:NovolisUseProjectReferences=true -- d:\novolis\novolis-workspaces\Novolis.Workspaces.slnx
```

Add evaluated MSBuild properties, items, and imports:

```powershell
dotnet run --project d:\novolis\novolis-lab\labs\workspaces\TypedSolutionIntelligence\TypedSolutionIntelligence.csproj -p:NovolisUseProjectReferences=true -- d:\novolis\novolis-workspaces\Novolis.Workspaces.slnx --evaluate --framework net10.0
```

Add the C# semantic projection (documents and declared types) and catalog queries. This
enables an MSBuild design-time load, so use it only for a solution you trust:

```powershell
dotnet run --project d:\novolis\novolis-lab\labs\workspaces\TypedSolutionIntelligence\TypedSolutionIntelligence.csproj -p:NovolisUseProjectReferences=true -- d:\novolis\novolis-workspaces\Novolis.Workspaces.slnx --semantic --framework net10.0 --namespace Novolis.Workspaces.DotNet
```

Omit `-p:NovolisUseProjectReferences=true` to consume the published packages from GitHub Packages.
