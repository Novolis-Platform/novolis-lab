using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Novolis.Avalonia.GraphicalProfile;
using ChannelLab.Windows;
using Microsoft.Extensions.DependencyInjection;

namespace ChannelLab;

public sealed class App : Application
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
            desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
            desktop.MainWindow = Program.ApplicationHost.Services.GetRequiredService<ControlWindow>();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
