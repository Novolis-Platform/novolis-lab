using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Novolis.Avalonia.GraphicalProfile;
using Microsoft.Extensions.DependencyInjection;

namespace SketchLab;

public class App : Application
{
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
        GraphicalProfile.Install(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = Program.ApplicationHost.Services.GetRequiredService<MainWindow>();

        base.OnFrameworkInitializationCompleted();
    }
}
