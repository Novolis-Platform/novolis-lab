namespace CalypsoCad.Generation;

/// <summary>
/// Forwards to <see cref="CalypsoLockGenerator"/>. The 65 m Rev G room inventory is not a second hull.
/// </summary>
internal static class CalypsoRevGGenerator
{
    public static string DefaultOutputDirectory => CalypsoLockGenerator.DefaultOutputDirectory;

    public static string Generate(string? outputDirectory = null) =>
        CalypsoLockGenerator.Generate(outputDirectory);
}
