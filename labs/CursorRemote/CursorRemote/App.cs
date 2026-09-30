using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Novolis.Avalonia.GraphicalProfile;
using CursorRemote.Services;
using CursorRemote.Ui;
using Microsoft.Extensions.DependencyInjection;

namespace CursorRemote;

public sealed class App : Application
{
    public static IServiceProvider Services { get; set; } = null!;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        GraphicalProfile.Install(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (Services is null)
        {
            throw new InvalidOperationException(
                "App.Services must be assigned before Avalonia starts.");
        }

        var remoteView = Services.GetRequiredService<RemoteView>();
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                Content = remoteView,
            };
            Novolis.Lab.Branding.AppBrand.ApplyWindowIcon(desktop.MainWindow);
            Services.GetService<IHostDesktopChrome>()?.AttachMainWindow(desktop.MainWindow);
            Services.GetService<HostActivityLog>()?.Info("Host window ready.");
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime single)
        {
            single.MainView = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Top,
                Content = remoteView,
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
