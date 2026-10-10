using Android.App;
using Android.Runtime;
using Avalonia;
using Avalonia.Android;
using CursorRemote.Services;
using CursorRemote.Ui;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Novolis.Avalonia.Mobile.Android;

namespace CursorRemote.Android;

[Application]
public class MainApplication : AvaloniaAndroidApplication<App>
{
    private IHost? _host;

    protected MainApplication(nint javaReference, JniHandleOwnership transfer)
        : base(javaReference, transfer)
    {
    }

    public override void OnCreate()
    {
        _host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddNovolisMobileAndroid("CursorRemote");
                services.AddSingleton<HostActivityLog>();
                services.AddSingleton<HostShellOptions>();
                services.AddSingleton<IRemoteControlSession, AndroidRemoteControlSession>();
                services.AddSingleton<RemoteView>();
            })
            .Build();
        App.Services = _host.Services;
        _host.Start();
        base.OnCreate();
    }

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder) =>
        base.CustomizeAppBuilder(builder).LogToTrace();
}
