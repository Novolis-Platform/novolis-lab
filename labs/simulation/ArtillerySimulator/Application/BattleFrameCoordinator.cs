using Microsoft.Extensions.Logging;
using Novolis.Physics.Ballistics;
using Novolis.Silk;

namespace ArtillerySimulator;

internal sealed class BattleFrameCoordinator
{
    private readonly BattleSession _session;
    private readonly ArtilleryRoundPool _rounds;
    private readonly ArtilleryBattleLoop _battle;
    private readonly ArtilleryCameras _camera;
    private readonly PlayerBattleInput _input;
    private readonly FireControlUi _ui;
    private readonly ArtilleryShellController _shell;
    private readonly TerrainWorld _terrain;
    private readonly Func<GunModel> _gun;
    private readonly Func<bool> _worldReady;
    private readonly Func<bool> _isPaused;
    private readonly FireAuthorizationRules _fireRules;
    private readonly Func<IDefenseMission?> _mission;
    private readonly ScoreBoard _score;
    private readonly ILogger _log;

    public BattleFrameCoordinator(
        BattleSession session,
        ArtilleryRoundPool rounds,
        ArtilleryBattleLoop battle,
        ArtilleryCameras camera,
        PlayerBattleInput input,
        FireControlUi ui,
        ArtilleryShellController shell,
        TerrainWorld terrain,
        Func<GunModel> gun,
        Func<bool> worldReady,
        Func<bool> isPaused,
        FireAuthorizationRules fireRules,
        Func<IDefenseMission?> mission,
        ScoreBoard score,
        ILogger logger)
    {
        _session = session;
        _rounds = rounds;
        _battle = battle;
        _camera = camera;
        _input = input;
        _ui = ui;
        _shell = shell;
        _terrain = terrain;
        _gun = gun;
        _worldReady = worldReady;
        _isPaused = isPaused;
        _fireRules = fireRules;
        _mission = mission;
        _score = score;
        _log = logger;
    }

    public void HandleInput(SilkFrame frame, IBatteryApplier applier)
    {
        if (!_worldReady())
            return;

        var missionTerminal = _fireRules.IsMissionTerminal;
        var gun = _gun();
        if (_isPaused())
        {
            _input.Handle(frame, 0f, paused: true, missionTerminal, applier, _ui, gun);
            return;
        }

        var controlSeconds = MathF.Max(1e-4f, frame.DeltaSeconds);
        if (!missionTerminal)
            _input.Handle(frame, controlSeconds, paused: false, missionTerminal, applier, _ui, gun);
    }

    public int TickPhysics(float wallSeconds)
    {
        if (!_worldReady() || _isPaused())
            return 0;

        var steps = _session.Clock.Consume(wallSeconds);
        if (steps > 0)
            _battle.AdvancePhysics(steps, steps * FixedStepClock.DtSeconds);

        MaybePauseOnMissionEnd();
        return steps;
    }

    public void PresentFrame(SilkFrame frame, float wallSeconds)
    {
        if (!_worldReady())
            return;

        var playing = _shell.Menus.IsPlaying && !_isPaused();
        _battle.AdvancePresentation(wallSeconds, audioPlaying: playing);
        _score.Tick(_mission(), playing, wallSeconds);

        if (_mission() is { } mission)
            _camera.SetBattleFocus(mission.BattleFocus(_terrain.GunBaseline));
        var camRound = _rounds.RoundForCamera(_battle.BurstAge, ArtilleryBattleLoop.KillcamReturnSeconds) ?? _rounds.Active;
        if (_camera.FollowProjectile && camRound.Phase == BallisticTrajectoryPhase.InFlight)
            _rounds.Active = camRound;
        _camera.Update(frame, _terrain.GunBaseline, camRound, allowMouseLook: !_ui.PointerOverUi);
    }

    private void MaybePauseOnMissionEnd()
    {
        if (!_fireRules.IsMissionTerminal || !_shell.Menus.IsPlaying)
            return;

        // Ensure terminal bonuses / high-score write before pause chrome.
        _score.Tick(_mission(), playing: false, wallSeconds: 0f);
        _shell.Menus.PushPause();
        _log.LogInformation("Mission ended: {Outcome}", _fireRules.MissionOutcome);
        Console.WriteLine(_mission()?.Observation() ?? "Mission complete.");
        Console.WriteLine(_score.Snapshot.PauseBody.Replace('\n', ' '));
    }
}
