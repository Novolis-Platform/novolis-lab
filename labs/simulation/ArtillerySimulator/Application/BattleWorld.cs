using System.Globalization;
using Microsoft.Extensions.Logging;
using Novolis.Physics.Ballistics;
using Novolis.Silk;

namespace ArtillerySimulator;

/// <summary>
/// Domain play kernel: gun, mission, fixed-step battle. No Silk frame loop, no agent host.
/// </summary>
internal sealed class BattleWorld : IPlayQuery, IBatteryApplier, IDisposable
{
    private readonly ILogger<BattleWorld> _log;
    private readonly ArtilleryRunOptions _options;
    private readonly TerrainWorld _terrain;
    private readonly AtmosphereModel _atmosphere;
    private readonly CrewDrill _crew = new();
    private readonly ArtilleryCameras _camera;
    private readonly ArtilleryParticles _particles;
    private readonly FireControlUi _ui;
    private readonly PlayerBattleInput _input;
    private readonly BattleFxSink _fx;
    private readonly ArtilleryShellController _shell;
    private readonly AimPreviewService _aim;
    private readonly ArtilleryRoundPool _rounds;
    private readonly ArtilleryBattleLoop _battle;
    private readonly ArtilleryAudioHub? _audio;
    private readonly BattleSession _session;

    private GunModel _gun = null!;
    private ProjectileBallisticSimulation _ballistics = null!;
    private IDefenseMission? _mission;
    private float _fxTime;
    private bool _worldReady;

    public BattleWorld(
        ArtilleryRunOptions options,
        TerrainWorld terrain,
        AtmosphereModel atmosphere,
        ArtilleryCameras camera,
        ArtilleryParticles particles,
        FireControlUi ui,
        PlayerBattleInput input,
        BattleFxSink fx,
        ArtilleryShellController shell,
        ILogger<BattleWorld> log,
        ILoggerFactory logs)
    {
        _options = options;
        _terrain = terrain;
        _atmosphere = atmosphere;
        _camera = camera;
        _particles = particles;
        _ui = ui;
        _input = input;
        _fx = fx;
        _shell = shell;
        _log = log;

        var level = options.Level;
        if (!level.IsPlayable)
            level = GameLevelCatalog.Default;
        _shell.SetLevel(level);

        var resolved = level.ResolvePiece();
        _session = new BattleSession(
            resolved,
            options.Seed ?? HashCode.Combine(Environment.TickCount, level.Id));

        _aim = new AimPreviewService(_terrain, _atmosphere, _session);
        _gun = new GunModel(_session.Piece);
        _ballistics = new ProjectileBallisticSimulation(_session.Piece.Projectile, DragCoefficientLaw.BoatTail);
        _aim.Bind(_gun, _ballistics);
        _rounds = new ArtilleryRoundPool(CreateRound);
        _battle = new ArtilleryBattleLoop(
            _terrain,
            _atmosphere,
            _session,
            _rounds,
            _aim,
            _fx,
            logs.CreateLogger<ArtilleryBattleLoop>());
        _battle.Bind(_gun, _crew, _ballistics);
        WireFx();

        _audio = ArtilleryAudioHub.TryCreate(
            _session.Seed,
            enabled: !options.SkipShell,
            logs.CreateLogger<ArtilleryAudioHub>());

        _log.LogInformation(
            "Session ready: level={LevelId} piece={PieceId} seed={Seed} skipShell={SkipShell}",
            level.Id,
            _session.Piece.Id,
            _session.SeedHash,
            options.SkipShell);
    }

    public ArtilleryCameras Camera => _camera;
    public ArtilleryParticles Particles => _particles;
    public FireControlUi Ui => _ui;
    public ArtilleryRoundPool Rounds => _rounds;
    public ArtilleryBattleLoop Battle => _battle;
    public AimPreviewService Aim => _aim;
    public ArtilleryAudioHub? Audio => _audio;
    public ArtilleryShellController Shell => _shell;
    public float FxTime => _fxTime;
    public ArtilleryRunOptions Options => _options;

