using System.Globalization;
using System.Text;
using Novolis.Physics.Ballistics;
using Novolis.Silk;

namespace ArtillerySimulator;

/// <summary>F1 overlay + Ctrl+C clipboard dump text builders.</summary>
internal static class ArtilleryDebugDump
{
    public static string BuildProfilePanel(
        SilkFrame frame,
        FrameProfiler profiler,
        ArtillerySceneRenderer renderer,
        TerrainWorld terrain,
        AtmosphereModel atmosphere,
        GunModel gun,
        ArtilleryRoundPool rounds,
        float predictedRangeMeters,
        ArtilleryCameras camera,
        IDefenseMission? mission,
        string? flash,
        float flashAge)
    {
        var sb = new StringBuilder(512);
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"FPS  {renderer.Fps:F0}  (inst {profiler.InstantFps():F0}  avg {profiler.AverageFps():F0})");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"FRAME  {profiler.LastFrameMs:F2} ms   samples {profiler.Frames}");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"GEO  fillTris {terrain.TerrainTriangleCount}  roads {terrain.RoadLineCount}  dyn {renderer.Wires.AsSpan().Length / 2}");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"VIEW  {frame.Width}x{frame.Height}  pace realtime  follow={(camera.FollowProjectile ? "shell" : "gun")}");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"GUN  QE {gun.ElevationMils:F0} mil  DEFL {gun.DeflectionMils:F0}  {gun.ChargeLabel} {gun.MuzzleSpeedMps:F0} m/s");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"SHOT  {rounds.Active.Phase}  t={rounds.Active.TimeSeconds:F2}s  range={predictedRangeMeters:F0}m");
        if (mission is { } m)
        {
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"OPFOR  round {m.Round}  alive {m.Remaining}  stopped {m.Stopped}  nearest {m.NearestMeters / 1000f:F2} km  spawns {m.SpawnPointCount}");
        }

        sb.AppendLine(CultureInfo.InvariantCulture,
            $"MET  {atmosphere.WindSpeedMetersPerSecond:F1} m/s from {atmosphere.WindFromDegrees:F0}°  ρ {atmosphere.DensityKgPerM3At(terrain.GunBaseline.Y):F3}  QNH {atmosphere.ReferencePressurePa / 100.0:F0}");
        sb.AppendLine(profiler.ToReport().TrimEnd());
        sb.Append("F1 toggle   Ctrl+C copy dump");
        if (flashAge >= 0f && flashAge < 2.5f && !string.IsNullOrEmpty(flash))
            sb.Append(CultureInfo.InvariantCulture, $"   [{flash}]");
        return sb.ToString();
    }

    public static string BuildClipboardDump(
        SilkFrame frame,
        FrameProfiler profiler,
        ArtillerySceneRenderer renderer,
        TerrainWorld terrain,
        AtmosphereModel atmosphere,
        GunModel gun,
        CrewDrill crew,
        ArtilleryRoundPool rounds,
        ArtilleryCameras camera,
        IDefenseMission? mission)
    {
        var sb = new StringBuilder(4096);
        sb.AppendLine("=== ArtillerySimulator debug dump ===");
        sb.AppendLine(CultureInfo.InvariantCulture, $"utc={DateTime.UtcNow:O}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"window={frame.Width}x{frame.Height}  fps={renderer.Fps:F1}");
        sb.AppendLine();
        sb.AppendLine(profiler.ToReport());
        sb.AppendLine("--- gun ---");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"elevation={gun.ElevationDegrees:F2} deg ({gun.ElevationMils:F1} mil)  ordered={crew.OrderedElevation:F2}");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"azimuth={gun.AzimuthDegrees:F2}  ordered={crew.OrderedAzimuth:F2}  trail={gun.CenterAzimuthDegrees:F2}");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"charge={gun.ChargeLabel}  muzzle={gun.MuzzleSpeedMps:F0} m/s  breech={(crew.BreechReady ? "ready" : crew.Phrase(gun))}");
        sb.AppendLine("--- shot ---");
        var shot = rounds.Active;
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"phase={shot.Phase}  time={shot.TimeSeconds:F3}s  pos={shot.CurrentPosition}  vel={shot.CurrentVelocity}");
        if (shot.Impact is { } hit)
        {
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"impact range={hit.HorizontalRangeMeters:F1}m  tof={hit.TimeSeconds:F2}s  speed={hit.ImpactSpeedMps:F0}  reason={hit.Reason}");
        }

        sb.AppendLine("--- atmosphere ---");
        sb.AppendLine(atmosphere.SummaryLine(terrain.GunBaseline.Y));
        sb.AppendLine(CultureInfo.InvariantCulture, $"wind={atmosphere.SurfaceWindMetersPerSecond}");
        sb.AppendLine("--- mission ---");
        if (mission is { } m)
        {
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"round={m.Round}  remaining={m.Remaining}  stopped={m.Stopped}  nearest={m.NearestMeters:F0}m  spawns={m.SpawnPointCount}");
            sb.AppendLine(m.HudLine());
            sb.AppendLine(m.Observation());
        }
        else
        {
            sb.AppendLine("(no mission)");
        }

        sb.AppendLine("--- camera ---");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"follow={(camera.FollowProjectile ? "shell" : "gun")}  pace=realtime");
        sb.AppendLine("--- terrain ---");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"east={terrain.EastMeters:F0}  north={terrain.NorthMeters:F0}  gun={terrain.GunBaseline}");
        return sb.ToString();
    }
}
