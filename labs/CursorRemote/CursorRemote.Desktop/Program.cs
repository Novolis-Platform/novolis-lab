using System.Runtime.Versioning;
using Avalonia;
using CursorRemote.Desktop.Host;
using CursorRemote.Services;
using CursorRemote.Ui;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Novolis.Avalonia.Mobile.Desktop;

namespace CursorRemote.Desktop;

[SupportedOSPlatform("windows")]
internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        using var host = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder(args)
            .ConfigureServices(services =>
            {
                services.AddNovolisMobileDesktop("CursorRemote");
                services.AddSingleton<HostActivityLog>();
                services.AddSingleton<HostShellOptions>();
                services.AddSingleton<HostTrayController>();
                services.AddSingleton<IHostDesktopChrome>(sp => sp.GetRequiredService<HostTrayController>());
                services.AddSingleton<CursorRemoteHost>();
                services.AddHostedService(sp => sp.GetRequiredService<CursorRemoteHost>());
                services.AddSingleton<IRemoteControlSession, DesktopRemoteControlSession>();
                services.AddSingleton<RemoteView>();
            })
            .Build();

        App.Services = host.Services;
        host.Start();
        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            host.Services.GetService<HostTrayController>()?.Dispose();
            host.StopAsync().GetAwaiter().GetResult();
        }
    }

    private static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}
