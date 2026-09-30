using GeoPolity;
using GeoPolity.AvaloniaUi;
using GeoPolity.Session;

var options = CliOptions.Parse(args);
var session = GeoSession.LoadDefault();

if (options.Headless)
{
    await HeadlessReport.RunAsync(session, options.Years, options.AttachAgent);
    return;
}

if (options.Mode == UiMode.Spectre)
{
    await SpectreShell.RunAsync(session, attachSessionAgent: true);
    return;
}

Environment.ExitCode = GeoPolityAvaloniaHost.Run(session, attachSessionAgent: true);

internal enum UiMode
{
    Avalonia,
    Spectre,
}

internal sealed class CliOptions
{
    public UiMode Mode { get; init; } = UiMode.Avalonia;
    public bool Headless { get; init; }
    public int Years { get; init; } = 50;
    public bool AttachAgent { get; init; }

    public static CliOptions Parse(string[] argv)
    {
        var mode = UiMode.Avalonia;
        var headless = false;
        var years = 50;
        var attachAgent = false;
        var modeExplicit = false;

        for (var i = 0; i < argv.Length; i++)
        {
            var a = argv[i];
            if (a.Equals("--headless", StringComparison.OrdinalIgnoreCase)
                || a.Equals("headless", StringComparison.OrdinalIgnoreCase))
            {
                headless = true;
            }
            else if (a.Equals("--agent", StringComparison.OrdinalIgnoreCase))
            {
                attachAgent = true;
            }
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
            else if (a.Equals("--years", StringComparison.OrdinalIgnoreCase) && i + 1 < argv.Length
                     && int.TryParse(argv[i + 1], out var y))
            {
                years = Math.Max(1, y);
                i++;
            }
            else if (a.StartsWith("--years=", StringComparison.OrdinalIgnoreCase)
                     && int.TryParse(a["--years=".Length..], out var y2))
            {
                years = Math.Max(1, y2);
            }
            else if (a.Equals("--live", StringComparison.OrdinalIgnoreCase)
                     || a.Equals("play", StringComparison.OrdinalIgnoreCase))
            {
                // interactive UI
            }
            else if (a.Equals("spectre", StringComparison.OrdinalIgnoreCase))
            {
                mode = UiMode.Spectre;
                modeExplicit = true;
            }
        }

        if (!headless && !modeExplicit && (Console.IsOutputRedirected || Console.IsInputRedirected))
        {
            var forceLive = argv.Any(x =>
                x.Equals("--live", StringComparison.OrdinalIgnoreCase)
                || x.Equals("play", StringComparison.OrdinalIgnoreCase)
                || x.Equals("--mode", StringComparison.OrdinalIgnoreCase)
                || x.StartsWith("--mode=", StringComparison.OrdinalIgnoreCase));
            if (!forceLive)
            {
                headless = true;
            }
        }

        return new CliOptions
        {
            Mode = mode,
            Headless = headless,
            Years = years,
            AttachAgent = attachAgent,
        };
    }

    private static UiMode ParseMode(string value) =>
        value.Equals("spectre", StringComparison.OrdinalIgnoreCase)
            ? UiMode.Spectre
            : UiMode.Avalonia;
}
