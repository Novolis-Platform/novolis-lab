using System.IO.Abstractions;
using Novolis.IO.Git;
using Novolis.Workspaces.DotNet;
using Novolis.Workspaces.DotNet.Git;
using Novolis.Workspaces.DotNet.Indexing;
using Novolis.Workspaces.DotNet.MSBuild;
using Novolis.Workspaces.DotNet.Roslyn;
using Novolis.Workspaces.DotNet.Slnx;
using RootWorkspace = Novolis.IO.Workspace.IWorkspace;

if (HasFlag(args, "--help") || HasFlag(args, "-h") || HasFlag(args, "help"))
{
    PrintUsage();
    return 0;
}

var options = ParseOptions(args);
string? fixtureRoot = null;
try
{
    var fileSystem = new FileSystem();
    var solutionPath = options.SolutionPath;
    if (solutionPath is null)
    {
        fixtureRoot = CreateDemoFixture();
        solutionPath = Path.Combine(fixtureRoot, "src", "Fixture.slnx");
        options = options with
        {
            Evaluate = true,
            Semantic = true,
            Framework = options.Framework ?? "net10.0",
            QueryNamespace = options.QueryNamespace ?? "Novolis.Sample",
        };
    }

    if (!File.Exists(solutionPath))
    {
        Console.Error.WriteLine($"Solution file does not exist: {solutionPath}");
        return 1;
    }

    var solutionFile = new SolutionFilePath(solutionPath);
    var solutionRootPath = Path.GetDirectoryName(solutionFile.FullPath);
    if (solutionRootPath is null)
    {
        Console.Error.WriteLine($"Could not determine a root directory for {solutionFile.FullPath}");
        return 1;
    }

    var solution = new SolutionWorkspace(fileSystem.DirectoryInfo.New(solutionRootPath), solutionFile);
    RootWorkspace rootWorkspace = solution;

    Console.WriteLine("Novolis.Workspaces.DotNet");
    Console.WriteLine("IWorkspace → SolutionWorkspace → SLNX → MSBuild → Roslyn → Catalog");
    Console.WriteLine("Git is a sibling discovery, not an inherited solution type.");
    Console.WriteLine();

    WriteSection("Abstractions (Novolis.Workspaces.DotNet.Abstractions)");
    Console.WriteLine($"IWorkspace root:     {rootWorkspace.Root.FullName}");
    Console.WriteLine($"SolutionWorkspace:   {solution.SolutionFile.FullPath}");

    var repository = fixtureRoot is null
        ? TryFindRepositoryWorkspace(fileSystem, solution)
        : new GitRepositoryWorkspace(
            fileSystem.DirectoryInfo.New(fixtureRoot),
            "typed-solution-demo",
            GitWorktreeKind.Main);
    var groupRoot = repository?.Root ?? solution.Root;
    var group = new MultiSolutionWorkspace(groupRoot, [solution]);
    Console.WriteLine($"Multi-solution group: {group.Root.FullName} ({group.Members.Count} member)");

    WriteSection("Git (Novolis.Workspaces.DotNet.Git)");
    if (repository is null)
    {
        Console.WriteLine("No containing Git repository workspace was found.");
    }
    else
    {
        var relation = SolutionRepositoryRelation.Create(solution, repository);
        Console.WriteLine($"Repository:  {repository.RepositoryName} ({repository.WorktreeKind})");
        Console.WriteLine($"Repo root:   {repository.Root.FullName}");
        Console.WriteLine($"Relation:    {relation.Kind}");
    }

    WriteSection("SLNX (Novolis.Workspaces.DotNet.Slnx)");
    var slnxReader = new SlnxSolutionReader();
    var topology = await slnxReader.ReadAsync(solution);
    Console.WriteLine($"Projects: {topology.Projects.Count}    Folders: {topology.Folders.Count}");
    foreach (var folder in topology.Folders)
        Console.WriteLine($"  folder   {folder.Name}");
    foreach (var project in topology.Projects)
        Console.WriteLine($"  project  {project.RelativePath}");
    PrintDiagnostics(topology.Diagnostics);

    if (!options.Evaluate && !options.Semantic)
    {
        Console.WriteLine();
        Console.WriteLine("Topology only. Pass --evaluate for MSBuild facts or --semantic for C# semantic facts.");
        return HasErrors(topology.Diagnostics) ? 1 : 0;
    }

    var context = new EvaluationContext(
        Configuration: options.Configuration,
        Platform: options.Platform,
        TargetFramework: options.Framework,
        AllowEvaluation: true,
        AllowDesignTimeBuilds: options.Semantic);
    var evaluator = new MsBuildProjectEvaluator();
    var semanticLoader = new RoslynSemanticProjectLoader();
    var catalog = await new SolutionCatalogBuilder(slnxReader, evaluator, semanticLoader)
        .BuildAsync(solution, context);

    WriteSection("MSBuild (Novolis.Workspaces.DotNet.MSBuild)");
    Console.WriteLine($"Context: {context.Configuration}|{context.Platform}|{context.TargetFramework ?? "default"}");
    Console.WriteLine($"AllowEvaluation={context.AllowEvaluation}  AllowDesignTimeBuilds={context.AllowDesignTimeBuilds}");
    foreach (var project in catalog.Projects)
    {
        var framework = ReadProperty(project.Evaluation, "TargetFramework") ?? "not evaluated";
        var outputType = ReadProperty(project.Evaluation, "OutputType") ?? "n/a";
        Console.WriteLine(
            $"  {project.Project.RelativePath} — TFM {framework}; OutputType {outputType}; "
            + $"{project.Evaluation.Items.Count} items; {project.Evaluation.Imports.Count} imports");
    }

    if (options.Semantic)
    {
        WriteSection("Roslyn (Novolis.Workspaces.DotNet.Roslyn)");
        foreach (var project in catalog.Projects)
        {
            Console.WriteLine(
                $"  {project.Project.RelativePath} — {project.Semantic.Documents.Count} documents; "
                + $"{project.Semantic.Types.Count} types");
            foreach (var type in project.Semantic.Types.Take(8))
                Console.WriteLine($"    {type.Kind,-10} {type.MetadataName}  public={type.IsPublic}");
        }
    }

    WriteSection("Indexing (Novolis.Workspaces.DotNet.Indexing)");
    Console.WriteLine($"Snapshot:  {catalog.SnapshotId}");
    Console.WriteLine($"Created:   {catalog.Provenance.CreatedAt:O}");
    Console.WriteLine($"Solution:  {catalog.Provenance.SolutionPath}");

    if (options.ProjectPath is not null)
    {
        var match = catalog.FindProject(options.ProjectPath);
        Console.WriteLine(
            match is null
                ? $"FindProject: no catalog entry for {options.ProjectPath}"
                : $"FindProject: {match.Project.RelativePath} ({match.Semantic.Types.Count} types)");
    }

    if (options.Semantic)
    {
        var publicTypes = catalog.FindPublicTypes().ToArray();
        Console.WriteLine($"Public types: {publicTypes.Length}");
        foreach (var type in publicTypes.Take(12))
            Console.WriteLine($"  {type.Kind,-10} {type.MetadataName}");

        if (!string.IsNullOrWhiteSpace(options.QueryNamespace))
        {
            var namespaced = catalog.FindTypesInNamespace(options.QueryNamespace).ToArray();
            Console.WriteLine($"Namespace {options.QueryNamespace}: {namespaced.Length} types");
            foreach (var type in namespaced)
                Console.WriteLine($"  {type.Kind,-10} {type.MetadataName}");
        }

        WriteSection("Generated typed exploration");
        var generated = SolutionExplorationGenerator.Generate(catalog);
        var sourceLines = generated.Source.Split('\n');
        Console.WriteLine($"Emitted {sourceLines.Length} lines of C# ({generated.WalkTypeName}.Walk).");
        Console.WriteLine("Typed members are real properties, for example:");
        Console.WriteLine("  solution.Projects.DemoLib.Novolis.Sample.Widget");
        Console.WriteLine();
        if (sourceLines.Length <= 220)
        {
            Console.WriteLine(generated.Source);
        }
        else
        {
            foreach (var line in sourceLines.Take(80))
                Console.WriteLine(line.TrimEnd('\r'));
            Console.WriteLine($"... ({sourceLines.Length - 100} lines omitted) ...");
            foreach (var line in sourceLines.TakeLast(20))
                Console.WriteLine(line.TrimEnd('\r'));
        }

        var compiled = SolutionExplorationCompiler.Compile(generated);
        Console.WriteLine();
        Console.WriteLine($"Compiled in-memory assembly: {compiled.Assembly.GetName().Name}");
        Console.WriteLine("Executing generated Walk (typed property access):");
        Console.WriteLine(compiled.Walk(catalog).TrimEnd());
    }

    PrintDiagnostics(catalog.Diagnostics);
    return HasErrors(catalog.Diagnostics) ? 1 : 0;
}
finally
{
    if (fixtureRoot is not null && Directory.Exists(fixtureRoot))
        Directory.Delete(fixtureRoot, recursive: true);
}

