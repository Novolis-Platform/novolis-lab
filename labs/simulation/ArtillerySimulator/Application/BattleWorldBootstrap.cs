using Microsoft.Extensions.Logging;
using Novolis.Physics.Ballistics;

namespace ArtillerySimulator;

internal sealed class BattleWorldBootstrap
{
    private readonly TerrainWorld _terrain;
    private readonly BattleSession _session;
    private readonly CrewDrill _crew;
    private readonly ArtilleryRoundPool _rounds;
    private readonly ArtilleryBattleLoop _battle;
    private readonly AimPreviewService _aim;
    private readonly ArtilleryCameras _camera;
    private readonly ArtilleryShellController _shell;
    private readonly PlayerBattleInput _input;
    private readonly ILogger _log;

    public BattleWorldBootstrap(
        TerrainWorld terrain,
        BattleSession session,
        CrewDrill crew,
        ArtilleryRoundPool rounds,
        ArtilleryBattleLoop battle,
        AimPreviewService aim,
        ArtilleryCameras camera,
        ArtilleryShellController shell,
        PlayerBattleInput input,
        ILogger logger)
    {
        _terrain = terrain;
        _session = session;
        _crew = crew;
        _rounds = rounds;
        _battle = battle;
        _aim = aim;
        _camera = camera;
        _shell = shell;
        _input = input;
        _log = logger;
    }

    public IDefenseMission LoadWorld(ref GunModel gun, ref bool worldReady, ref IDefenseMission? mission)
    {
        if (worldReady)
            return mission!;

        var level = _shell.Level;
        _shell.SetLoadingStatus($"Building {level.DisplayName}…");
        level.RebuildMap(_terrain);
        _shell.SetLoadingStatus($"Seating {gun.Spec.ShortName} battery…");
        _session.Clock.Reset();
        mission = level.StartMission(_terrain, _session.Piece, _session.Seed);
        _battle.SetMission(mission);
        _terrain.PlaceGun(mission.GunX, mission.GunZ, _session.Piece.GunHeightOffsetMeters);
        gun.LayCenter(mission.TrailBearingDegrees);
        gun.SetCharge(mission.OpeningChargeIndex);
        gun.SetElevation(mission.OpeningElevationDegrees);
        gun.NudgeAzimuth(mission.OpeningAzimuthDegrees - mission.TrailBearingDegrees);
        _crew.SeatOpening(gun);
        _rounds.ResetAll(_battle.ImpactHandled);
        _battle.ResetVisuals();
        _input.Reset();
        _camera.SnapToGun(_terrain.GunBaseline, gun.BarrelDirection());
        _aim.Rebuild(_crew);
        _shell.Menus.ClearToPlaying();
        worldReady = true;

        _log.LogInformation(
            "World loaded: approaches={Approaches} remaining={Remaining}",
            mission.SpawnPointCount,
            mission.Remaining);

        return mission;
    }
}
