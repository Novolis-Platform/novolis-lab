// Emit a graduation checklist and destination manifest fragment. Does not copy files.
//
//   dotnet run --file d:\novolis\novolis-lab\scripts\Graduate-Lab.cs -- --name FooLab --to tools

#:property TargetFramework=net10.0
#:property PublishAot=false
#:property PackAsTool=false
#:property ImplicitUsings=enable
#:property Nullable=enable
#:property ManagePackageVersionsCentrally=false

using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

var name = Arg(args, "--name", "-Name");
var to = Arg(args, "--to", "-To");
var outputPath = Arg(args, "--out", "-OutputPath");
if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(to))
{
    Console.Error.WriteLine("Usage: Graduate-Lab --name Name --to tools|utilities|apps");
    return 2;
}

var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "tools", "utilities", "apps" };
if (!allowed.Contains(to))
{
    Console.Error.WriteLine("Destination must be tools, utilities, or apps.");
    return 2;
}

var repoRoot = Directory.GetParent(Path.GetDirectoryName(ThisFile())!)!.FullName;
var labsRoot = Path.Combine(repoRoot, "labs");
var labRoots = Directory.EnumerateDirectories(labsRoot, name, SearchOption.AllDirectories)
    .Where(p => !p.Replace('/', '\\').Contains(@"\shared\", StringComparison.OrdinalIgnoreCase))
    .ToArray();
if (labRoots.Length == 0)
{
    Console.Error.WriteLine($"Lab '{name}' was not found under {labsRoot}.");
    return 1;
}

if (labRoots.Length > 1)
{
    Console.Error.WriteLine($"Lab name '{name}' is ambiguous: {string.Join(", ", labRoots)}");
    return 1;
}

var sourceRoot = labRoots[0];
var projects = Directory.EnumerateFiles(sourceRoot, "*.csproj", SearchOption.AllDirectories).ToArray();
if (projects.Length == 0)
{
    Console.Error.WriteLine($"Lab '{name}' does not contain a project.");
    return 1;
}

var shared = new List<string>();
var includeRx = new Regex(@"<ProjectReference\b[^>]*\bInclude\s*=\s*""([^""]+)""", RegexOptions.IgnoreCase);
foreach (var project in projects)
{
    foreach (Match match in includeRx.Matches(File.ReadAllText(project)))
    {
        var include = match.Groups[1].Value;
        if (Regex.IsMatch(include, @"(?i)(?:[\\/]|^)shared(?:[\\/]|$)|Novolis\.(?:Lab|Dogfooding)\."))
            shared.Add($"{project}: {include}");
    }
}

if (shared.Count > 0)
{
    Console.Error.WriteLine($"Cannot graduate '{name}' while it references lab-only shared projects:");
    foreach (var s in shared)
        Console.Error.WriteLine(s);
    return 1;
}

var workspaceRoot = Directory.GetParent(repoRoot)!.FullName;
var destinationRoot = Path.Combine(workspaceRoot, $"novolis-{to}", "src", name);
var displayName = Regex.Replace(name, "(?<=[a-z])(?=[A-Z])", " ");
var relativeProject = $"src/{name}/{name}.csproj";
object manifest = to.ToLowerInvariant() switch
{
    "tools" => new
    {
        schemaVersion = 1,
        catalogVersion = "2026.1.0",
        repository = "novolis-tools",
        tools = new[]
        {
            new
            {
                key = name,
                displayName,
                project = relativeProject,
                packageId = $"Novolis.{name}",
                command = $"novolis-{name.ToLowerInvariant()}",
                tests = Array.Empty<string>(),
                changedPathGlobs = new[] { $"src/{name}/**" },
            },
        },
    },
    "utilities" => new
    {
        schemaVersion = 1,
        catalogVersion = "2026.1.0",
        repository = "novolis-utilities",
        utilities = new[]
        {
            new
            {
                key = name,
                displayName,
                project = relativeProject,
                solution = $"src/{name}/{name}.slnx",
                ship = new[] { "windows-zip" },
                tests = Array.Empty<string>(),
                changedPathGlobs = new[] { $"src/{name}/**" },
            },
        },
    },
    _ => new
    {
        schemaVersion = 1,
        manifestVersion = "2026.1.0",
        channels = new { },
        apps = new[]
        {
            new
            {
                key = name,
                choice = name,
                displayName,
                sourceRoot = $"src/{name}",
                solution = $"src/{name}/{name}.slnx",
                artifactPrefix = name,
                stack = "console",
                status = "draft",
            },
        },
    },
};

outputPath ??= Path.Combine(repoRoot, "artifacts", "graduation", name, $"{to}.json");
Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
var json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
File.WriteAllText(outputPath, json, new UTF8Encoding(false));
var checklistPath = Path.ChangeExtension(outputPath, ".md");
File.WriteAllText(checklistPath, $"""
    # Graduate {name} to {to}

    This is a checklist and copy plan. The script does not copy or rewrite the lab.

    Source:

    ```text
    {sourceRoot}
    ```

    Destination:

    ```text
    {destinationRoot}
    ```

    ## Checklist

    - [ ] Copy the host files into the destination repository.
    - [ ] Replace lab-only shared code with package or destination-repo code.
    - [ ] Keep committed cross-repository references as `PackageReference`.
    - [ ] Add the project to the destination solution.
    - [ ] Merge the generated manifest fragment from `{outputPath}`.
    - [ ] Set the destination repository's release metadata and CI coverage.
    - [ ] Remove the lab entry after the destination build is published.

    The source contained {projects.Length} project(s), and no lab-only shared `ProjectReference` was found.
    """, new UTF8Encoding(false));
Console.WriteLine($"Wrote manifest stub: {outputPath}");
Console.WriteLine($"Wrote graduation checklist: {checklistPath}");
Console.WriteLine($"Copy manually from {sourceRoot} to {destinationRoot}.");
return 0;

static string? Arg(string[] args, params string[] names)
{
    for (var i = 0; i < args.Length; i++)
    {
        if (names.Any(n => string.Equals(args[i], n, StringComparison.OrdinalIgnoreCase)) && i + 1 < args.Length)
            return args[i + 1];
    }

    return null;
}

static string ThisFile([CallerFilePath] string path = "") => path;