static DemoOptions ParseOptions(string[] commandLine)
{
    var evaluate = HasFlag(commandLine, "--evaluate") || HasFlag(commandLine, "--semantic");
    var semantic = HasFlag(commandLine, "--semantic");
    string? solutionPath = null;
    for (var index = 0; index < commandLine.Length; index++)
    {
        var argument = commandLine[index];
        if (argument is "--configuration" or "--platform" or "--framework" or "--namespace" or "--project")
        {
            index++;
            continue;
        }

        if (argument.StartsWith('-'))
            continue;

        solutionPath = argument;
        break;
    }

    if (!string.IsNullOrWhiteSpace(GetOption(commandLine, "--namespace")))
    {
        evaluate = true;
        semantic = true;
    }

    return new DemoOptions(
        solutionPath,
        evaluate,
        semantic,
        GetOption(commandLine, "--configuration") ?? "Debug",
        GetOption(commandLine, "--platform") ?? "AnyCPU",
        GetOption(commandLine, "--framework"),
        GetOption(commandLine, "--namespace"),
        GetOption(commandLine, "--project"));
}

static GitRepositoryWorkspace? TryFindRepositoryWorkspace(IFileSystem fileSystem, SolutionWorkspace solution)
{
    var directory = new DirectoryInfo(solution.Root.FullName);
    while (directory is not null)
    {
        var gitMarker = Path.Combine(directory.FullName, ".git");
        if (Directory.Exists(gitMarker) || File.Exists(gitMarker))
        {
            return new GitRepositoryWorkspace(
                fileSystem.DirectoryInfo.New(directory.FullName),
                directory.Name,
                GitWorktreeKind.Main);
        }

        directory = directory.Parent;
    }

    return null;
}

