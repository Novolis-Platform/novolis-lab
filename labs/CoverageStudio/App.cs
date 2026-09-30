using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Microsoft.Extensions.DependencyInjection;
using Novolis.Avalonia.GraphicalProfile;

namespace CoverageStudio;

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
        {
            desktop.MainWindow = Program.ApplicationHost.Services.GetRequiredService<MainWindow>();
            Novolis.Lab.Branding.AppBrand.ApplyWindowIcon(desktop.MainWindow);
        }

        base.OnFrameworkInitializationCompleted();
    }
}
