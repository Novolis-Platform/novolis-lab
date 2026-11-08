using Avalonia;
using Avalonia.Win32;
using Novolis.Avalonia.Cad.Session;

namespace CalypsoCad.Host;

/// <summary>Avalonia desktop host for the thin CadSessionService composition.</summary>
internal static class CalypsoCadHost
{
    public static void Run(CadSessionService session)
    {
        ArgumentNullException.ThrowIfNull(session);
        AppBuilder.Configure(() => new CalypsoCadApp(session))
            .UsePlatformDetect()
            .With(new Win32PlatformOptions
            {
                RenderingMode = [Win32RenderingMode.Software],
            })
            .LogToTrace()
            .StartWithClassicDesktopLifetime([]);
    }
}
