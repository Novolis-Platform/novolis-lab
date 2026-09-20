using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Novolis.Lab.Manifest;

internal static class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true,
    };

    private static readonly HashSet<string> CategoryDirectories =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "astro",
            "audio",
            "avalonia",
            "cad",
            "civics",
            "codegen",
            "documents",
            "economy",
            "gaming",
            "io",
            "manuscript",
            "raylib",
            "rendering",
            "workspaces",
        };

    private static int Main(string[] args)
    {
        var command = args.FirstOrDefault()?.ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(command))
        {
            PrintUsage();
            return 2;
        }

        var repo = GetOption(args, "--repo") ?? FindRepoRoot();
        var manifestPath = GetOption(args, "--manifest")
            ?? Path.Combine(repo, "build", "labs.json");

        try
        {
            return command switch
            {
                "generate" => Generate(repo, manifestPath),
                "validate" => Validate(Load(manifestPath), repo),
                "list" => List(Load(manifestPath)),
                "ci-matrix" => EmitCiMatrix(
                    Load(manifestPath),
                    GetChangedFiles(args)),
                _ => Unknown(command),
            };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private static int Generate(string repo, string manifestPath)
    {
        var manifest = new LabManifest
        {
            SchemaVersion = 1,
            Repository = "novolis-lab",
            Labs = Discover(repo),
        };

        Directory.CreateDirectory(Path.GetDirectoryName(manifestPath)!);
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest, JsonOptions) + Environment.NewLine);
        Console.WriteLine($"Generated {manifest.Labs.Count} lab entries: {manifestPath}");
        return 0;
    }

    private static List<LabEntry> Discover(string repo)
    {
        var labsRoot = Path.Combine(repo, "labs");
        if (!Directory.Exists(labsRoot))
            throw new DirectoryNotFoundException($"Lab root does not exist: {labsRoot}");

        var groups = new Dictionary<string, LabEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var projectPath in Directory.EnumerateFiles(labsRoot, "*.csproj", SearchOption.AllDirectories)
                     .Where(path => path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                         .All(part => !string.Equals(part, "shared", StringComparison.OrdinalIgnoreCase)
                             && !string.Equals(part, "bin", StringComparison.OrdinalIgnoreCase)
                             && !string.Equals(part, "obj", StringComparison.OrdinalIgnoreCase))))
        {
            var relativeProject = Normalize(Path.GetRelativePath(repo, projectPath));
            var relativeToLabs = Normalize(Path.GetRelativePath(labsRoot, projectPath));
            var segments = relativeToLabs.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0)
                continue;

            var rootSegments = GetLabRootSegments(segments);
            var root = string.Join('/', rootSegments);
            var key = string.Join('-', rootSegments);
            var entry = groups.GetValueOrDefault(key);
            if (entry is null)
            {
                entry = new LabEntry
                {
                    Key = key,
                    DisplayName = string.Join(' ', rootSegments.Select(SplitWords)),
                    ChangedPathGlobs =
                    [
                        $"labs/{root}/**",
                        "labs/shared/**",
                    ],
                };
                groups.Add(key, entry);
            }

            var isTest = Regex.IsMatch(
                Path.GetFileNameWithoutExtension(projectPath),
                @"(?i)(\.Tests|\.Unit)$")
                || relativeToLabs.Contains("/tests/", StringComparison.OrdinalIgnoreCase);

            if (isTest)
                entry.Tests.Add(relativeProject);
            else
                entry.Projects.Add(relativeProject);
        }

        return groups.Values
            .Where(entry => entry.Projects.Count > 0)
            .OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase)
            .Select(entry =>
            {
                entry.Projects.Sort(StringComparer.OrdinalIgnoreCase);
                entry.Tests.Sort(StringComparer.OrdinalIgnoreCase);
                return entry;
            })
            .ToList();
    }

    private static string[] GetLabRootSegments(string[] segments)
    {
        if (segments.Length >= 2 && CategoryDirectories.Contains(segments[0]))
        {
            var name = Regex.Replace(segments[1], @"(?i)\.Tests$", string.Empty);
            return [segments[0], name];
        }

        var directName = Regex.Replace(segments[0], @"(?i)\.Tests$", string.Empty);
        return [directName];
    }

    private static string SplitWords(string value) =>
        Regex.Replace(value.Replace('-', ' '), @"(?<=[a-z])(?=[A-Z])", " ");

    private static int Validate(LabManifest manifest, string repo)
    {
        var errors = new List<string>();
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var lab in manifest.Labs)
        {
            if (!keys.Add(lab.Key))
                errors.Add($"Duplicate lab key: {lab.Key}");
            if (lab.Projects.Count == 0)
                errors.Add($"{lab.Key}: no projects declared");
            if (!lab.ChangedPathGlobs.Any())
                errors.Add($"{lab.Key}: changedPathGlobs must not be empty");

            foreach (var project in lab.Projects.Concat(lab.Tests))
            {
                var fullPath = Resolve(repo, project);
                if (!File.Exists(fullPath))
                {
                    errors.Add($"{lab.Key}: project does not exist: {project}");
                    continue;
                }

                var text = File.ReadAllText(fullPath);
                if (Regex.IsMatch(text, @"<PackAsTool>\s*true\s*</PackAsTool>", RegexOptions.IgnoreCase))
                    errors.Add($"{project}: PackAsTool is forbidden in labs");
                if (Regex.IsMatch(text, @"<IsPackable>\s*true\s*</IsPackable>", RegexOptions.IgnoreCase))
                    errors.Add($"{project}: packable projects are forbidden in labs");
                if (Regex.IsMatch(text, @"<ProjectReference\s+Include=""[^""]*submodules[/\\]",
                        RegexOptions.IgnoreCase))
                {
                    errors.Add($"{project}: submodule ProjectReference is forbidden; use ProjectReference mode mapping");
                }
            }
        }

        if (errors.Count > 0)
        {
            foreach (var error in errors)
                Console.Error.WriteLine(error);
            return 1;
        }

        Console.WriteLine($"labs manifest valid ({manifest.Labs.Count} labs)");
        return 0;
    }

    private static int List(LabManifest manifest)
    {
        foreach (var lab in manifest.Labs)
            Console.WriteLine($"{lab.Key}\t{string.Join(';', lab.Projects)}");
        return 0;
    }

    private static int EmitCiMatrix(LabManifest manifest, IReadOnlyList<string> changedFiles)
    {
        var all = changedFiles.Count == 0
            || changedFiles.Any(path =>
                string.Equals(path, "build/labs.json", StringComparison.OrdinalIgnoreCase)
                || string.Equals(path, "Directory.Packages.props", StringComparison.OrdinalIgnoreCase)
                || string.Equals(path, "Directory.Build.props", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith(".github/workflows/", StringComparison.OrdinalIgnoreCase));

        var selected = all
            ? manifest.Labs
            : manifest.Labs.Where(lab => lab.ChangedPathGlobs.Any(glob =>
                changedFiles.Any(file => Matches(file, glob)))).ToList();

        var selectedList = selected.ToList();
        var matrix = new
        {
            skip_build = selectedList.Count == 0,
            include = selectedList.Select(lab => new
            {
                key = lab.Key,
                projects = lab.Projects.Select(Normalize).ToArray(),
                tests = lab.Tests.Select(Normalize).ToArray(),
            }).ToArray(),
        };

        Console.WriteLine(JsonSerializer.Serialize(matrix));
        return 0;
    }

    private static LabManifest Load(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"Labs manifest not found: {path}");
        return JsonSerializer.Deserialize<LabManifest>(File.ReadAllText(path), JsonOptions)
            ?? throw new InvalidOperationException("Failed to parse labs manifest.");
    }

    private static bool Matches(string file, string glob)
    {
        var expression = Normalize(glob)
            .Replace(".", "\\.")
            .Replace("**", "\u0000")
            .Replace("*", "[^/]*")
            .Replace("\u0000", ".*");
        return Regex.IsMatch(
            Normalize(file),
            $"^{expression.TrimEnd('/')}(?:/.*)?$",
            RegexOptions.IgnoreCase);
    }

    private static IReadOnlyList<string> GetChangedFiles(string[] args)
    {
        if (args.Any(arg => string.Equals(arg, "--all", StringComparison.OrdinalIgnoreCase)))
            return [];

        var files = GetRepeatedOption(args, "--changed-file").ToList();
        var listPath = GetOption(args, "--changed-files");
        if (!string.IsNullOrWhiteSpace(listPath) && File.Exists(listPath))
            files.AddRange(File.ReadLines(listPath));
        return files;
    }

    private static string Resolve(string repo, string relative) =>
        Path.GetFullPath(Path.Combine(repo, relative.Replace('/', Path.DirectorySeparatorChar)));

    private static string Normalize(string path) => path.Replace('\\', '/').TrimStart('/');

    private static string? GetOption(string[] args, string name)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        }

        return null;
    }

    private static IReadOnlyList<string> GetRepeatedOption(string[] args, string name)
    {
        var values = new List<string>();
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                values.Add(args[i + 1]);
        }

        return values;
    }

    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "build", "labs.json")))
                return current.FullName;
            current = current.Parent;
        }

        return Directory.GetCurrentDirectory();
    }

    private static int Unknown(string command)
    {
        Console.Error.WriteLine($"Unknown command '{command}'.");
        PrintUsage();
        return 2;
    }

    private static void PrintUsage() =>
        Console.Error.WriteLine(
            "Usage: LabsManifest <generate|validate|list|ci-matrix> [--repo PATH] [--all|--changed-files PATH]");

    private sealed class LabManifest
    {
        public int SchemaVersion { get; init; }
        public string Repository { get; init; } = string.Empty;
        public List<LabEntry> Labs { get; init; } = [];
    }

    private sealed class LabEntry
    {
        public string Key { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
        public List<string> Projects { get; init; } = [];
        public List<string> Tests { get; init; } = [];
        public List<string> ChangedPathGlobs { get; init; } = [];
    }
}
