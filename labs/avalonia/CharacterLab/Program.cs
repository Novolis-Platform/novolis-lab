using Avalonia;
using Avalonia.Win32;
using CharacterLab.Agent;
using CharacterLab.Demo;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Novolis.Agent.Surface;
using Novolis.Avalonia.ThreeD.Session;

namespace CharacterLab;

internal static class Program
{
    internal static IHost ApplicationHost { get; private set; } = null!;
    internal static AgentSurface? CharacterSurface { get; private set; }

    [STAThread]
    public static void Main(string[] args)
    {
        if (args.Any(a => string.Equals(a, "--drill-smoke", StringComparison.OrdinalIgnoreCase)))
        {
            Environment.ExitCode = DrillSmoke.Run();
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
                services.AddSingleton<MocapParadeDriver>();
                services.AddSingleton(_ => new SceneSessionService { AppId = "characterlab" });
                services.AddSingleton(sp => new CharacterLabAgentHost(sp.GetRequiredService<MocapParadeDriver>()));
                services.AddTransient<MainWindow>();
            })
            .Build();

        ApplicationHost.Start();
        try
        {
            var characterHost = ApplicationHost.Services.GetRequiredService<CharacterLabAgentHost>();
            CharacterSurface = AgentSurface.AttachAll(characterHost, CharacterLabSessionContract.Definition)
                               ?? AgentSurface.TryAttachFromEnvironment(characterHost, CharacterLabSessionContract.Definition);
            if (CharacterSurface is not null)
                Console.WriteLine("CharacterLab agent surface on http://127.0.0.1:18795 (mocap wire / sampleholds / explore).");
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            if (CharacterSurface is not null)
                CharacterSurface.DisposeAsync().AsTask().GetAwaiter().GetResult();
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
