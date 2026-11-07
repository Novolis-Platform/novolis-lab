using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;
using CalypsoCad.Host;
using CalypsoCad.Rendering;
using Novolis.Avalonia.GraphicalProfile;

namespace CalypsoCad.Shell;

internal sealed class CalypsoExplorerApp : Application
{
    private readonly CalypsoSession _session;
    private readonly CalypsoRenderer _renderer;
    private readonly CalypsoViewportController _controller;

    public CalypsoExplorerApp(
        CalypsoSession session,
        CalypsoRenderer renderer,
        CalypsoViewportController controller)
    {
        _session = session;
        _renderer = renderer;
        _controller = controller;
    }

    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
        GraphicalProfile.Install(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _ = _session;
            _ = _renderer;
            desktop.MainWindow = new CalypsoExplorerWindow(_controller);
            desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
        }

        base.OnFrameworkInitializationCompleted();
    }
}
