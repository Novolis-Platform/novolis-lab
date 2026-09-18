using Avalonia;
using Avalonia.Win32;
using KatoriLab.Agent;
using KatoriLab.Demo;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Novolis.Agent.Surface;
using Novolis.Avalonia.ThreeD.Session;

namespace KatoriLab;

internal static class Program
{
    internal static IHost ApplicationHost { get; private set; } = null!;
    internal static AgentSurface? KatoriSurface { get; private set; }

    [STAThread]
    public static void Main(string[] args)
    {
        if (args.Any(a => string.Equals(a, "--kata-smoke", StringComparison.OrdinalIgnoreCase)))
        {
            Environment.ExitCode = KataSmoke.Run();
            return;
        }

        if (args.Any(a => string.Equals(a, "--agent-explore", StringComparison.OrdinalIgnoreCase)))
        {
            Environment.ExitCode = AgentExplore.Run();
            return;
        }

        ApplicationHost = Host.CreateDefaultBuilder(args)
            .ConfigureServices(services =>
            {
                services.AddSingleton<KatoriKataDriver>();
                services.AddSingleton(_ => new SceneSessionService { AppId = "katorilab" });
                services.AddSingleton(sp => new KatoriLabAgentHost(sp.GetRequiredService<KatoriKataDriver>()));
                services.AddTransient<MainWindow>();
            })
            .Build();

        ApplicationHost.Start();
        try
        {
            var host = ApplicationHost.Services.GetRequiredService<KatoriLabAgentHost>();
            KatoriSurface = AgentSurface.AttachAll(host, KatoriLabSessionContract.Definition)
                            ?? AgentSurface.TryAttachFromEnvironment(host, KatoriLabSessionContract.Definition);
            if (KatoriSurface is not null)
                Console.WriteLine("KatoriLab agent surface on http://127.0.0.1:18797 (ken kata / sampleholds / explore).");
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            if (KatoriSurface is not null)
                KatoriSurface.DisposeAsync().AsTask().GetAwaiter().GetResult();
            ApplicationHost.StopAsync().GetAwaiter().GetResult();
        }
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .With(new Win32PlatformOptions
            {
                RenderingMode = [Win32RenderingMode.Wgl, Win32RenderingMode.Software],
            })
            .LogToTrace();
}
