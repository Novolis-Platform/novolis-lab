using System.IO.Abstractions;
using RootWorkspace = Novolis.IO.Workspace.IWorkspace;
using Novolis.Workspaces.DotNet;
using Novolis.Workspaces.DotNet.Indexing;
using Novolis.Workspaces.DotNet.Slnx;

if (args.Length is 0 || args[0] is "--help" or "-h" or "help")
{
    PrintUsage();
    return 0;
}

var solutionPath = new SolutionFilePath(args[0]);
if (!File.Exists(solutionPath.FullPath))
{
    Console.Error.WriteLine($"Solution file does not exist: {solutionPath.FullPath}");
    return 1;
}

var rootPath = Path.GetDirectoryName(solutionPath.FullPath);
if (rootPath is null)
{
    Console.Error.WriteLine($"Could not determine a root directory for {solutionPath.FullPath}");
    return 1;
}

var fileSystem = new FileSystem();
var solution = new SolutionWorkspace(
    fileSystem.DirectoryInfo.New(rootPath),
    solutionPath);
RootWorkspace rootWorkspace = solution;

Console.WriteLine($"Root workspace: {rootWorkspace.Root.FullName}");
Console.WriteLine($"Solution:       {solution.SolutionFile.FullPath}");

var topology = await new SlnxSolutionReader().ReadAsync(solution);
Console.WriteLine();
Console.WriteLine($"Topology: {topology.Projects.Count} projects, {topology.Folders.Count} folders");
foreach (var project in topology.Projects)
    Console.WriteLine($"  project  {project.RelativePath}");
foreach (var folder in topology.Folders)
    Console.WriteLine($"  folder   {folder.Name}");

var enableDesignTimeBuilds = HasFlag(args, "--semantic");
var enableEvaluation = HasFlag(args, "--evaluate") || enableDesignTimeBuilds;
if (!enableEvaluation)
{
    PrintDiagnostics(topology.Diagnostics);
    Console.WriteLine();
    Console.WriteLine("Topology only. Pass --evaluate for MSBuild facts or --semantic for C# semantic facts.");
    return HasErrors(topology.Diagnostics) ? 1 : 0;
}

var context = new EvaluationContext(
    Configuration: GetOption(args, "--configuration") ?? "Debug",
    Platform: GetOption(args, "--platform") ?? "AnyCPU",
    TargetFramework: GetOption(args, "--framework"),
    AllowEvaluation: true,
    AllowDesignTimeBuilds: enableDesignTimeBuilds);
var catalog = await new SolutionCatalogBuilder().BuildAsync(solution, context);

Console.WriteLine();
Console.WriteLine($"Catalog snapshot: {catalog.SnapshotId}");
Console.WriteLine($"Created:          {catalog.Provenance.CreatedAt:O}");
Console.WriteLine($"Evaluation:       {context.Configuration}|{context.Platform}|{context.TargetFramework ?? "default"}");

foreach (var project in catalog.Projects)
{
    var framework = project.Evaluation.Properties.TryGetValue("TargetFramework", out var targetFramework)
        ? targetFramework
        : "not evaluated";
    Console.WriteLine(
        $"  {project.Project.RelativePath} — {framework}; "
        + $"{project.Evaluation.Items.Count} items; {project.Evaluation.Imports.Count} imports; "
        + $"{project.Semantic.Types.Count} C# types");

    if (enableDesignTimeBuilds)
    {
        foreach (var type in project.Semantic.Types.Take(5))
            Console.WriteLine($"    {type.Kind,-10} {type.MetadataName} ({type.SourcePath})");
    }
}

PrintDiagnostics(catalog.Diagnostics);
return HasErrors(catalog.Diagnostics) ? 1 : 0;

static string? GetOption(IEnumerable<string> commandLine, string name)
{
    var arguments = commandLine.ToArray();
    for (var index = 1; index < arguments.Length - 1; index++)
    {
        if (string.Equals(arguments[index], name, StringComparison.OrdinalIgnoreCase))
            return arguments[index + 1];
    }

    return null;
}

static bool HasFlag(IEnumerable<string> commandLine, string name) =>
    commandLine.Any(argument => string.Equals(argument, name, StringComparison.OrdinalIgnoreCase));

static bool HasErrors(IEnumerable<WorkspaceDiagnostic> diagnostics) =>
    diagnostics.Any(diagnostic => diagnostic.Severity is WorkspaceDiagnosticSeverity.Error);

static void PrintDiagnostics(IEnumerable<WorkspaceDiagnostic> diagnostics)
{
    foreach (var diagnostic in diagnostics)
        Console.Error.WriteLine($"{diagnostic.Severity} {diagnostic.Code}: {diagnostic.Message} ({diagnostic.Path})");
}

static void PrintUsage()
{
    Console.WriteLine("""
        TypedSolutionIntelligence
          <solution.slnx> [--evaluate] [--semantic]
                          [--configuration Debug] [--platform AnyCPU] [--framework net10.0]

        The default reads topology only.
        --evaluate enables MSBuild evaluation without running build targets.
        --semantic enables evaluation and Roslyn design-time loading.
        """);
}
