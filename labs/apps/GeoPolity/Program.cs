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
