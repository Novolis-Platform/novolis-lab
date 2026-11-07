using CalypsoCad.Rendering;
using Novolis.Silk;

namespace CalypsoCad.Host;

/// <summary>Shared view/layer/cut controls for Silk window and Avalonia explorer.</summary>
internal sealed class CalypsoViewportController
{
    private int _layerIndex;

    public CalypsoViewportController(CalypsoSession session, CalypsoRenderer renderer)
    {
        Session = session ?? throw new ArgumentNullException(nameof(session));
        Renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
    }

    public CalypsoSession Session { get; }
    public CalypsoRenderer Renderer { get; }

    public void HandleSilkInput(SilkFrame frame)
    {
        if (frame.IsKeyPressed(Key.Escape))
            frame.Close();

        if (frame.IsKeyPressed(Key.P))
            SetView(CalypsoViewMode.Plan);
        if (frame.IsKeyPressed(Key.O))
            SetView(CalypsoViewMode.Orbit);
        if (frame.IsKeyPressed(Key.I))
            SetInterior();

        if (frame.IsKeyPressed(Key.W))
            Session.WireMeshMode = CalypsoWireMeshMode.Wire;
        if (frame.IsKeyPressed(Key.C))
            Session.WireMeshMode = CalypsoWireMeshMode.CutawayPartial;
        if (frame.IsKeyPressed(Key.S))
            Session.WireMeshMode = CalypsoWireMeshMode.None;

        if (frame.IsKeyPressed(Key.G))
            CycleLayerPreset();

        if (frame.IsKeyPressed(Key.H))
            ToggleOmlGuide();

        if (frame.IsKeyPressed(Key.Number1))
            Session.DeckFilter = -1;
        if (frame.IsKeyPressed(Key.Number2))
            Session.DeckFilter = 0;
        if (frame.IsKeyPressed(Key.Number3))
            Session.DeckFilter = 1;
        if (frame.IsKeyPressed(Key.Number4))
            Session.DeckFilter = null;

        if (frame.IsKeyPressed(Key.L))
            Session.CutPlaneLongitudinal = true;
        if (frame.IsKeyPressed(Key.B))
            Session.CutPlaneLongitudinal = false;

        if (frame.IsKeyPressed(Key.KeypadSubtract) || frame.IsKeyPressed(Key.Minus))
            Session.CutPlaneOffset -= 1f;
        if (frame.IsKeyPressed(Key.KeypadAdd) || frame.IsKeyPressed(Key.Equal))
            Session.CutPlaneOffset += 1f;

        if (Session.ViewMode == CalypsoViewMode.Orbit)
        {
            if (frame.IsMouseButtonDown(MouseButton.Left) || frame.IsMouseButtonDown(MouseButton.Right))
            {
                Renderer.Orbit.AddLookDelta(
                    -frame.MouseDeltaX * 0.005f,
                    frame.MouseDeltaY * 0.004f);
            }

            if (MathF.Abs(frame.ScrollDelta) > 0.01f)
                Renderer.Zoom(frame.ScrollDelta * -4f);
        }
    }

    public void SetView(CalypsoViewMode mode) => Session.ViewMode = mode;

    public void SetInterior()
    {
        Session.ViewMode = CalypsoViewMode.Interior;
        Renderer.SyncInteriorFromSelection();
    }

    public void SetWire(CalypsoWireMeshMode mode) => Session.WireMeshMode = mode;

    public void CycleLayerPreset()
    {
        _layerIndex++;
        Session.VisualLayers = CalypsoVisualLayerPresets.CycleOrbit(_layerIndex, out var label);
        if (!Session.VisualLayers.HasFlag(CalypsoVisualLayers.HullShell))
            Session.ShowOmlGuide = true;
        Session.StatusText = $"layers:{label}";
    }

    public void SetLayerPreset(int index)
    {
        _layerIndex = index;
        Session.VisualLayers = CalypsoVisualLayerPresets.CycleOrbit(_layerIndex, out var label);
        if (!Session.VisualLayers.HasFlag(CalypsoVisualLayers.HullShell))
            Session.ShowOmlGuide = true;
        Session.StatusText = $"layers:{label}";
    }

    public void ToggleOmlGuide()
    {
        Session.ShowOmlGuide = !Session.ShowOmlGuide;
        Session.StatusText = Session.ShowOmlGuide ? "oml-guide:on" : "oml-guide:off";
    }

    public void SetDeckFilter(int? deck) => Session.DeckFilter = deck;

    public void SetCutLongitudinal(bool longitudinal) => Session.CutPlaneLongitudinal = longitudinal;

    public void NudgeCut(float delta) => Session.CutPlaneOffset += delta;

    /// <summary>Avalonia pointer drag → orbit (hidden Silk window never sees mouse).</summary>
    public void OrbitPointerDelta(float dxPx, float dyPx)
    {
        if (Session.ViewMode != CalypsoViewMode.Orbit)
            return;
        // Match Silk host: yaw from −dx, pitch from +dy.
        Renderer.Orbit.AddLookDelta(-dxPx * 0.005f, dyPx * 0.004f);
    }

    /// <summary>Avalonia wheel → zoom (Orbit / Interior).</summary>
    public void PointerWheel(float scrollDelta)
    {
        if (Session.ViewMode is not (CalypsoViewMode.Orbit or CalypsoViewMode.Interior))
            return;
        if (MathF.Abs(scrollDelta) < 0.01f)
            return;
        Renderer.Zoom(scrollDelta * -4f);
    }

    public void Regenerate()
    {
        Session.RegenerateAndLoad();
        Renderer.Fit();
        Renderer.SyncInteriorFromSelection();
        Session.StatusText = "regenerated";
    }
}