static string CreateDemoFixture()
{
    var root = Path.Combine(Path.GetTempPath(), "novolis-dotnet-demo-" + Guid.NewGuid().ToString("N"));
    var src = Path.Combine(root, "src");
    var lib = Path.Combine(src, "DemoLib");
    var app = Path.Combine(src, "DemoApp");
    Directory.CreateDirectory(lib);
    Directory.CreateDirectory(app);

    File.WriteAllText(Path.Combine(src, "Fixture.slnx"), """
        <Solution>
          <Folder Name="src">
            <Project Path="DemoLib/DemoLib.csproj" />
            <Project Path="DemoApp/DemoApp.csproj" />
          </Folder>
        </Solution>
        """);
    File.WriteAllText(Path.Combine(lib, "DemoLib.csproj"), """
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <TargetFramework>net10.0</TargetFramework>
            <ImplicitUsings>enable</ImplicitUsings>
            <Nullable>enable</Nullable>
          </PropertyGroup>
        </Project>
        """);
    File.WriteAllText(Path.Combine(lib, "Widget.cs"), """
        namespace Novolis.Sample;

        public sealed class Widget
        {
            public required string Name { get; init; }
        }

        public enum WidgetKind
        {
            Gadget = 0,
        }

        internal sealed class HiddenPart
        {
        }
        """);
    File.WriteAllText(Path.Combine(app, "DemoApp.csproj"), """
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <OutputType>Exe</OutputType>
            <TargetFramework>net10.0</TargetFramework>
            <ImplicitUsings>enable</ImplicitUsings>
            <Nullable>enable</Nullable>
          </PropertyGroup>
          <ItemGroup>
            <ProjectReference Include="..\DemoLib\DemoLib.csproj" />
          </ItemGroup>
        </Project>
        """);
    File.WriteAllText(Path.Combine(app, "Program.cs"), """
        using Novolis.Sample;

        var widget = new Widget { Name = "lab" };
        Console.WriteLine($"{widget.Name}:{WidgetKind.Gadget}");
        """);

    return root;
}

static string? ReadProperty(EvaluatedProject evaluation, string name) =>
    evaluation.Properties.TryGetValue(name, out var value) ? value : null;

static string? GetOption(IEnumerable<string> commandLine, string name)
{
    var arguments = commandLine.ToArray();
    for (var index = 0; index < arguments.Length - 1; index++)
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

static void WriteSection(string title)
{
    Console.WriteLine();
    Console.WriteLine($"== {title} ==");
}

static void PrintDiagnostics(IEnumerable<WorkspaceDiagnostic> diagnostics)
{
    foreach (var diagnostic in diagnostics)
        Console.Error.WriteLine($"{diagnostic.Severity} {diagnostic.Code}: {diagnostic.Message} ({diagnostic.Path})");
}

static void PrintUsage()
{
    Console.WriteLine("""
        TypedSolutionIntelligence
          [<solution.slnx>] [--evaluate] [--semantic]
                            [--configuration Debug] [--platform AnyCPU] [--framework net10.0]
                            [--namespace Novolis.Sample] [--project path.csproj]

        With no solution path, the lab materializes a tiny fixture and walks every layer
        including MSBuild evaluation, Roslyn facts, Git relation, and catalog queries.

        Against a real solution the default is topology only.
        --evaluate enables MSBuild evaluation without running build targets.
        --semantic enables evaluation, Roslyn loading, and dynamic compilation of a
        typed exploration façade (solution.Projects.<Project>.<Namespace>.<Type>).
        --namespace implies --semantic and queries FindTypesInNamespace.
        """);
}

internal sealed record DemoOptions(
    string? SolutionPath,
    bool Evaluate,
    bool Semantic,
    string Configuration,
    string Platform,
    string? Framework,
    string? QueryNamespace,
    string? ProjectPath);