    public IGameLevel Level => _shell.Level;
    public bool IsPaused => _shell.IsPaused;
    public bool WorldReady => _worldReady;
    public GunModel Gun => _gun;
    public CrewDrill Crew => _crew;
    public BallisticTrajectoryRunner Shot => _rounds.Active;
    public TerrainWorld Terrain => _terrain;
    public AtmosphereModel Atmosphere => _atmosphere;
    public IDefenseMission? Mission => _mission;
    public BattleSession Session => _session;
    public DeliverySampler Delivery => _session.Delivery;
    public float PredictedRangeMeters => _aim.PredictedRangeMeters();
    public string CameraModeName => "Orbit";
    public string PaceText => "realtime";
    public bool LaunchDetonation => PredictedRangeMeters < _gun.Spec.LaunchClearanceMeters;
    public bool BelowHeMin => !LaunchDetonation && PredictedRangeMeters < _gun.Spec.MinEffectiveRangeMeters;
    public bool SolutionUnsafe => LaunchDetonation || BelowHeMin;
    public bool IsMissionTerminal => _mission?.IsTerminal == true;
    public MissionOutcome MissionOutcome => _mission?.Outcome ?? MissionOutcome.Open;

    private void WireFx()
    {
        _fx.Muzzle = (muzzle, dir) =>
        {
            _particles.SpawnMuzzle(muzzle, dir);
            _audio?.OnFire();
        };
        _fx.Burst = (at, airburst) =>
        {
            _particles.SpawnBurst(at, airburst);
            _audio?.OnImpact(airburst);
        };
        _fx.KillcamExpired = () =>
        {
            _camera.FollowProjectile = false;
            _camera.SnapToGun(_terrain.GunBaseline, _gun.BarrelDirection());
        };
        _fx.FrameFx = (wall, playing, mission) =>
        {
            _particles.Update(wall);
            _fxTime += wall;
            if (playing)
            {
                _audio?.Update(
                    wall,
                    playing: true,
                    remaining: mission?.Remaining ?? 0,
                    nearestMeters: mission?.NearestMeters ?? 0f,
                    mission?.Outcome ?? MissionOutcome.Open);
            }
        };
    }

    private BallisticTrajectoryRunner CreateRound() =>
        new(new BallisticTrajectoryRunnerOptions
        {
            Step = new ProjectileTerrainStepOptions
            {
                DtSeconds = FixedStepClock.DtSeconds,
                ProjectileRadius = _gun.Spec.MuzzleRadiusMeters,
            },
        });

    public void BindPiece(IArtilleryPieceSpec piece)
    {
        _session.RebindPiece(piece);
        _gun = new GunModel(piece);
        _ballistics = new ProjectileBallisticSimulation(piece.Projectile, DragCoefficientLaw.BoatTail);
        _aim.Bind(_gun, _ballistics);
        _battle.Bind(_gun, _crew, _ballistics);
        _aim.MarkDirty();
    }

    public void SelectLevel(int delta)
    {
        var next = GameLevelCatalog.NextPlayable(_shell.Level, delta);
        if (string.Equals(next.Id, _shell.Level.Id, StringComparison.OrdinalIgnoreCase))
            return;

        _shell.SetLevel(next);
        BindPiece(next.ResolvePiece());
        _rounds.ResetAll(_battle.ImpactHandled);
        _log.LogInformation("Selected level {LevelId}", next.Id);
    }

    public BattleCommandResult Apply(BattleCommand command) =>
        command.Kind switch
        {
            BattleCommandKind.Aim => ApplyAim(command),
            BattleCommandKind.Charge => ApplyCharge(command),
            BattleCommandKind.Fire => ApplyFire(),
            BattleCommandKind.FollowCamera => ApplyFollow(command.FollowShell),
            _ => BattleCommandResult.Fail("unavailable", "Unknown battle command."),
        };

    private BattleCommandResult ApplyAim(BattleCommand command)
    {
        if (IsPaused)
            return BattleCommandResult.Fail("paused", "Battle clock paused — resume (Esc) before commanding the gun.");

        var elev = Math.Clamp(command.ElevationDegrees, _gun.Spec.MinElevationDegrees, _gun.Spec.MaxElevationDegrees);
        _crew.OrderAim(_gun, elev, command.AzimuthDegrees);
        _aim.Rebuild(_crew);
        var message = _crew.Ready(_gun)
            ? string.Create(CultureInfo.InvariantCulture, $"On lay at {_crew.OrderedElevation:F1}° elevation, {_crew.OrderedAzimuth:F1}° azimuth.")
            : string.Create(CultureInfo.InvariantCulture, $"Laying to {_crew.OrderedElevation:F1}° / {_crew.OrderedAzimuth:F1}°. Tube at {_gun.ElevationDegrees:F1}° / {_gun.AzimuthDegrees:F1}°.");
        return BattleCommandResult.Ok(message);
    }

