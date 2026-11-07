namespace CalypsoCad.Host;

/// <summary>In-repo paths for CalypsoCad renders (source-controlled) vs scratch CAD JSON.</summary>
internal static class CalypsoProjectPaths
{
    /// <summary>labs/cad/CalypsoCad</summary>
    public static string ProjectRoot { get; } = FindProjectRoot();

    /// <summary>Committed headless PNG tree (structure wire, build steps, visual CI).</summary>
    public static string RendersDirectory => Path.Combine(ProjectRoot, "docs", "renders");

    /// <summary>Progressive structure-wire build-step slides.</summary>
    public static string BuildStepsDirectory => Path.Combine(RendersDirectory, "build-steps");

    /// <summary>4K construction-stage orbit video + keyframes.</summary>
    public static string ConstructionOrbitDirectory => Path.Combine(RendersDirectory, "construction-orbit");

    /// <summary>Curated visual-gate baselines (subset of renders).</summary>
    public static string PreviewDirectory => Path.Combine(ProjectRoot, "docs", "preview");

    public static string RendersExportsDirectory => RendersDirectory;

    private static string FindProjectRoot()
    {
        // Same convention as CalypsoLockGenerator / blueprint emitters (bin/Debug/<tfm> → project).
        foreach (var candidate in CandidateRoots())
        {
            if (File.Exists(Path.Combine(candidate, "CalypsoCad.csproj"))
                && Directory.Exists(Path.Combine(candidate, "docs")))
                return candidate;
        }

        throw new InvalidOperationException(
            "CalypsoCad project root not found (expected CalypsoCad.csproj + docs/).");
    }

    private static IEnumerable<string> CandidateRoots()
    {
        yield return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", ".."));
        yield return @"d:\novolis\novolis-lab\labs\cad\CalypsoCad";

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            yield return dir.FullName;
            dir = dir.Parent;
        }
    }
}
