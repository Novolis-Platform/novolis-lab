namespace TinyExprBindings;

internal static class RepoRoot
{
    private const string SolutionFile = "Novolis.Lab.slnx";

    public static string Find()
    {
        var root = FindFrom(Directory.GetCurrentDirectory()) ?? FindFrom(AppContext.BaseDirectory);
        return root ?? throw new DirectoryNotFoundException(
            $"Could not find {SolutionFile} from the working directory or application path.");
    }

    private static string? FindFrom(string startPath)
    {
        for (DirectoryInfo? directory = new(startPath); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, SolutionFile)))
                return directory.FullName;
        }

        return null;
    }
}