    private BattleCommandResult ApplyCharge(BattleCommand command)
    {
        if (IsPaused)
            return BattleCommandResult.Fail("paused", "Battle clock paused — resume (Esc) before commanding the gun.");

        var index = command.ChargeIndex;
        if (index < 0 || index >= _gun.Spec.Charges.Count)
            return BattleCommandResult.Fail("invalid-arguments", $"Charge index must be 0..{_gun.Spec.Charges.Count - 1}.");

        _crew.OrderCharge(_gun, index);
        _aim.Rebuild(_crew);
        var message = _crew.Ready(_gun)
            ? string.Create(CultureInfo.InvariantCulture, $"Charge {_gun.ChargeLabel} ({_gun.MuzzleSpeedMps:F0} m/s).")
            : string.Create(CultureInfo.InvariantCulture, $"Charge {_gun.ChargeLabel} ({_gun.MuzzleSpeedMps:F0} m/s). {_crew.Phrase(_gun)}");
        return BattleCommandResult.Ok(message);
    }

    private BattleCommandResult ApplyFire()
    {
        if (IsPaused)
            return BattleCommandResult.Fail("paused", "Battle clock paused — resume (Esc) before commanding the gun.");

        if (FireBlockReason() is { } blocked)
            return BattleCommandResult.Fail("unavailable", blocked);

        _crew.SnapTube(_gun);
        var muzzle = _gun.MuzzlePosition(_terrain.GunBaseline);
        var delivery = _session.Delivery.SampleFire(_gun.ElevationDegrees, _gun.AzimuthDegrees, _gun.MuzzleSpeedMps);
        var fireDir = GunModel.DirectionOf(delivery.ElevationDegrees, delivery.AzimuthDegrees);
        _rounds.Active = _rounds.Acquire(_camera.FollowProjectile, _battle.ImpactHandled);
        _rounds.Active.Begin(_gun.CreateProjectileState(muzzle, fireDir, delivery.MuzzleSpeedMps));
        _battle.ImpactHandled.Remove(_rounds.Active);
        _battle.NotifyFired(muzzle, fireDir);
        _crew.BeginReload();
        _battle.KillcamActive = _camera.FollowProjectile;
        _aim.MarkDirty();
        _log.LogDebug(
            "Fired {Charge} at elev={Elev:F1} az={Az:F1}",
            _gun.ChargeLabel,
            _gun.ElevationDegrees,
            _gun.AzimuthDegrees);
        return BattleCommandResult.Ok("Round away. Impact will arrive as artillery.impact.", _rounds.Active);
    }

    private BattleCommandResult ApplyFollow(bool followShell)
    {
        _camera.FollowProjectile = followShell;
        _battle.KillcamActive = followShell;
        return BattleCommandResult.Ok(followShell ? "Camera follows the shell." : "Camera follows the gun.");
    }

    public string? FireBlockReason()
    {
        if (IsMissionTerminal)
            return MissionOutcome switch
            {
                MissionOutcome.Held => "City held — mission complete.",
                MissionOutcome.Fratricide => "Fratricide — mission lost.",
                MissionOutcome.ObjectiveLost => "Crossing lost — mission over.",
                _ => "Mission complete.",
            };

        if (!_crew.BreechReady)
            return _crew.Phrase(_gun);
        if (LaunchDetonation)
            return "No clearance — predicted detonation at the piece.";
        return null;
    }

    public void NudgeOrder(float deltaElevationDegrees, float deltaTraverseDegrees)
    {
        _crew.NudgeOrder(_gun, deltaElevationDegrees, deltaTraverseDegrees);
        _aim.MarkDirty();
    }

    public void MarkAimDirty() => _aim.MarkDirty();

    public void ToggleOptionalShellFollow() =>
        _input.ToggleOptionalShellFollow(_camera, _battle, _rounds, _terrain, _gun);

    public void SnapCameraToGun() => _camera.SnapToGun(_terrain.GunBaseline, _gun.BarrelDirection());

