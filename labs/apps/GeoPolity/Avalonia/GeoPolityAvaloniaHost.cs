using Avalonia;
using GeoPolity.Agent;
using GeoPolity.Session;
using Novolis.Agent.Surface;

namespace GeoPolity.AvaloniaUi;

internal static class GeoPolityAvaloniaHost
{
    public static AgentSurface? SessionSurface { get; private set; }

    public static int Run(GeoSession session, bool attachSessionAgent)
    {
        GeoPolityApp.Session = session;

        if (attachSessionAgent)
        {
            var host = new GeoPolityAgentHost(session);
            SessionSurface = AgentSurface.AttachAll(
                host,
                GeoPolitySessionContract.Definition,
                new AgentAttachOptions
                {
                    EnableIpc = true,
                    EnableHttp = true,
                    EnableTcp = true,
                    EnableRpc = false,
                    HttpPort = 18857,
                    TcpPort = 18858,
                });

            if (SessionSurface?.HttpBaseUrl is { } url)
            {
                Console.WriteLine($"GeoPolity Agent Surface: {url}");
            }

            Console.WriteLine("Avalonia UI agent pipe: novolis-avalonia-agent-geopolity");
        }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime([]);

        SessionSurface?.DisposeAsync().AsTask().GetAwaiter().GetResult();
        GeoPolityApp.UiAgent?.DisposeAsync().AsTask().GetAwaiter().GetResult();
        return 0;
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<GeoPolityApp>()
            .UsePlatformDetect()
            .LogToTrace();
}
