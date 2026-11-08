using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;
using Novolis.Avalonia.Cad.Session;
using Novolis.Avalonia.GraphicalProfile;

namespace CalypsoCad.Host;

internal sealed class CalypsoCadApp : Application
{
    private readonly CadSessionService _session;

    public CalypsoCadApp(CadSessionService session) =>
        _session = session ?? throw new ArgumentNullException(nameof(session));

    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
        GraphicalProfile.Install(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = new CalypsoCadMainWindow(_session);

        base.OnFrameworkInitializationCompleted();
    }
}
