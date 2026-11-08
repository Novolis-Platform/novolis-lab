using System.Globalization;
using Microsoft.Extensions.Logging;
using Novolis.Physics.Ballistics;
using Novolis.Silk;

namespace ArtillerySimulator;

/// <summary>
/// Play kernel composition root: wires session, command apply, bootstrap, and frame loop.
/// No Silk frame loop, no agent host, no particle/audio wiring (see <see cref="BattlePresentationBinder"/>).
/// </summary>
internal sealed class BattleWorld : IPlayQuery, IBatteryApplier, IDisposable
{
    private readonly ILogger<BattleWorld> _log;
    private readonly ArtilleryRunOptions _options;
    private readonly TerrainWorld _terrain;
    private readonly AtmosphereModel _atmosphere;
    private readonly CrewDrill _crew = new();
    private readonly ArtilleryCameras _camera;
    private readonly FireControlUi _ui;
    private readonly PlayerBattleInput _input;
    private readonly BattleFxSink _fx;
    private readonly ArtilleryShellController _shell;
    private readonly BattlePresentationBinder _presentation;
    private readonly AimPreviewService _aim;
    private readonly ArtilleryRoundPool _rounds;
    private readonly ArtilleryBattleLoop _battle;
    private readonly ArtilleryAudioHub? _audio;
    private readonly BattleSession _session;
    private readonly FireAuthorizationRules _fireRules;
    private readonly BattleCommandApplier _commandApplier;
    private readonly LevelTransitionService _levelTransitions;
    private readonly BattleWorldBootstrap _bootstrap;
    private readonly BattleFrameCoordinator _frames;
    private readonly ScoreBoard _score;

    private GunModel _gun = null!;
    private ProjectileBallisticSimulation _ballistics = null!;
    private IDefenseMission? _mission;
    private BatteryRoster _battery;
    private bool _worldReady;

    public BattleWorld(
        ArtilleryRunOptions options,
        TerrainWorld terrain,
        AtmosphereModel atmosphere,
        ArtilleryCameras camera,
        FireControlUi ui,
        PlayerBattleInput input,
        BattleFxSink fx,
        ArtilleryShellController shell,
        BattlePresentationBinder presentation,
        ILogger<BattleWorld> log,
        ILoggerFactory logs)
    {
        _options = options;
        _terrain = terrain;
        _atmosphere = atmosphere;
        _camera = camera;
        _ui = ui;
        _input = input;
        _fx = fx;
        _shell = shell;
        _presentation = presentation;
        _log = log;

        var level = options.Level;
        if (!level.IsPlayable)
            level = GameLevelCatalog.Default;
        _shell.SetLevel(level);
        _battery = BatteryRoster.FromLevel(level);

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
            _rounds,
            _aim,
            _fx,
            logs.CreateLogger<ArtilleryBattleLoop>());
        _battle.Bind(_gun, _crew, _ballistics);

        _audio = ArtilleryAudioHub.TryCreate(
            _session.Seed,
            enabled: !options.SkipShell,
            logs.CreateLogger<ArtilleryAudioHub>());
        _score = new ScoreBoard(log: logs.CreateLogger<ScoreBoard>());
        _presentation.Bind(this, _audio);
        _fx.ImpactScored = (kills, pins) => _score.RegisterImpact(kills, pins);

        _fireRules = new FireAuthorizationRules(() => _gun, _crew, _aim, () => _mission);
        _commandApplier = new BattleCommandApplier(
            _terrain,
            _session,
            _crew,
            _rounds,
            _battle,
            _aim,
            _camera,
            _fireRules,
            () => _gun,
            () => IsPaused,
            ApplyCycleGun,
            _score,
            log);
        _levelTransitions = new LevelTransitionService(
            _session,
            _crew,
            _rounds,
            _battle,
            _aim,
            _shell,
            log);
        _bootstrap = new BattleWorldBootstrap(
            _terrain,
            _session,
            _rounds,
            _battle,
            _shell,
            _input,
            _score,
            log);
        _frames = new BattleFrameCoordinator(
            _session,
            _rounds,
            _battle,
            _camera,
            _input,
            _ui,
            _shell,
            _terrain,
            () => _gun,
            () => _worldReady,
            () => IsPaused,
            _fireRules,
            () => _mission,
            _score,
            log);

