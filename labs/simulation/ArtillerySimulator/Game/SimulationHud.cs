using System.Globalization;
using Novolis.Physics.Ballistics;

namespace ArtillerySimulator.Game;

internal static class SimulationHud
{
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
        var terrainLabel = terrain.IsFlat
            ? "flat"
            : terrain.RealGround
                ? "Kristiansand"
                : "broken";
        var downrange = Horizontal(terrain.GunBaseline, shot.CurrentPosition);
        var heroMeters = shot.Phase switch
        {
            BallisticTrajectoryPhase.InFlight => downrange,
            BallisticTrajectoryPhase.Impacted when shot.Impact is { } impact => impact.HorizontalRangeMeters,
            _ => predictedRangeMeters,
        };
        var stop = gun.AtTraverseStop ? " TRAVERSE-STOP" : "";
        var orderedElev = orderedElevation * 6400f / 360f;
        var orderedDefl = (orderedAzimuth - gun.CenterAzimuthDegrees) * 6400f / 360f;
        var defense = string.IsNullOrEmpty(defenseLine) ? "" : defenseLine + " | ";
        var reload = reloadLine ?? "ready";
        var sampleAlt = shot.Phase == BallisticTrajectoryPhase.InFlight ? shot.CurrentPosition.Y : terrain.GunBaseline.Y;
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{defense}{phase}  {SimulationUnits.FormatRange(heroMeters)}  QE {gun.ElevationMils:F0}→{orderedElev:F0} mil  Defl {gun.DeflectionMils:F0}→{orderedDefl:F0}{stop}  {gun.ChargeLabel}  {(gun.DragEnabled ? "drag" : "vac")}  {reload}  {terrainLabel}  {paceLabel}  {fps:F0}fps  {(camera.Mode == CameraMode.Freecam ? "free" : "follow")}  {atmosphere.SummaryLine(sampleAlt)}");
    }

    private static float Horizontal(System.Numerics.Vector3 origin, System.Numerics.Vector3 point)
    {
        var dx = point.X - origin.X;
        var dz = point.Z - origin.Z;
        return MathF.Sqrt(dx * dx + dz * dz);
    }
}
