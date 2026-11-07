using System.Drawing;
using System.Globalization;
using System.Numerics;
using Novolis.Math.Geometry;
using Novolis.Physics.Ballistics;
using Novolis.Silk;

namespace ArtillerySimulator;

/// <summary>On-screen artillery HUD (ortho wire panels + stick font) plus window title line.</summary>
internal static class SimulationHud
{
    private static readonly Color Panel = Color.FromArgb(160, 24, 32, 40);
    private static readonly Color Border = Color.FromArgb(220, 120, 160, 190);
    private static readonly Color Text = Color.FromArgb(255, 220, 230, 235);
    private static readonly Color Accent = Color.FromArgb(255, 255, 186, 92);
    private static readonly Color Ready = Color.FromArgb(255, 120, 210, 140);
    private static readonly Color Alert = Color.FromArgb(255, 255, 120, 72);

    private static readonly SilkWireBatch Overlay = new();

    /// <summary>Line segments submitted by the last <see cref="Draw"/> call (for profiling).</summary>
    public static int LastOverlayLineCount { get; private set; }

    public static void Draw(
        SilkFrame frame,
        GunModel gun,
        float orderedElevation,
        float orderedAzimuth,
        TerrainWorld terrain,
        AtmosphereModel atmosphere,
        BallisticTrajectoryRunner shot,
        ArtilleryCameras camera,
        float fps,
        float predictedRangeMeters,
        string paceLabel,
        string? defenseLine,
        string? reloadLine)
    {
        var title = TitleLine(
            gun,
            orderedElevation,
            orderedAzimuth,
            terrain,
            atmosphere,
            shot,
            camera,
            fps,
            predictedRangeMeters,
            paceLabel,
            defenseLine,
            reloadLine);
        frame.SetTitle(title);

        Overlay.Clear();
        var w = frame.Width;
        var h = frame.Height;
        var scale = Math.Clamp(Math.Min(w, h) / 900f, 0.85f, 1.35f);

        // Top strip
        var topH = 52f * scale;
        HudStickFont.FillHatch(Overlay, 8, 8, w - 16, topH, Panel, 6f * scale);
        HudStickFont.Rect(Overlay, 8, 8, w - 16, topH, Border);

        var phase = shot.Phase switch
        {
            BallisticTrajectoryPhase.InFlight => "IN FLIGHT",
            BallisticTrajectoryPhase.Impacted => "IMPACT",
            _ => "READY",
        };
        var phaseColor = shot.Phase == BallisticTrajectoryPhase.InFlight ? Accent
            : shot.Phase == BallisticTrajectoryPhase.Impacted ? Alert
            : Ready;

        var y1 = 16f * scale;
        HudStickFont.Draw(Overlay, phase, 18 * scale, y1, 2.2f * scale, phaseColor);
        HudStickFont.Draw(
            Overlay,
            $"{SimulationUnits.FormatRange(HeroRange(terrain, shot, predictedRangeMeters))}   {gun.ChargeLabel}",
            160 * scale,
            y1,
            2.0f * scale,
            Text);

        var y2 = 34f * scale;
        var mission = string.IsNullOrEmpty(defenseLine) ? "DEFENSE OF KRISTIANSAND" : defenseLine;
        HudStickFont.Draw(Overlay, Truncate(mission, 72), 18 * scale, y2, 1.55f * scale, Accent);

        // Left gun panel
        var panelW = 280f * scale;
        var panelH = 148f * scale;
        var px = 8f;
        var py = 72f * scale;
        HudStickFont.FillHatch(Overlay, px, py, panelW, panelH, Panel, 7f * scale);
        HudStickFont.Rect(Overlay, px, py, panelW, panelH, Border);

        var orderedElev = orderedElevation * 6400f / 360f;
        var orderedDefl = (orderedAzimuth - gun.CenterAzimuthDegrees) * 6400f / 360f;
        var lx = px + 12 * scale;
        var ly = py + 12 * scale;
        var ls = 1.7f * scale;
        var lh = 16f * scale;
        HudStickFont.Draw(Overlay, "GUN", lx, ly, 2.0f * scale, Accent);
        ly += lh + 4 * scale;
        HudStickFont.Draw(Overlay, $"QE   {gun.ElevationMils:F0} -> {orderedElev:F0} MIL", lx, ly, ls, Text);
        ly += lh;
        HudStickFont.Draw(Overlay, $"DEFL {gun.DeflectionMils:F0} -> {orderedDefl:F0}{ (gun.AtTraverseStop ? " STOP" : "")}", lx, ly, ls, Text);
        ly += lh;
        HudStickFont.Draw(Overlay, reloadLine is null ? "CREW READY" : Truncate(reloadLine.ToUpperInvariant(), 28), lx, ly, ls, reloadLine is null ? Ready : Alert);
        ly += lh;
        HudStickFont.Draw(Overlay, atmosphere.SummaryLine(shot.Phase == BallisticTrajectoryPhase.InFlight ? shot.CurrentPosition.Y : terrain.GunBaseline.Y).ToUpperInvariant(), lx, ly, 1.35f * scale, Text);

        // Right status panel
        var rw = 260f * scale;
        var rh = 120f * scale;
        var rx = w - rw - 8;
        var ry = 72f * scale;
        HudStickFont.FillHatch(Overlay, rx, ry, rw, rh, Panel, 7f * scale);
        HudStickFont.Rect(Overlay, rx, ry, rw, rh, Border);
        var rlx = rx + 12 * scale;
        var rly = ry + 12 * scale;
        HudStickFont.Draw(Overlay, "VIEW", rlx, rly, 2.0f * scale, Accent);
        rly += lh + 4 * scale;
        HudStickFont.Draw(Overlay, $"{(camera.Mode == CameraMode.Freecam ? "FREE" : "FOLLOW")}  {fps:F0} FPS", rlx, rly, ls, Text);
        rly += lh;
        HudStickFont.Draw(Overlay, $"PACE {paceLabel}  KRISTIANSAND", rlx, rly, ls, Text);
        rly += lh;
        HudStickFont.Draw(Overlay, "V MAP  G GUN  C CAM", rlx, rly, 1.35f * scale, Text);

        // Bottom help
        var help = "W/S ELEV  Q/E TRAV  1-4 CHARGE  SPACE FIRE  ,/. PACE  SHIFT FINE";
        var helpScale = 1.45f * scale;
        var helpW = HudStickFont.MeasureWidth(help, helpScale);
        var hx = Math.Max(12f, (w - helpW) * 0.5f);
        var hy = h - 28f * scale;
        HudStickFont.Draw(Overlay, help, hx, hy, helpScale, Border);

        // Pixel ortho: origin top-left, Y down. Near depth so overlay wins the depth buffer.
        var ortho = Matrix4x4.CreateOrthographicOffCenter(0f, w, h, 0f, -1f, 1f);
        LastOverlayLineCount = Overlay.AsSpan().Length / 2;
        frame.DrawWorldLines(Overlay.AsSpan(), ortho);
    }

