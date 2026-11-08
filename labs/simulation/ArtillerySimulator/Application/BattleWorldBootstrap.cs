using Microsoft.Extensions.Logging;

namespace ArtillerySimulator;

internal sealed class BattleWorldBootstrap
{
    private readonly TerrainWorld _terrain;
    private readonly AtmosphereModel _atmosphere;
    private readonly BattleSession _session;
    private readonly ArtilleryRoundPool _rounds;
    private readonly ArtilleryBattleLoop _battle;
    private readonly ArtilleryShellController _shell;
    private readonly PlayerBattleInput _input;
    private readonly ScoreBoard _score;
    private readonly ILogger _log;

    public BattleWorldBootstrap(
        TerrainWorld terrain,
        AtmosphereModel atmosphere,
        BattleSession session,
        ArtilleryRoundPool rounds,
        ArtilleryBattleLoop battle,
        ArtilleryShellController shell,
        PlayerBattleInput input,
        ScoreBoard score,
        ILogger logger)
    {
        _terrain = terrain;
        _atmosphere = atmosphere;
        _session = session;
        _rounds = rounds;
        _battle = battle;
        _shell = shell;
        _input = input;
        _score = score;
        _log = logger;
    }

    /// <summary>
    /// Rebuilds map + boots mission. Caller seats the active <see cref="BatteryRoster"/> mount afterward.
    /// </summary>
    public IDefenseMission LoadWorld(ref bool worldReady, ref IDefenseMission? mission)
    {
        if (worldReady)
            return mission!;

        var level = _shell.Level;
        _shell.SetLoadingStatus($"Building {level.DisplayName}…");
        level.RebuildMap(_terrain);
        _atmosphere.BindMap(_terrain.Profile);
        _shell.SetLoadingStatus($"Seating {level.SiteName} battery…");
        _session.Clock.Reset();
        mission = level.StartMission(_terrain, _session.Piece, _session.Seed);
        _score.BeginMission(level.Id, _session.SeedHash);
        _battle.SetMission(mission);
        _rounds.ResetAll(_battle.ImpactHandled);
        _battle.ResetVisuals();
        _input.Reset();
        worldReady = true;

        _log.LogInformation(
            "World loaded: approaches={Approaches} remaining={Remaining}",
            mission.SpawnPointCount,
            mission.Remaining);

        return mission;
    }
}
