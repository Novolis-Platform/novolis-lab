using Avalonia;
using Avalonia.Win32;
using CalypsoCad.Host;
using CalypsoCad.Rendering;

namespace CalypsoCad.Shell;

/// <summary>
/// Avalonia explorer on the process main STA (Skia/Software) with an embedded
/// Silk viewport stream. Do not enable Win32 WGL — it steals OpenGL from Silk.
/// </summary>
internal static class CalypsoExplorerHost
{
    public static void Run(CalypsoSession session, CalypsoRenderer renderer)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(renderer);

        var controller = new CalypsoViewportController(session, renderer);
        AppBuilder.Configure(() => new CalypsoExplorerApp(session, renderer, controller))
            .UsePlatformDetect()
            .With(new Win32PlatformOptions
            {
                RenderingMode = [Win32RenderingMode.Software],
            })
            .LogToTrace()
            .StartWithClassicDesktopLifetime([]);
    }
}