        _log.LogInformation(
            "Session ready: level={LevelId} piece={PieceId} seed={Seed} skipShell={SkipShell}",
            level.Id,
            _session.Piece.Id,
            _session.SeedHash,
            options.SkipShell);
    }

    public ArtilleryCameras Camera => _camera;
    public FireControlUi Ui => _ui;
    public ArtilleryRoundPool Rounds => _rounds;
    public ArtilleryBattleLoop Battle => _battle;
    public AimPreviewService Aim => _aim;
    public ArtilleryAudioHub? Audio => _audio;
    public ArtilleryShellController Shell => _shell;
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
    public float PredictedRangeMeters => _fireRules.PredictedRangeMeters;
    public string CameraModeName => "Orbit";
    public string PaceText => "realtime";
    public bool LaunchDetonation => _fireRules.LaunchDetonation;
    public bool BelowHeMin => _fireRules.BelowHeMin;
    public bool SolutionUnsafe => _fireRules.SolutionUnsafe;
    public bool IsMissionTerminal => _fireRules.IsMissionTerminal;
    public MissionOutcome MissionOutcome => _fireRules.MissionOutcome;
    public ArtilleryParticles Particles => _presentation.Particles;
    public float FxTime => _presentation.FxTime;
    public ScoreBoard Score => _score;
    public ScoreSnapshot ScoreSnapshot => _score.Snapshot;
    public string ActiveBatteryLabel => _battery.ActiveLabel;
    public int BatteryMountCount => _battery.Count;
    public int BatteryActiveIndex => _battery.ActiveIndex;

    public FrameViewModel CreateFrameViewModel() => FrameViewModelFactory.Create(this);

    private BallisticTrajectoryRunner CreateRound() =>
        new(new BallisticTrajectoryRunnerOptions
        {
            Step = new ProjectileTerrainStepOptions
            {
                DtSeconds = FixedStepClock.DtSeconds,
                ProjectileRadius = _gun.Spec.MuzzleRadiusMeters,
            },
        });

    public void BindPiece(IArtilleryPieceSpec piece) =>
        _levelTransitions.BindPiece(piece, ref _gun, ref _ballistics);

    public void SelectLevel(int delta)
    {
        _levelTransitions.SelectLevel(delta, ref _gun, ref _ballistics);
        _battery = BatteryRoster.FromLevel(_shell.Level);
        _worldReady = false;
        _mission = null;
    }

    public BattleCommandResult Apply(BattleCommand command) => _commandApplier.Apply(command);

    public string? FireBlockReason() => _fireRules.FireBlockReason();

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

    public void CycleGun() => _ = ApplyCycleGun();

    public void LoadWorld()
    {
        _battery = BatteryRoster.FromLevel(_shell.Level);
        _mission = _bootstrap.LoadWorld(ref _worldReady, ref _mission);
        SeatActiveMount(useMissionOpening: true);
        _shell.Menus.ClearToPlaying();
    }

    public void HandleInput(SilkFrame frame, IBatteryApplier applier) =>
        _frames.HandleInput(frame, applier);

    public int TickPhysics(float wallSeconds) => _frames.TickPhysics(wallSeconds);

    public void PresentFrame(SilkFrame frame, float wallSeconds) =>
        _frames.PresentFrame(frame, wallSeconds);

    public void Dispose() => _audio?.Dispose();

    private BattleCommandResult ApplyCycleGun()
    {
        if (!_worldReady || _mission is null)
            return BattleCommandResult.Fail("unavailable", "World not ready.");
        if (!_battery.CanCycle)
            return BattleCommandResult.Fail("unavailable", "This level has a single tube.");

        if (!_battery.TryCycle())
            return BattleCommandResult.Fail("unavailable", "Cannot cycle battery.");

        SeatActiveMount(useMissionOpening: false);
        return BattleCommandResult.Ok(
            string.Create(CultureInfo.InvariantCulture, $"Battery: {_battery.ActiveLabel} ({_gun.Spec.ShortName})."));
    }

    private void SeatActiveMount(bool useMissionOpening)
    {
        if (_mission is null)
            return;

        var mount = _battery.Active;
        var piece = ArtilleryPieceCatalog.Require(mount.PieceSpecId);
        BindPiece(piece);
        _mission.RetuneEquipment(piece);

        var pad = BatteryLandSnap.Snap(
            _terrain,
            _mission.GunX + mount.OffsetEastMeters,
            _mission.GunZ + mount.OffsetNorthMeters);
        _terrain.PlaceGun(pad.X, pad.Y, piece.GunHeightOffsetMeters);

        _gun.LayCenter(_mission.TrailBearingDegrees);
        var elev = mount.OpeningElevationDegrees;
        var useOpeningLay = useMissionOpening
            && string.Equals(mount.PieceSpecId, _shell.Level.PieceSpecId, StringComparison.OrdinalIgnoreCase);
        if (useOpeningLay)
            elev = _mission.OpeningElevationDegrees;
        elev = Math.Clamp(elev, piece.MinElevationDegrees, piece.MaxElevationDegrees);
        _gun.SetElevation(elev);
        if (useOpeningLay)
        {
            _gun.SetCharge(_mission.OpeningChargeIndex);
            _gun.NudgeAzimuth(_mission.OpeningAzimuthDegrees - _mission.TrailBearingDegrees);
        }
        else
        {
            _gun.SetCharge(Math.Min(1, piece.Charges.Count - 1));
        }

        _crew.SeatOpening(_gun);
        _camera.SnapToGun(_terrain.GunBaseline, _gun.BarrelDirection());
        _aim.Rebuild(_crew);
        _log.LogInformation(
            "Seated battery {Label} ({Piece}) at ({X:F0},{Z:F0})",
            mount.Label,
            piece.Id,
            pad.X,
            pad.Y);
    }
}
