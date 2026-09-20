# Typed Solution Intelligence

Console walkthrough of the `Novolis.Workspaces.DotNet.*` stack against either the committed
fixture or a real `.slnx` file:

```text
IWorkspace → SolutionWorkspace → SLNX topology → MSBuild evaluation → Roslyn facts → SolutionCatalog
GitRepositoryWorkspace is a sibling discovery (SolutionRepositoryRelation), not inheritance.
```

`IWorkspace` is only a typed directory root. Each following type adds information without
collapsing a directory, Git repository, solution, evaluated project, and C# semantic model
into one abstraction.

The no-argument path compiles `Generated/FixtureExploration.g.cs` into this lab, so the
consumer is ordinary C#:

```csharp
var solution = new GeneratedSolution(catalog);
SemanticType identityService = solution.Projects.DemoLib.Services.IdentityService;
SemanticType widget = solution.Projects.DemoLib.Novolis.Sample.Widget;
```

Those members are generated properties of type `SemanticType`. There is no `dynamic`.
Against an arbitrary `.slnx`, `--semantic` still emits a façade and compiles it with Roslyn
so member access is type-checked in that compilation.

## Run

With no arguments the lab opens `fixture/src/Fixture.slnx` and walks every layer,
including Git relation, MSBuild, Roslyn, catalog queries, and the typed consumer:

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
