using Avalonia;
using RepoStudio;
using RepoStudio.Cli;

var options = CliOptions.Parse(args);

if (options.Mode == UiMode.Daemon)
{
    await DaemonHost.RunAsync(options);
    return;
}

if (options.Mode == UiMode.Spectre || options.Headless)
{
    Environment.ExitCode = await SpectreHost.RunAsync(options);
    return;
}

BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

static AppBuilder BuildAvaloniaApp()
    => AppBuilder.Configure<App>()
        .UsePlatformDetect()
        .LogToTrace();
