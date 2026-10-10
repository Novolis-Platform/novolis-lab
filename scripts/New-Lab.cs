// Scaffold a new lab host and register it in build/labs.json.
//
//   dotnet run --file d:\novolis\novolis-lab\scripts\New-Lab.cs -- --name FooLab --stack console

#:property TargetFramework=net10.0
#:property PublishAot=false
#:property PackAsTool=false
#:property ImplicitUsings=enable
#:property Nullable=enable
#:property ManagePackageVersionsCentrally=false

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

var name = Arg(args, "--name", "-Name");
var category = Arg(args, "--category", "-Category");
var stack = Arg(args, "--stack", "-Stack");
var force = args.Any(a => a is "--force" or "-Force");
if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(category) || string.IsNullOrWhiteSpace(stack))
{
    Console.Error.WriteLine("Usage: New-Lab --name Name --category simulation --stack avalonia|raylib|spectre|console [--force]");
    return 2;
}

if (!Regex.IsMatch(name, "^[A-Za-z][A-Za-z0-9.-]*$")
    || !Regex.IsMatch(category, "^[a-z][a-z0-9-]*$"))
{
    Console.Error.WriteLine("Name must start with a letter. Category is the repo name with the novolis- prefix removed (lowercase).");
    return 2;
}

var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "avalonia", "raylib", "spectre", "console" };
if (!allowed.Contains(stack))
{
    Console.Error.WriteLine("Stack must be avalonia, raylib, spectre, or console.");
    return 2;
}

var repoRoot = Directory.GetParent(Path.GetDirectoryName(ThisFile())!)!.FullName;
var labPath = Path.Combine(repoRoot, "labs", category, name);
var projectPath = Path.Combine(labPath, $"{name}.csproj");
var manifestPath = Path.Combine(repoRoot, "build", "labs.json");
var solutionPath = Path.Combine(repoRoot, "Novolis.Lab.slnx");
if (Directory.Exists(labPath) && !force)
{
    Console.Error.WriteLine($"The lab already exists: {labPath}. Use --force only when replacing it intentionally.");
    return 1;
}

Directory.CreateDirectory(labPath);
File.WriteAllText(projectPath, $"""
    <Project Sdk="Microsoft.NET.Sdk">
      <PropertyGroup>
        <OutputType>Exe</OutputType>
        <TargetFramework>net10.0</TargetFramework>
        <RootNamespace>Novolis.Lab.{name}</RootNamespace>
        <AssemblyName>Novolis.Lab.{name}</AssemblyName>
      </PropertyGroup>
    </Project>
    """, new UTF8Encoding(false));
File.WriteAllText(Path.Combine(labPath, "Program.cs"), $"Console.WriteLine(\"Novolis lab: {name}\");{Environment.NewLine}", new UTF8Encoding(false));
File.WriteAllText(Path.Combine(labPath, "README.md"), $"""
    # {name}

    Experimental **{stack}** lab host in `novolis-lab`.

    Run it from the repository root:

    ```powershell
    dotnet run --project labs/{category}/{name}/{name}.csproj
    ```

    This host is not a release artifact. Graduate a successful experiment into
    `novolis-tools`, `novolis-utilities`, or `novolis-apps`.
    """, new UTF8Encoding(false));

if (!File.Exists(manifestPath))
{
    Console.Error.WriteLine($"Labs manifest not found: {manifestPath}");
    return 1;
}

var manifest = JsonNode.Parse(File.ReadAllText(manifestPath))!.AsObject();
var labs = manifest["Labs"]!.AsArray();
var key = $"{category}-{name}";
if (labs.Any(l => string.Equals(l?["Key"]?.GetValue<string>(), key, StringComparison.OrdinalIgnoreCase)))
{
    Console.Error.WriteLine($"The labs manifest already contains '{key}'.");
    return 1;
}

labs.Add(new JsonObject
{
    ["Key"] = key,
    ["DisplayName"] = Regex.Replace(name, "(?<=[a-z])(?=[A-Z])", " "),
    ["Projects"] = new JsonArray($"labs/{category}/{name}/{name}.csproj"),
    ["Tests"] = new JsonArray(),
    ["ChangedPathGlobs"] = new JsonArray($"labs/{category}/{name}/**", "labs/shared/**"),
});
File.WriteAllText(manifestPath, manifest.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

var psi = new ProcessStartInfo("dotnet")
{
    WorkingDirectory = repoRoot,
    UseShellExecute = false,
    CreateNoWindow = true,
};
psi.ArgumentList.Add("sln");
psi.ArgumentList.Add(solutionPath);
psi.ArgumentList.Add("add");
psi.ArgumentList.Add(projectPath);
psi.ArgumentList.Add("--solution-folder");
psi.ArgumentList.Add(category);
using var p = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start dotnet");
p.WaitForExit();
if (p.ExitCode != 0)
{
    Console.Error.WriteLine($"Could not add {projectPath} to {solutionPath}.");
    return p.ExitCode;
}

Console.WriteLine($"Created lab {name} ({stack}): {labPath}");
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
