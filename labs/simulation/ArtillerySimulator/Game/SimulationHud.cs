using System.Drawing;
using Novolis.Physics.Ballistics;
using Novolis.Raylib.Game;

namespace ArtillerySimulator.Game;

internal static class SimulationHud
{
    public static void Draw(
        RayGameContext ctx,
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
        var scale = Math.Clamp(Math.Min(ctx.Width / 1920f, ctx.Height / 1080f), 0.55f, 1.15f);
        var pad = Math.Max(10, (int)(14 * scale));
        var body = Math.Max(13, (int)(16 * scale));
        var titleSize = Math.Max(12, (int)(13 * scale));
        var hero = Math.Max(28, (int)(36 * scale));
        var dimSize = Math.Max(11, (int)(12 * scale));
        var line = body + 6;

        var paper = Color.FromArgb(230, 214, 206, 188);
        var dim = Color.FromArgb(200, 150, 146, 132);
        var accent = PhaseColor(shot.Phase);
        var phase = shot.Phase switch
        {
            BallisticTrajectoryPhase.InFlight => "IN FLIGHT",
            BallisticTrajectoryPhase.Impacted => "IMPACT",
            _ => "READY",
        };
        var terrainLabel = terrain.IsFlat
            ? "flat range"
            : terrain.RealGround
                ? "Kristiansand"
                : "broken ground";
        var downrange = Horizontal(terrain.GunBaseline, shot.CurrentPosition);
        var heroMeters = shot.Phase switch
        {
            BallisticTrajectoryPhase.InFlight => downrange,
            BallisticTrajectoryPhase.Impacted when shot.Impact is { } impact => impact.HorizontalRangeMeters,
            _ => predictedRangeMeters,
        };
        var heroCaption = shot.Phase switch
        {
            BallisticTrajectoryPhase.InFlight => "downrange",
            BallisticTrajectoryPhase.Impacted => "impact",
            _ => "this lay",
        };
        var stop = gun.AtTraverseStop ? "    traverse stop" : "";
        var orderedElev = orderedElevation * 6400f / 360f;
        var orderedDefl = (orderedAzimuth - gun.CenterAzimuthDegrees) * 6400f / 360f;
        var gap = MathF.Max(MathF.Abs(orderedElev - gun.ElevationMils), MathF.Abs(orderedDefl - gun.DeflectionMils));
        var lay = gap < 2f
            ? $"QE {gun.ElevationMils:F0} mil ({gun.ElevationDegrees:F0}°)    Defl {gun.DeflectionMils:F0} mil{stop}"
            : $"QE {gun.ElevationMils:F0} → {orderedElev:F0} mil    Defl {gun.DeflectionMils:F0} → {orderedDefl:F0} mil{stop}";
        var sampleAlt = shot.Phase == BallisticTrajectoryPhase.InFlight ? shot.CurrentPosition.Y : terrain.GunBaseline.Y;

        var rows = new List<(string Text, Color Color)>
        {
            (lay, paper),
            ($"{gun.ChargeLabel}    {gun.MuzzleSpeedMps:F0} m/s    {(gun.DragEnabled ? "drag" : "vacuum")}    {(reloadLine ?? "ready")}", paper),
            ($"{terrainLabel}    {paceLabel}    {fps:F0} fps    {(camera.Mode == CameraMode.Freecam ? "free" : "follow")}", dim),
            (atmosphere.SummaryLine(sampleAlt), dim),
        };
        if (!string.IsNullOrEmpty(defenseLine))
            rows.Insert(0, (defenseLine, accent));
        if (shot.Phase == BallisticTrajectoryPhase.InFlight)
        {
            rows.Insert(2, ($"Alt {shot.CurrentPosition.Y:F0} m    {shot.CurrentVelocity.Length():F0} m/s    {shot.TimeSeconds:F1} s", paper));
        }
        else if (shot.Impact is { } hit)
        {
            var edge = hit.Reason == ProjectileTerrainImpactReason.BeyondRange ? "    edge" : "";
            rows.Insert(2, ($"Flight {hit.TimeSeconds:F1} s    {hit.ImpactSpeedMps:F0} m/s{edge}", paper));
        }
        else
        {
            rows.Insert(2, (reloadLine ?? "Upper register. The crew lays on the handwheels. Space fires when the breech is closed", accent));
        }

        var cardW = Math.Clamp((int)(ctx.Width * 0.32f), 320, 480);
        var rowStart = pad + 18 + titleSize * 2 + hero + dimSize;
        var cardH = rowStart + rows.Count * line + 8 - pad;
        ctx.HudRect(pad, pad, cardW, cardH, Color.FromArgb(168, 10, 14, 16));
        ctx.HudRect(pad, pad, 4, cardH, accent);
        ctx.HudText("DEFEND KVADRATUR", pad + 16, pad + 8, titleSize, dim);
        ctx.HudText(phase, pad + 16, pad + 8 + titleSize, titleSize, accent);
        ctx.HudText(SimulationUnits.FormatRange(heroMeters), pad + 16, pad + 12 + titleSize * 2, hero, paper);
        ctx.HudText(heroCaption, pad + 16, pad + 12 + titleSize * 2 + hero, dimSize, dim);

        var row = rowStart;
        foreach (var (text, color) in rows)
        {
            ctx.HudText(text, pad + 16, row, body, color);
            row += line;
        }

        var bar = ControlSize(scale);
        var barY = ctx.Height - bar - 18;
        ctx.HudRect(0, barY, ctx.Width, bar + 18, Color.FromArgb(150, 8, 10, 12));
        ctx.HudText(
            "Drag lay     Shift fine     Q/E traverse     W/S elevate     Wheel zoom     Space when the crew is ready     1-4 charge",
            16,
            barY + 8,
            bar,
            Color.FromArgb(220, 186, 178, 160));
    }

    private static int ControlSize(float scale) => Math.Max(11, (int)(13 * scale));

    private static float Horizontal(System.Numerics.Vector3 origin, System.Numerics.Vector3 point)
    {
        var dx = point.X - origin.X;
        var dz = point.Z - origin.Z;
        return MathF.Sqrt(dx * dx + dz * dz);
    }

    private static Color PhaseColor(BallisticTrajectoryPhase phase) => phase switch
    {
        BallisticTrajectoryPhase.InFlight => Color.FromArgb(255, 230, 164, 72),
        BallisticTrajectoryPhase.Impacted => Color.FromArgb(255, 232, 112, 74),
        _ => Color.FromArgb(255, 150, 196, 150),
    };
}