    public void SnapCameraToBattle()
    {
        if (_mission is not { } mission)
            return;
        _camera.SnapToGun(_terrain.GunBaseline, _gun.BarrelDirection());
        _camera.LookAtBattle(_terrain.GunBaseline, mission.NearestAlive(_terrain.GunBaseline));
    }

    public void ShowMapOverview() => _camera.ShowMapOverview(_terrain.EastMeters, _terrain.NorthMeters);

    public void LoadWorld()
    {
        if (_worldReady)
            return;

        var level = _shell.Level;
        _shell.SetLoadingStatus($"Building {level.DisplayName}…");
        level.RebuildMap(_terrain);
        _shell.SetLoadingStatus($"Seating {_gun.Spec.ShortName} battery…");
        _session.Clock.Reset();
        _mission = level.StartMission(_terrain, _session.Piece, _session.Seed);
        _battle.SetMission(_mission);
        _terrain.PlaceGun(_mission.GunX, _mission.GunZ, _session.Piece.GunHeightOffsetMeters);
        _gun.LayCenter(_mission.TrailBearingDegrees);
        _gun.SetCharge(_mission.OpeningChargeIndex);
        _gun.SetElevation(_mission.OpeningElevationDegrees);
        _gun.NudgeAzimuth(_mission.OpeningAzimuthDegrees - _mission.TrailBearingDegrees);
        _crew.SeatOpening(_gun);
        _rounds.ResetAll(_battle.ImpactHandled);
        _battle.ResetVisuals();
        _input.Reset();
        _camera.SnapToGun(_terrain.GunBaseline, _gun.BarrelDirection());
        _aim.Rebuild(_crew);
        _shell.Menus.ClearToPlaying();
        _worldReady = true;

        _log.LogInformation(
            "World loaded: approaches={Approaches} remaining={Remaining}",
            _mission.SpawnPointCount,
            _mission.Remaining);
    }

    /// <summary>Player input only — never advances physics.</summary>
    public void HandleInput(SilkFrame frame, IBatteryApplier applier)
    {
        if (!_worldReady)
            return;

        if (IsPaused)
        {
            _input.Handle(frame, 0f, paused: true, IsMissionTerminal, applier, _ui);
            return;
        }

        var controlSeconds = MathF.Max(1e-4f, frame.DeltaSeconds);
        if (!IsMissionTerminal)
            _input.Handle(frame, controlSeconds, paused: false, IsMissionTerminal, applier, _ui);
    }

    /// <summary>
    /// Fixed-step physics from wall time. Independent of how long render will take;
    /// <see cref="FixedStepClock"/> caps steps so a hitch cannot explode this call.
    /// </summary>
    public int TickPhysics(float wallSeconds)
    {
        if (!_worldReady || IsPaused || IsMissionTerminal)
            return 0;

        var steps = _session.Clock.Consume(wallSeconds);
        if (steps > 0)
            _battle.AdvancePhysics(steps, steps * FixedStepClock.DtSeconds);

        MaybePauseOnMissionEnd();
        return steps;
    }

    /// <summary>Camera + FX + aim preview at display rate — never calls ballistics steps.</summary>
    public void PresentFrame(SilkFrame frame, float wallSeconds)
    {
        if (!_worldReady)
            return;

        _battle.AdvancePresentation(wallSeconds, audioPlaying: _shell.Menus.IsPlaying && !IsPaused);

        if (_mission is { } mission)
            _camera.SetBattleFocus(mission.BattleFocus(_terrain.GunBaseline));
        var camRound = _rounds.RoundForCamera(_battle.BurstAge, ArtilleryBattleLoop.KillcamReturnSeconds) ?? _rounds.Active;
        if (_camera.FollowProjectile && camRound.Phase == BallisticTrajectoryPhase.InFlight)
            _rounds.Active = camRound;
        _camera.Update(frame, _terrain.GunBaseline, camRound, allowMouseLook: !_ui.PointerOverUi);
    }

    private void MaybePauseOnMissionEnd()
    {
        if (!IsMissionTerminal || !_shell.Menus.IsPlaying)
            return;

        _shell.Menus.PushPause();
        _log.LogInformation("Mission ended: {Outcome}", MissionOutcome);
        Console.WriteLine(_mission?.Observation() ?? "Mission complete.");
    }

    public void Dispose() => _audio?.Dispose();
}
