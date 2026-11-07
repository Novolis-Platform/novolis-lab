using CalypsoCad.Rendering;
using Novolis.Silk;

namespace CalypsoCad.Host;

/// <summary>Interactive Silk GLFW host for Calypso CAD (CI/agent default).</summary>
internal static class CalypsoSilkHost
{
    public static void Run(CalypsoSession session, CalypsoRenderer renderer)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(renderer);

        var controller = new CalypsoViewportController(session, renderer);
        SilkGame.Run("Calypso CAD", 1600, 900, initialize: null, update: frame =>
        {
            controller.HandleSilkInput(frame);
            renderer.DrawFrame(frame, frame.DeltaSeconds);
        });
    }
}
