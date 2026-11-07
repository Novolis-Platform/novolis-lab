using CalypsoCad.Rendering;
using Novolis.Silk;

namespace CalypsoCad.Host;

/// <summary>Interactive Silk GLFW host for Calypso CAD (replaces Avalonia+Raylib).</summary>
internal static class CalypsoSilkHost
{
    public static void Run(CalypsoSession session, CalypsoRenderer renderer)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(renderer);

        var layerIndex = 0;
        SilkGame.Run("Calypso CAD", 1600, 900, initialize: null, update: frame =>
        {
            HandleInput(frame, session, renderer, ref layerIndex);
            renderer.DrawFrame(frame, frame.DeltaSeconds);
        });
    }

    private static void HandleInput(
        SilkFrame frame, CalypsoSession session, CalypsoRenderer renderer, ref int layerIndex)
    {
        if (frame.IsKeyPressed(Key.Escape))
            frame.Close();

        if (frame.IsKeyPressed(Key.P))
            session.ViewMode = CalypsoViewMode.Plan;
        if (frame.IsKeyPressed(Key.O))
            session.ViewMode = CalypsoViewMode.Orbit;
        if (frame.IsKeyPressed(Key.I))
        {
            session.ViewMode = CalypsoViewMode.Interior;
            renderer.SyncInteriorFromSelection();
        }

        if (frame.IsKeyPressed(Key.W))
            session.WireMeshMode = CalypsoWireMeshMode.Wire;
        if (frame.IsKeyPressed(Key.C))
            session.WireMeshMode = CalypsoWireMeshMode.CutawayPartial;
        if (frame.IsKeyPressed(Key.S))
            session.WireMeshMode = CalypsoWireMeshMode.None;

        if (frame.IsKeyPressed(Key.G))
        {
            layerIndex++;
            session.VisualLayers = CalypsoVisualLayerPresets.CycleOrbit(layerIndex, out var label);
            session.StatusText = $"layers:{label}";
        }

        if (frame.IsKeyPressed(Key.Number1))
            session.DeckFilter = -1;
        if (frame.IsKeyPressed(Key.Number2))
            session.DeckFilter = 0;
        if (frame.IsKeyPressed(Key.Number3))
            session.DeckFilter = 1;
        if (frame.IsKeyPressed(Key.Number4))
            session.DeckFilter = null;

        if (frame.IsKeyPressed(Key.L))
            session.CutPlaneLongitudinal = true;
        if (frame.IsKeyPressed(Key.B))
            session.CutPlaneLongitudinal = false;

        if (frame.IsKeyPressed(Key.KeypadSubtract) || frame.IsKeyPressed(Key.Minus))
            session.CutPlaneOffset -= 1f;
        if (frame.IsKeyPressed(Key.KeypadAdd) || frame.IsKeyPressed(Key.Equal))
            session.CutPlaneOffset += 1f;

        if (session.ViewMode == CalypsoViewMode.Orbit)
        {
            if (frame.IsMouseButtonDown(MouseButton.Left) || frame.IsMouseButtonDown(MouseButton.Right))
            {
                renderer.Orbit.AddLookDelta(
                    -frame.MouseDeltaX * 0.005f,
                    frame.MouseDeltaY * 0.004f);
            }

            if (MathF.Abs(frame.ScrollDelta) > 0.01f)
                renderer.Zoom(frame.ScrollDelta * -4f);
        }
    }
}