    public static string TitleLine(
        GunModel gun,
        float orderedElevation,
        float orderedAzimuth,
        TerrainWorld terrain,
        AtmosphereModel atmosphere,
        BallisticTrajectoryRunner shot,
        ArtilleryCameras camera,
        float fps,
        float predictedRangeMeters,
        string paceLabel,
        string? defenseLine = null,
        string? reloadLine = null)
    {
        var phase = shot.Phase switch
        {
            BallisticTrajectoryPhase.InFlight => "IN FLIGHT",
            BallisticTrajectoryPhase.Impacted => "IMPACT",
            _ => "READY",
        };
        var heroMeters = HeroRange(terrain, shot, predictedRangeMeters);
        var stop = gun.AtTraverseStop ? " TRAVERSE-STOP" : "";
        var orderedElev = orderedElevation * 6400f / 360f;
        var orderedDefl = (orderedAzimuth - gun.CenterAzimuthDegrees) * 6400f / 360f;
        var defense = string.IsNullOrEmpty(defenseLine) ? "" : defenseLine + " | ";
        var reload = reloadLine ?? "ready";
        var sampleAlt = shot.Phase == BallisticTrajectoryPhase.InFlight ? shot.CurrentPosition.Y : terrain.GunBaseline.Y;
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{defense}{phase}  {SimulationUnits.FormatRange(heroMeters)}  QE {gun.ElevationMils:F0}→{orderedElev:F0} mil  Defl {gun.DeflectionMils:F0}→{orderedDefl:F0}{stop}  {gun.ChargeLabel}  {reload}  Kristiansand  {paceLabel}  {fps:F0}fps  {(camera.Mode == CameraMode.Freecam ? "free" : "follow")}  {atmosphere.SummaryLine(sampleAlt)}");
    }

    private static float HeroRange(TerrainWorld terrain, BallisticTrajectoryRunner shot, float predictedRangeMeters)
    {
        return shot.Phase switch
        {
            BallisticTrajectoryPhase.InFlight => Horizontal(terrain.GunBaseline, shot.CurrentPosition),
            BallisticTrajectoryPhase.Impacted when shot.Impact is { } impact => impact.HorizontalRangeMeters,
            _ => predictedRangeMeters,
        };
    }

    private static float Horizontal(Vector3 origin, Vector3 point)
    {
        var dx = point.X - origin.X;
        var dz = point.Z - origin.Z;
        return MathF.Sqrt(dx * dx + dz * dz);
    }

    private static string Truncate(string text, int max)
        => text.Length <= max ? text : text[..Math.Max(0, max - 3)] + "...";
}
