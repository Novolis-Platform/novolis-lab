using CalypsoCad.Session;
using CalypsoCad.Structure;
using CalypsoCad.Viewport;

namespace CalypsoCad.Export;

/// <summary>Shared session + Silk renderer bootstrap for headless exporters.</summary>
internal static class CalypsoHeadlessHost
{
    /// <summary>Regenerate lock companions, load session, fit camera, run <paramref name="body"/>.</summary>
    public static int WithViewport(Func<CalypsoSession, CalypsoRenderer, int> body)
    {
        ArgumentNullException.ThrowIfNull(body);
        var session = new CalypsoSession();
        session.RegenerateAndLoad();
        Console.WriteLine($"Wrote CAD → {session.GeneratedDirectory}");
        var renderer = new CalypsoRenderer(session);
        renderer.Fit();
        renderer.SyncInteriorFromSelection();
        return body(session, renderer);
    }

    /// <summary>Ensure companions exist without opening a viewport.</summary>
    public static string EnsureGenerated()
    {
        var dir = CalypsoLockGenerator.Generate();
        Console.WriteLine($"Wrote CAD → {dir}");
        return dir;
    }
}
