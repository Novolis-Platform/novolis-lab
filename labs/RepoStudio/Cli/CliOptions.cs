namespace RepoStudio.Cli;

internal enum UiMode
{
    Avalonia,
    Spectre,
    Daemon,
}

internal sealed class CliOptions
{
    public UiMode Mode { get; init; } = UiMode.Avalonia;
    public bool Headless { get; init; }
    public bool Json { get; init; }
    public string? Root { get; init; }
    public string[] Args { get; init; } = [];
    public int IntervalSeconds { get; init; } = 600;

    public static CliOptions Parse(string[] argv)
    {
        var mode = UiMode.Avalonia;
        var headless = false;
        var json = false;
        string? root = null;
        var interval = 600;
        var modeExplicit = false;
        var rest = new List<string>();

        for (var i = 0; i < argv.Length; i++)
        {
            var a = argv[i];
            if (a.Equals("--json", StringComparison.OrdinalIgnoreCase))
                json = true;
            else if (a.Equals("--headless", StringComparison.OrdinalIgnoreCase))
                headless = true;
            else if (a.Equals("--mode", StringComparison.OrdinalIgnoreCase) && i + 1 < argv.Length)
            {
                mode = ParseMode(argv[++i]);
                modeExplicit = true;
            }
            else if (a.StartsWith("--mode=", StringComparison.OrdinalIgnoreCase))
            {
                mode = ParseMode(a["--mode=".Length..]);
                modeExplicit = true;
            }
            else if (a.Equals("--root", StringComparison.OrdinalIgnoreCase) && i + 1 < argv.Length)
                root = argv[++i];
            else if (a.StartsWith("--root=", StringComparison.OrdinalIgnoreCase))
                root = a["--root=".Length..];
            else if (a.Equals("--interval", StringComparison.OrdinalIgnoreCase) && i + 1 < argv.Length
                     && int.TryParse(argv[++i], out var sec))
                interval = Math.Max(30, sec);
            else if (a.Equals("spectre", StringComparison.OrdinalIgnoreCase))
            {
                mode = UiMode.Spectre;
                modeExplicit = true;
            }
            else if (a.Equals("daemon", StringComparison.OrdinalIgnoreCase))
            {
                mode = UiMode.Daemon;
                modeExplicit = true;
            }
            else
                rest.Add(a);
        }

        if (!headless && !modeExplicit && (Console.IsOutputRedirected || Console.IsInputRedirected))
            headless = true;

        if (headless && mode == UiMode.Avalonia)
            mode = UiMode.Spectre;

        return new CliOptions
        {
            Mode = mode,
            Headless = headless,
            Json = json,
            Root = root,
            IntervalSeconds = interval,
            Args = rest.ToArray(),
        };
    }

    static UiMode ParseMode(string value) =>
        value.Equals("spectre", StringComparison.OrdinalIgnoreCase) ? UiMode.Spectre
        : value.Equals("daemon", StringComparison.OrdinalIgnoreCase) ? UiMode.Daemon
        : UiMode.Avalonia;
}
