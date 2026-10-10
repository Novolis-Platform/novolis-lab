using System.Drawing;
using Novolis.Physics.Ballistics;
using Novolis.Raylib.Game;

namespace ArtillerySimulator.Game;

internal static class SimulationHud
{
    public static void Draw(
        RayGameContext ctx,
        GunModel gun,
        TerrainWorld terrain,
        AtmosphereModel atmosphere,
        BallisticTrajectoryRunner shot,
        ArtilleryCameras camera,
        float fps)
    {
        // Fixed 500px panel dominated small guest framebuffers; keep a corner card instead.
        var scale = Math.Clamp(Math.Min(ctx.Width / 1920f, ctx.Height / 1080f), 0.55f, 1.15f);
        var pad = Math.Max(8, (int)(10 * scale));
        var x = pad;
        var y = pad;
        var w = Math.Clamp((int)(ctx.Width * 0.28f), 260, 420);
        var titleSize = Math.Max(11, (int)(13 * scale));
        var bodySize = Math.Max(10, (int)(12 * scale));
        var dimSize = Math.Max(9, (int)(10 * scale));
        var lineH = bodySize + 4;

        var text = Color.FromArgb(255, 200, 215, 195);
        var accent = Color.FromArgb(255, 140, 200, 170);
        var dim = Color.FromArgb(255, 120, 130, 125);

        var mils = gun.ElevationDegrees * 6400f / 360f;
        var sampleAlt = shot.Phase == BallisticTrajectoryPhase.InFlight ? shot.CurrentPosition.Y : terrain.GunBaseline.Y;
        var terrainLabel = terrain.IsFlat
            ? "flat"
            : terrain.Style switch
            {
                TerrainStyle.AfghanHighland => "afghan",
                TerrainStyle.NordicRidges => "nordic",
                _ => "rugged",
            };

        var rows = new List<(string Line, int Size, Color Color)>
        {
            ("ARTILLERY SIMULATOR", titleSize, accent),
            ($"FPS {fps:F0}  cam {(camera.Mode == CameraMode.Freecam ? "free" : "orbit")}  {shot.Phase}", bodySize, text),
            ($"Elev {gun.ElevationDegrees:F0} deg / {mils:F0} mils   Az {gun.AzimuthDegrees:F0}", bodySize, text),
            ($"{gun.ChargeLabel}  Mv {gun.MuzzleSpeedMps:F0}  {(gun.DragEnabled ? "aero" : "vacuum")}", bodySize, text),
            (atmosphere.SummaryLine(sampleAlt), dimSize, text),
            ($"{terrainLabel}  {SimulationUnits.FormatRange(terrain.ExtentMeters)}", bodySize, text),
        };

        if (shot.Phase == BallisticTrajectoryPhase.InFlight)
        {
            rows.Add((
                $"Alt {shot.CurrentPosition.Y:F0}m  Spd {shot.CurrentVelocity.Length():F0}  T {shot.TimeSeconds:F1}s",
                bodySize,
                text));
        }

        if (shot.Impact is { } impact)
        {
            var reason = impact.Reason == ProjectileTerrainImpactReason.BeyondRange ? "  EDGE" : "";
            rows.Add((
                $"Impact {SimulationUnits.FormatRange(impact.HorizontalRangeMeters)}  TOF {impact.TimeSeconds:F1}s{reason}",
                bodySize,
                text));
        }

        rows.Add(("WASD look  Shift/Ctrl elev  Q/E az  C/F/T/R", dimSize, dim));

        var h = pad + rows.Count * lineH + pad / 2;
        ctx.HudRect(x - 2, y - 2, w + 4, h + 4, Color.FromArgb(180, 6, 8, 10));
        ctx.HudRect(x, y, w, h, Color.FromArgb(170, 14, 18, 22));

        var row = y + pad / 2;
        foreach (var (line, size, color) in rows)
        {
            ctx.HudText(line, x + 8, row, size, color);
            row += lineH;
        }
    }
}
