using System.Drawing;
using System.Globalization;
using System.Numerics;
using System.Text;
using Novolis.Agent.Surface;
using Novolis.Math.Geometry;
using Novolis.Physics.Ballistics;
using Novolis.Silk;

namespace ArtillerySimulator;

internal sealed class ArtilleryApp : IDisposable
{
    private const float FineMilsPerSecond = 12f;
    /// <summary>Wall-clock seconds of WASD hold to reach full lay rate (smooth take-up).</summary>
    private const float LayRampSeconds = 0.4f;
    private const float KillcamReturnSeconds = 3f;
    private const int MaxLiveRounds = 12;

    private static readonly Color TrailHot = BallisticArcColors.Flight;
    private static readonly Color TrailCool = Color.FromArgb(90, 180, 140, 70);
    private static readonly Color FlashColor = Color.FromArgb(255, 255, 214, 150);
    private static readonly Rgba32 SkyZenith = new(28, 58, 98, 255);
    private static readonly Rgba32 SkyHorizon = new(210, 140, 90, 220);
    private static readonly Rgba32 ClearSky = new(18, 28, 42, 255);

    private readonly TerrainWorld _terrain = new();
    private readonly BattleSession _session;
    private GunModel _gun = null!;
    private readonly List<BallisticTrajectoryRunner> _rounds = [];
    private BallisticTrajectoryRunner _shot = null!;
    private ProjectileBallisticSimulation _ballistics = null!;
    private readonly ArtilleryCameras _camera = new();
    private readonly AtmosphereModel _atmosphere = AtmosphereModel.CreateRegionalDefault();
    private readonly SilkWireBatch _wires = new();
    private readonly WorldSolidBatch _solids = new();
    private readonly ArtilleryParticles _particles = new();
    private readonly FireControlUi _ui = new();
    private readonly ShellOverlay _shell = new();
    private readonly ProfileOverlay _profileOverlay = new();
    private readonly FrameProfiler _liveProfiler = new(warmupFrames: 5);
    private readonly bool _skipShell;
    private readonly ArtilleryMenuFlow _menus = new();
    private readonly ArtilleryAudioHub? _audio;
    private IGameLevel _level;
    private float _previewGunElev;
    private float _previewGunAz;
    private int _previewCharge = -1;
    private float _fxTime;

    private readonly SmoothedFps _fps = new();
    private readonly CrewDrill _crew = new();
    private IDefenseMission? _mission;
    private string _loadingStatus = "Loading map…";
    private bool _worldReady;
    private IReadOnlyList<Vector3> _orderedPreview = [];
    private IReadOnlyList<Vector3> _tubePreview = [];
    private readonly List<IReadOnlyList<Vector3>> _dispersionPreviews = [];
    private bool _aimDirty = true;
    private float _muzzleFlash;
    private float _burstAge = -1f;
    private Vector3 _burstAt;
    private bool _killcamActive;
    private float _layHoldSeconds;
    private readonly HashSet<BallisticTrajectoryRunner> _impactHandled = [];
    private readonly ArtilleryLiveHost _agent;
    private AgentSurface? _surface;
    private FrameProfiler? _profiler;
    private int _profileFramesRemaining = -1;
    private string? _profileJsonPath;
    private bool _showProfile;
    private string _profileFlash = "";
    private float _profileFlashAge = -1f;

    public ArtilleryApp(
        IGameLevel? level = null,
        IArtilleryPieceSpec? piece = null,
        bool skipShell = false,
        int? seed = null)
    {
        _skipShell = skipShell;
        _level = level ?? GameLevelCatalog.Default;
        if (!_level.IsPlayable)
            _level = GameLevelCatalog.Default;
        var resolved = piece ?? _level.ResolvePiece();
        _session = new BattleSession(resolved, seed ?? HashCode.Combine(Environment.TickCount, _level.Id));
        BindPiece(_session.Piece);
        _shot = CreateRound();
        _rounds.Add(_shot);
        _agent = new ArtilleryLiveHost(this);
        _audio = ArtilleryAudioHub.TryCreate(_session.Seed, enabled: !skipShell);
        if (!skipShell)
            _menus.ShowSplash();
    }

    private void BindPiece(IArtilleryPieceSpec piece)
    {
        _session.RebindPiece(piece);
        _gun = new GunModel(piece);
        _ballistics = new ProjectileBallisticSimulation(piece.Projectile, DragCoefficientLaw.BoatTail);
        _aimDirty = true;
    }

    private void SelectLevel(int delta)
    {
        var next = GameLevelCatalog.NextPlayable(_level, delta);
        if (string.Equals(next.Id, _level.Id, StringComparison.OrdinalIgnoreCase))
            return;

        _level = next;
        BindPiece(_level.ResolvePiece());
        foreach (var round in _rounds)
            round.Reset();
        _impactHandled.Clear();
        if (_rounds.Count > 0)
            _shot = _rounds[0];
    }

    internal bool IsPaused => _menus.IsPaused || _menus.IsLegal;
    internal ShellPhase Phase => _menus.Phase;
    internal IGameLevel Level => _level;
    internal bool WorldReady => _worldReady;
    internal bool IsMissionTerminal => _mission?.IsTerminal == true;
    internal MissionOutcome MissionOutcome => _mission?.Outcome ?? MissionOutcome.Open;

    private BallisticTrajectoryRunner CreateRound() =>
        new(new BallisticTrajectoryRunnerOptions
        {
            Step = new ProjectileTerrainStepOptions
            {
                DtSeconds = FixedStepClock.DtSeconds,
                ProjectileRadius = _gun.Spec.MuzzleRadiusMeters,
            },
        });

    public void EnableProfile(int frames, string? jsonPath = null)
    {
        _profiler = new FrameProfiler(warmupFrames: Math.Min(20, Math.Max(0, frames / 6)));
        _profileFramesRemaining = Math.Max(1, frames);
        _profileJsonPath = jsonPath;
    }

    public FrameProfiler? Profiler => _profiler;

    internal GunModel Gun => _gun;
    internal DeliverySampler Delivery => _session.Delivery;
    internal BattleSession Session => _session;
    internal TerrainWorld Terrain => _terrain;
    internal BallisticTrajectoryRunner Shot => _shot;
    internal AtmosphereModel Atmosphere => _atmosphere;
    internal string CameraModeName => "Orbit";
    internal string PaceText => "realtime";
    internal IDefenseMission? Mission => _mission;
    internal float PredictedRangeMeters => HorizontalRange(_terrain.GunBaseline, PredictedImpact());
    internal CrewDrill Crew => _crew;

    internal void OrderAim(float elevation, float azimuth)
    {
        _crew.OrderAim(_gun, elevation, azimuth);
        RebuildAimPreview();
    }

    internal void OrderCharge(int index)
    {
        _crew.OrderCharge(_gun, index);
        RebuildAimPreview();
    }

    internal void RefreshPrediction() => RebuildAimPreview();

    internal void SetFollowCamera(bool followShell) => _camera.FollowProjectile = followShell;

    /// <summary>True when the arc dies at the piece (terrain / muzzle clearance) — blocks fire.</summary>
    internal bool LaunchDetonation => PredictedRangeMeters < _gun.Spec.LaunchClearanceMeters;

    /// <summary>Below HE min (warning / red tint) — fire still allowed if the shell clears the piece.</summary>
    internal bool BelowHeMin =>
        !LaunchDetonation && PredictedRangeMeters < _gun.Spec.MinEffectiveRangeMeters;

    /// <summary>Red-tinge aim when clearance fails or the MPI is inside HE min.</summary>
    internal bool SolutionUnsafe => LaunchDetonation || BelowHeMin;

    internal string? FireBlockReason()
    {
        if (IsMissionTerminal)
            return MissionOutcome switch
            {
                MissionOutcome.Held => "City held — mission complete.",
                MissionOutcome.Fratricide => "Fratricide — mission lost.",
                MissionOutcome.ObjectiveLost => "Crossing lost — mission over.",
                _ => "Mission complete.",
            };

        // Autoloader only — rounds already in the air do not block the next fire.
        // HE min is a warning (red arcs), not a hard fire interlock.
        if (!_crew.BreechReady)
            return _crew.Phrase(_gun);
        if (LaunchDetonation)
            return "No clearance — predicted detonation at the piece.";
        return null;
    }

    internal bool TryFire()
    {
        if (FireBlockReason() is not null)
            return false;

        // Fire the ordered lay the instant the breech is closed — do not wait out the slew.
        _crew.SnapTube(_gun);
        var muzzle = _gun.MuzzlePosition(_terrain.GunBaseline);
        // PE residuals from the piece error budget; live met stays on AtmosphereModel.
        var delivery = _session.Delivery.SampleFire(_gun.ElevationDegrees, _gun.AzimuthDegrees, _gun.MuzzleSpeedMps);
        var fireDir = GunModel.DirectionOf(delivery.ElevationDegrees, delivery.AzimuthDegrees);
        _shot = AcquireRound();
        _shot.Begin(_gun.CreateProjectileState(muzzle, fireDir, delivery.MuzzleSpeedMps));
        _impactHandled.Remove(_shot);
        _muzzleFlash = 0.22f;
        _particles.SpawnMuzzle(muzzle, fireDir);
        _audio?.OnFire();
        _crew.BeginReload();
        // New round becomes the follow target if shell-cam is already on; otherwise stay on the gun.
        if (_camera.FollowProjectile)
        {
            _killcamActive = true;
        }
        else
        {
            _killcamActive = false;
        }

        _aimDirty = true;
        return true;
    }

    private BallisticTrajectoryRunner AcquireRound()
    {
        foreach (var round in _rounds)
        {
            if (round.Phase is BallisticTrajectoryPhase.Ready)
                return round;
            if (round.Phase == BallisticTrajectoryPhase.Impacted
                && round != _shot
                && !_camera.FollowProjectile)
            {
                _impactHandled.Remove(round);
                round.Reset();
                return round;
            }
        }

        if (_rounds.Count >= MaxLiveRounds)
        {
            // Prefer recycling the oldest finished round; otherwise the oldest flyer.
            for (var i = 0; i < _rounds.Count; i++)
            {
                if (_rounds[i].Phase != BallisticTrajectoryPhase.InFlight && _rounds[i] != _shot)
                {
                    _impactHandled.Remove(_rounds[i]);
                    _rounds[i].Reset();
                    return _rounds[i];
                }
            }

            var steal = _rounds[0];
            _impactHandled.Remove(steal);
            steal.Reset();
            return steal;
        }

        var created = CreateRound();
        _rounds.Add(created);
        return created;
    }

    public void Initialize(SilkFrame frame)
    {
        _shell.EnsureAssetsLoaded();
        frame.SetTitle(ProductIdentity.WindowTitle);
        TrySetWindowIcon(frame);
        if (_skipShell)
            LoadWorldAndAttachAgent();
    }

    private static void TrySetWindowIcon(SilkFrame frame)
    {
        foreach (var relative in new[] { "assets/k9-dog-icon.png", "assets/k9-dog-icon.jpg" })
        {
            var path = Path.Combine(AppContext.BaseDirectory, relative);
            if (!File.Exists(path))
                continue;

            try
            {
                using var bmp = SkiaSharp.SKBitmap.Decode(path);
                if (bmp is null)
                    continue;

                // Silk wants tightly packed RGBA8; normalize from Skia pixels.
                var w = bmp.Width;
                var h = bmp.Height;
                var bytes = new byte[checked(w * h * 4)];
                for (var y = 0; y < h; y++)
                {
                    for (var x = 0; x < w; x++)
                    {
                        var c = bmp.GetPixel(x, y);
                        var o = (y * w + x) * 4;
                        bytes[o] = c.Red;
                        bytes[o + 1] = c.Green;
                        bytes[o + 2] = c.Blue;
                        bytes[o + 3] = c.Alpha;
                    }
                }

                frame.SetWindowIcon(w, h, bytes);
                return;
            }
            catch
            {
                // Fall through — title bar keeps the platform default.
            }
        }
    }

    public void Update(SilkFrame frame)
    {
        HandleShellKeys(frame);
        HandleProfileHotkeys(frame);

        if (_menus.IsSplash || (_menus.IsLegal && !_worldReady))
        {
            frame.Clear(Rgba32.Black);
            var status = _menus.IsSplash
                ? ProductLegal.SplashControls
                : ProductLegal.LegalCloseHint;
            _shell.Draw(frame, _menus.Phase, status, _level);
            return;
        }

        if (_menus.IsLoading)
        {
            frame.Clear(Rgba32.Black);
            _shell.Draw(frame, _menus.Phase, _loadingStatus, _level);
            if (!_worldReady)
            {
                _loadingStatus = $"Building {_level.DisplayName}…";
                LoadWorldAndAttachAgent();
                _loadingStatus = "Ready";
                _menus.ClearToPlaying();
            }

            return;
        }

        var active = ActiveProfiler();
        active?.BeginFrame();
        if (active is not null)
        {
            active.Time("simulate", () => _agent.RunFrame(() => Simulate(frame)));
            active.Time("render", () => Render(frame));
            active.SetGeometry(
                _terrain.TerrainTriangleCount,
                _terrain.RoadLineCount,
                _wires.AsSpan().Length / 2,
                FireControlUi.LastOverlayLineCount);
            active.EndFrame();
            if (_profiler is not null && _profileFramesRemaining > 0)
            {
                _profileFramesRemaining--;
                if (_profileFramesRemaining == 0)
                {
                    WriteProfileReport();
                    frame.Close();
                }
            }
        }
        else
        {
            _agent.RunFrame(() => Simulate(frame));
            Render(frame);
        }

        if (_menus.IsPaused || _menus.IsLegal)
            _shell.Draw(frame, _menus.Phase, "", _level);

        if (_profileFlashAge >= 0f)
            _profileFlashAge += frame.DeltaSeconds;
    }

    private void LoadWorldAndAttachAgent()
    {
        LoadWorld(attachAgent: true);
    }

    /// <summary>
    /// Boots terrain + mission. Tests pass <paramref name="attachAgent"/> false to avoid binding HTTP.
    /// </summary>
    internal void LoadWorld(bool attachAgent = true)
    {
        if (_worldReady)
            return;

        _loadingStatus = $"Building {_level.DisplayName}…";
        _level.RebuildMap(_terrain);
        _loadingStatus = $"Seating {_gun.Spec.ShortName} battery…";
        _session.Clock.Reset();
        _mission = _level.StartMission(_terrain, _session.Piece, _session.Seed);
        _terrain.PlaceGun(_mission.GunX, _mission.GunZ, _session.Piece.GunHeightOffsetMeters);
        _gun.LayCenter(_mission.TrailBearingDegrees);
        _gun.SetCharge(_mission.OpeningChargeIndex);
        _gun.SetElevation(_mission.OpeningElevationDegrees);
        _gun.NudgeAzimuth(_mission.OpeningAzimuthDegrees - _mission.TrailBearingDegrees);
        _crew.SeatOpening(_gun);
        foreach (var round in _rounds)
            round.Reset();
        _impactHandled.Clear();
        _shot = _rounds[0];
        _layHoldSeconds = 0f;
        _camera.SnapToGun(_terrain.GunBaseline, _gun.BarrelDirection());
        RebuildAimPreview();
        _menus.ClearToPlaying();
        _worldReady = true;

        if (!attachAgent)
            return;

        var def = AgentSurfaceDefinition.From<IArtillerySurface>();
        _surface = AgentSurface.AttachAll(
            _agent,
            def,
            new AgentAttachOptions { EnableStdio = false, EnableMcpStdio = false });
        if (_surface?.Http is { } http)
            Console.WriteLine($"Agent surface ready: {http.BaseUrl}  (MCP tools at /agent/mcp/tools)");
        else
            Console.WriteLine($"Agent surface attach incomplete — expected HTTP :{def.DefaultHttpPort}");
        Console.WriteLine(ProductLegal.Tagline);
        Console.WriteLine($"Session seed={_session.SeedHash}  level={_level.Id}  piece={_session.Piece.Id}");
    }

    /// <summary>
    /// Advances the authoritative battle clock by wall time without a Silk frame.
    /// Returns the number of fixed physics steps consumed.
    /// </summary>
    internal int TickBattle(float wallSeconds)
    {
        if (!_worldReady || IsPaused || IsMissionTerminal)
            return 0;

        var steps = _session.Clock.Consume(wallSeconds);
        if (steps <= 0)
            return 0;

        var battleSeconds = steps * FixedStepClock.DtSeconds;
        AdvanceBattleSystems(steps, battleSeconds, wallSeconds);
        MaybePauseOnMissionEnd();
        return steps;
    }

    private void AdvanceBattleSystems(int steps, float battleSeconds, float wallSeconds)
    {
        _atmosphere.Advance(battleSeconds);
        if (_atmosphere.ConsumeAimInvalidation())
            _aimDirty = true;
        _crew.Advance(_gun, battleSeconds);

        Vector3? incoming = null;
        foreach (var round in _rounds)
        {
            if (round.Phase != BallisticTrajectoryPhase.InFlight)
                continue;

            for (var i = 0; i < steps && round.Phase == BallisticTrajectoryPhase.InFlight; i++)
            {
                var env = _atmosphere.BallisticEnvironment(round.CurrentPosition.Y);
                round.AdvanceOne(_ballistics, env, _terrain.Collision, _terrain.Field, _terrain.GunBaseline);
            }

            if (round.Phase == BallisticTrajectoryPhase.InFlight)
                incoming ??= round.CurrentPosition;
        }

        _mission?.Advance(battleSeconds, incoming);

        foreach (var round in _rounds)
        {
            if (round.Phase != BallisticTrajectoryPhase.Impacted
                || round.Impact is not { } landed
                || !_impactHandled.Add(round))
                continue;

            _burstAge = 0f;
            _burstAt = BurstPoint(landed.Position);
            var airburst = _gun.Spec.Warhead.IsAirburst;
            _particles.SpawnBurst(_burstAt, airburst);
            _audio?.OnImpact(airburst);
            _mission?.ResolveImpact(landed.Position);
        }

        if (_burstAge >= 0f)
        {
            _burstAge += wallSeconds;
            if (_killcamActive && _burstAge >= KillcamReturnSeconds)
            {
                _killcamActive = false;
                _camera.FollowProjectile = false;
                _camera.SnapToGun(_terrain.GunBaseline, _gun.BarrelDirection());
            }
        }

        if (_muzzleFlash > 0f)
            _muzzleFlash = MathF.Max(0f, _muzzleFlash - wallSeconds);

        _particles.Update(wallSeconds);
        _fxTime += wallSeconds;
        if (_menus.IsPlaying)
        {
            _audio?.Update(
                wallSeconds,
                playing: true,
                remaining: _mission?.Remaining ?? 0,
                nearestMeters: _mission?.NearestMeters ?? 0f,
                _mission?.Outcome ?? MissionOutcome.Open);
        }

        if (_aimDirty || TubePreviewStale())
            RebuildAimPreview();
    }

    private void MaybePauseOnMissionEnd()
    {
        if (!IsMissionTerminal || !_menus.IsPlaying)
            return;

        _menus.PushPause();
        Console.WriteLine(_mission?.Observation() ?? "Mission complete.");
    }

    private void HandleShellKeys(SilkFrame frame)
    {
        if (_menus.IsSplash)
        {
            if (frame.IsKeyPressed(Key.Left) || frame.IsKeyPressed(Key.A))
            {
                SelectLevel(-1);
                return;
            }

            if (frame.IsKeyPressed(Key.Right) || frame.IsKeyPressed(Key.D))
            {
                SelectLevel(+1);
                return;
            }

            if (frame.IsKeyPressed(Key.L))
            {
                _shell.ResetLegalScroll();
                _menus.PushLegal();
                return;
            }

            if (frame.IsKeyPressed(Key.Enter) || frame.IsKeyPressed(Key.Space))
                _menus.ShowLoading();

            return;
        }

        if (_menus.IsLegal)
        {
            HandleLegalScroll(frame);
            if (frame.IsKeyPressed(Key.Escape)
                || frame.IsKeyPressed(Key.L)
                || frame.IsKeyPressed(Key.Enter))
                _menus.PopOverlay();
            return;
        }

        if (!_worldReady)
            return;

        if (frame.IsKeyPressed(Key.L))
        {
            if (!_menus.IsLegal)
                _shell.ResetLegalScroll();
            _menus.ToggleLegal();
            return;
        }

        if (frame.IsKeyPressed(Key.Escape) || frame.IsKeyPressed(Key.P))
        {
            _menus.TogglePause();
            return;
        }

        if (_menus.IsPaused && frame.IsKeyPressed(Key.Enter))
            _menus.PopOverlay();
    }

    private void HandleLegalScroll(SilkFrame frame)
    {
        if (MathF.Abs(frame.ScrollDelta) > 0.001f)
            _shell.NudgeLegalScroll(-frame.ScrollDelta * 3f);
        if (frame.IsKeyPressed(Key.Up) || frame.IsKeyPressed(Key.W))
            _shell.NudgeLegalScroll(-8f);
        if (frame.IsKeyPressed(Key.Down) || frame.IsKeyPressed(Key.S))
            _shell.NudgeLegalScroll(+8f);
        if (frame.IsKeyDown(Key.Up) || frame.IsKeyDown(Key.W))
            _shell.NudgeLegalScroll(-frame.DeltaSeconds * 14f);
        if (frame.IsKeyDown(Key.Down) || frame.IsKeyDown(Key.S))
            _shell.NudgeLegalScroll(+frame.DeltaSeconds * 14f);
    }

    public void Dispose()
    {
        _audio?.Dispose();
        _shell.Dispose();
        _profileOverlay.Dispose();
        _ui.Dispose();
        _terrain.Dispose();
    }

    private FrameProfiler? ActiveProfiler()
    {
        if (_profiler is not null)
            return _profiler;
        return _showProfile ? _liveProfiler : null;
    }

    private void HandleProfileHotkeys(SilkFrame frame)
    {
        if (frame.IsKeyPressed(Key.F1))
        {
            _showProfile = !_showProfile;
            if (_showProfile)
                _liveProfiler.Reset();
        }

        var ctrl = frame.IsKeyDown(Key.ControlLeft) || frame.IsKeyDown(Key.ControlRight);
        if (ctrl && frame.IsKeyPressed(Key.C))
        {
            var dump = BuildDebugDump(frame);
            var ok = WindowsClipboardText.TrySet(dump);
            _profileFlash = ok ? "DEBUG DUMP COPIED" : "CLIPBOARD FAILED";
            _profileFlashAge = 0f;
            if (!_showProfile)
            {
                _showProfile = true;
                _liveProfiler.Reset();
            }
        }
    }

    private void WriteProfileReport()
    {
        if (_profiler is null)
            return;
        var text = _profiler.ToReport();
        Console.WriteLine();
        Console.WriteLine("=== ArtillerySimulator profile ===");
        Console.WriteLine(text);
        if (!string.IsNullOrWhiteSpace(_profileJsonPath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_profileJsonPath))!);
            File.WriteAllText(_profileJsonPath, _profiler.ToJson());
            Console.WriteLine($"Wrote {_profileJsonPath}");
        }
    }

    private string BuildProfilePanelText(SilkFrame frame)
    {
        var p = ActiveProfiler() ?? _liveProfiler;
        var sb = new StringBuilder(512);
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"FPS  {_fps.Value:F0}  (inst {p.InstantFps():F0}  avg {p.AverageFps():F0})");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"FRAME  {p.LastFrameMs:F2} ms   samples {p.Frames}");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"GEO  fillTris {_terrain.TerrainTriangleCount}  roads {_terrain.RoadLineCount}  dyn {_wires.AsSpan().Length / 2}");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"VIEW  {frame.Width}x{frame.Height}  pace realtime  follow={(_camera.FollowProjectile ? "shell" : "gun")}");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"GUN  QE {_gun.ElevationMils:F0} mil  DEFL {_gun.DeflectionMils:F0}  {_gun.ChargeLabel} {_gun.MuzzleSpeedMps:F0} m/s");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"SHOT  {_shot.Phase}  t={_shot.TimeSeconds:F2}s  range={PredictedRangeMeters:F0}m");
        if (_mission is { } m)
        {
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"OPFOR  round {m.Round}  alive {m.Remaining}  stopped {m.Stopped}  nearest {m.NearestMeters / 1000f:F2} km  spawns {m.SpawnPointCount}");
        }

        sb.AppendLine(CultureInfo.InvariantCulture,
            $"MET  {_atmosphere.WindSpeedMetersPerSecond:F1} m/s from {_atmosphere.WindFromDegrees:F0}°  ρ {_atmosphere.DensityKgPerM3At(_terrain.GunBaseline.Y):F3}  QNH {_atmosphere.ReferencePressurePa / 100.0:F0}");
        sb.AppendLine(p.ToReport().TrimEnd());
        sb.Append("F1 toggle   Ctrl+C copy dump");
        if (_profileFlashAge >= 0f && _profileFlashAge < 2.5f)
            sb.Append(CultureInfo.InvariantCulture, $"   [{_profileFlash}]");
        return sb.ToString();
    }

    private string BuildDebugDump(SilkFrame frame)
    {
        var sb = new StringBuilder(4096);
        sb.AppendLine("=== ArtillerySimulator debug dump ===");
        sb.AppendLine(CultureInfo.InvariantCulture, $"utc={DateTime.UtcNow:O}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"window={frame.Width}x{frame.Height}  fps={_fps.Value:F1}");
        sb.AppendLine();
        sb.AppendLine((ActiveProfiler() ?? _liveProfiler).ToReport());
        sb.AppendLine("--- gun ---");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"elevation={_gun.ElevationDegrees:F2} deg ({_gun.ElevationMils:F1} mil)  ordered={_crew.OrderedElevation:F2}");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"azimuth={_gun.AzimuthDegrees:F2}  ordered={_crew.OrderedAzimuth:F2}  trail={_gun.CenterAzimuthDegrees:F2}");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"charge={_gun.ChargeLabel}  muzzle={_gun.MuzzleSpeedMps:F0} m/s  breech={(_crew.BreechReady ? "ready" : _crew.Phrase(_gun))}");
        sb.AppendLine("--- shot ---");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"phase={_shot.Phase}  time={_shot.TimeSeconds:F3}s  pos={_shot.CurrentPosition}  vel={_shot.CurrentVelocity}");
        if (_shot.Impact is { } hit)
        {
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"impact range={hit.HorizontalRangeMeters:F1}m  tof={hit.TimeSeconds:F2}s  speed={hit.ImpactSpeedMps:F0}  reason={hit.Reason}");
        }

        sb.AppendLine("--- atmosphere ---");
        sb.AppendLine(_atmosphere.SummaryLine(_terrain.GunBaseline.Y));
        sb.AppendLine(CultureInfo.InvariantCulture, $"wind={_atmosphere.SurfaceWindMetersPerSecond}");
        sb.AppendLine("--- mission ---");
        if (_mission is { } m)
        {
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"round={m.Round}  remaining={m.Remaining}  stopped={m.Stopped}  nearest={m.NearestMeters:F0}m  spawns={m.SpawnPointCount}");
            sb.AppendLine(m.HudLine());
            sb.AppendLine(m.Observation());
            foreach (var t in m.Reports(_terrain.GunBaseline))
            {
                if (!t.Alive)
                    continue;
                sb.AppendLine(CultureInfo.InvariantCulture,
                    $"  atk#{t.Id} {t.Road}  range={t.MetersFromGun:F0}m  toCrossing={t.MetersToCrossing:F0}m  posture={t.Posture}");
            }
        }
        else
        {
            sb.AppendLine("(no mission)");
        }

        sb.AppendLine("--- camera ---");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"follow={(_camera.FollowProjectile ? "shell" : "gun")}  pace=realtime");
        sb.AppendLine("--- terrain ---");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"east={_terrain.EastMeters:F0}  north={_terrain.NorthMeters:F0}  gun={_terrain.GunBaseline}");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"fillTris={_terrain.TerrainTriangleCount}  roadLines={_terrain.RoadLineCount}");
        return sb.ToString();
    }

    private void Simulate(SilkFrame frame)
    {
        if (!_worldReady)
            return;

        // Paused / legal: freeze battle clock, met, crew, and shells; keep camera readable.
        if (IsPaused)
        {
            HandleInput(frame, battleSeconds: 0f);
            return;
        }

        // Lay/input uses wall time so handwheels stay responsive during catch-up.
        var controlSeconds = MathF.Max(1e-4f, frame.DeltaSeconds);
        if (!IsMissionTerminal)
            HandleInput(frame, controlSeconds);

        var steps = _session.Clock.Consume(frame.DeltaSeconds);
        if (steps > 0)
            AdvanceBattleSystems(steps, steps * FixedStepClock.DtSeconds, frame.DeltaSeconds);

        MaybePauseOnMissionEnd();

        if (_mission is { } mission)
            _camera.SetBattleFocus(mission.BattleFocus(_terrain.GunBaseline));
        var camRound = RoundForCamera() ?? _shot;
        if (_camera.FollowProjectile && camRound.Phase == BallisticTrajectoryPhase.InFlight)
            _shot = camRound;
        _camera.Update(frame, _terrain.GunBaseline, camRound, allowMouseLook: !_ui.PointerOverUi);
    }

    private bool TubePreviewStale() =>
        MathF.Abs(_gun.ElevationDegrees - _previewGunElev) > 0.05f
        || MathF.Abs(_gun.AzimuthDegrees - _previewGunAz) > 0.05f
        || _gun.ChargeIndex != _previewCharge;

    private void Render(SilkFrame frame)
    {
        frame.Clear(ClearSky);
        var cam = _camera.Build(frame.DeltaSeconds);
        var aspect = frame.Width / (float)Math.Max(1, frame.Height);
        var view = KristiansandGeoref.CreateMapView(cam.Position, cam.Target, cam.Up);
        var near = 2f;
        var far = MathF.Max(40_000f, Vector3.Distance(cam.Position, cam.Target) * 8f);
        var proj = Matrix4x4.CreatePerspectiveFieldOfView(
            cam.FieldOfViewDegrees * (MathF.PI / 180f),
            aspect,
            near,
            far);
        var mvp = view * proj;

        _solids.Clear();
        DrawAtmosphere(_solids, cam.Position);
        frame.DrawWorldTriangles(_solids.Triangles, mvp);

        var prof = ActiveProfiler();
        if (prof is not null)
            prof.Time("draw.static", () => _terrain.DrawStatic(frame, mvp));
        else
            _terrain.DrawStatic(frame, mvp);

        _wires.Clear();
        void BuildDynamic()
        {
            DrawSkyHaze(_wires, cam.Position);
            _terrain.DrawDynamic(_wires);
            _mission?.Draw(_wires);
            DrawMinRangeRing(_wires);
            DrawAimPreview(_wires);
            _gun.Draw(
                _wires,
                _terrain.GunBaseline,
                showPivotGlow: _crew.BreechReady,
                loading: _crew.Loading);
            DrawMuzzleFlash(_wires);
            DrawShot(_wires);
            _particles.Draw(_wires);
        }

        if (prof is not null)
            prof.Time("draw.dynamic.build", BuildDynamic);
        else
            BuildDynamic();

        if (prof is not null)
            prof.Time("draw.dynamic.upload", () => frame.DrawWorldLines(_wires.AsSpan(), mvp));
        else
            frame.DrawWorldLines(_wires.AsSpan(), mvp);

        frame.DrawScreenGrade(_fxTime, intensity: 0.92f);

        _fps.Update(frame.DeltaSeconds);
        var predicted = PredictedImpact();
        var predictedRange = HorizontalRange(_terrain.GunBaseline, predicted);
        var tof = EstimateTofSeconds(_orderedPreview);

        void DrawHud()
        {
            _ui.Draw(
                frame,
                _gun,
                _crew.OrderedElevation,
                _crew.OrderedAzimuth,
                _terrain,
                _atmosphere,
                _shot,
                _camera.FollowProjectile,
                _fps.Value,
                predictedRange,
                tof,
                predicted,
                _mission is null
                    ? $"{_level.DisplayName} — {_level.SiteName}"
                    : $"{_level.DisplayName.ToUpperInvariant()}  |  {_mission.HudLine()}",
                FireBlockReason(),
                SolutionUnsafe,
                LaunchDetonation,
                _audio?.RadioLine,
                _audio?.RadioSpeaker);
        }

        if (ActiveProfiler() is { } hudProf)
            hudProf.Time("draw.hud", DrawHud);
        else
            DrawHud();

        if (_showProfile)
            _profileOverlay.Draw(frame, BuildProfilePanelText(frame));
    }

    private void RebuildAimPreview()
    {
        var elev = _crew.OrderedElevation;
        var az = _crew.OrderedAzimuth;
        var speed = _gun.MuzzleSpeedMps;
        _orderedPreview = BuildPreview(elev, az, speed, windScale: 1f, coarse: false);
        // Only show a second tube arc when the lay is clearly off the order (avoid twin paths).
        var tubeDiffers = MathF.Abs(_gun.ElevationDegrees - elev) > 1.25f
            || MathF.Abs(_gun.AzimuthDegrees - az) > 1.25f;
        _tubePreview = tubeDiffers
            ? BuildPreview(_gun.ElevationDegrees, _gun.AzimuthDegrees, speed, windScale: 1f, coarse: false)
            : [];

        // Probability envelope from the piece PE budget (expandable via IArtilleryPieceSpec).
        _dispersionPreviews.Clear();
        foreach (var s in _session.Delivery.EnvelopeCorners())
        {
            var arc = BuildPreview(
                elev + s.ElevationDegrees,
                az + s.AzimuthDegrees,
                speed * (1f + s.MuzzleFraction),
                windScale: 1f + s.WindFraction,
                coarse: true);
            if (arc.Count >= 2)
                _dispersionPreviews.Add(arc);
        }

        _previewGunElev = _gun.ElevationDegrees;
        _previewGunAz = _gun.AzimuthDegrees;
        _previewCharge = _gun.ChargeIndex;
        _aimDirty = false;
    }

    private IReadOnlyList<Vector3> BuildPreview(
        float elevation,
        float azimuth,
        float speed,
        float windScale,
        bool coarse)
    {
        var start = _gun.PredictState(_terrain.GunBaseline, elevation, azimuth, speed);
        var dt = coarse ? 1.0 / 20.0 : 1.0 / 30.0;
        var maxPoints = coarse ? 1_200 : 2_400;
        const double maxTimeSeconds = 120.0;
        // Same altitude-resampled met as live AdvanceOne — MPI is not a frozen surface snapshot.
        var points = new List<Vector3>(Math.Min(Math.Max(64, maxPoints), 8_192)) { start.Position };
        var state = start;
        var finished = false;
        var drawCap = Math.Max(8, maxPoints);
        var hardSteps = Math.Max(drawCap * 4, (int)Math.Ceiling(maxTimeSeconds / dt) + 8);
        var terrain = _terrain.Field;

        for (var step = 0; step < hardSteps; step++)
        {
            var prev = state.Position;
            var baseEnv = _atmosphere.BallisticEnvironment(state.Position.Y);
            var env = windScale == 1f
                ? baseEnv
                : new ProjectileBallisticEnvironment(
                    baseEnv.GravityMetersPerSecondSquared,
                    baseEnv.AirDensityKgPerM3,
                    baseEnv.WindMetersPerSecond * windScale,
                    baseEnv.TemperatureKelvin,
                    baseEnv.LatitudeDegrees);
            state = _ballistics.Step(state, dt, env);
            var p = state.Position;

            if (terrain.TrySegmentLeavesRange(prev, p, out var boundaryHit, out _))
            {
                points.Add(terrain.ProjectOntoSurface(boundaryHit));
                finished = true;
                break;
            }

            if (!terrain.IsInside(p.X, p.Z))
            {
                points.Add(terrain.ProjectOntoSurface(p));
                finished = true;
                break;
            }

            if (terrain.TryHeightfieldContact(p, 0.08f))
            {
                points.Add(p);
                finished = true;
                break;
            }

            if (points.Count < drawCap)
                points.Add(p);
            else if (step % 3 == 0)
                points[^1] = p;
        }

        if (!finished && points.Count >= 1)
            points[^1] = terrain.ProjectOntoSurface(state.Position);

        return points;
    }

    private void HandleInput(SilkFrame frame, float battleSeconds)
    {
        if (IsPaused)
            return;

        var actions = _ui.HandleMouse(frame);
        if (actions.ChargeIndex is { } charge)
        {
            _crew.OrderCharge(_gun, charge);
            _aimDirty = true;
        }

        if (actions.Fire || frame.IsKeyPressed(Key.Space))
            TryFire();

        var ctrl = frame.IsKeyDown(Key.ControlLeft) || frame.IsKeyDown(Key.ControlRight);
        if (actions.ToggleFollow || frame.IsKeyPressed(Key.F) || (frame.IsKeyPressed(Key.C) && !ctrl))
            ToggleOptionalShellFollow();

        if (actions.SnapGun)
            _camera.SnapToGun(_terrain.GunBaseline, _gun.BarrelDirection());

        if (actions.SnapBattle && _mission is { } battle)
        {
            _camera.SnapToGun(_terrain.GunBaseline, _gun.BarrelDirection());
            _camera.LookAtBattle(_terrain.GunBaseline, battle.NearestAlive(_terrain.GunBaseline));
        }

        if (actions.MapOverview)
            _camera.ShowMapOverview(_terrain.EastMeters, _terrain.NorthMeters);

        // WASD + arrows lay the tube. Shift = fine. Mouse never aims.
        // Order rates follow the realtime battle clock (same as turret slew).
        var fine = frame.IsKeyDown(Key.ShiftLeft) || frame.IsKeyDown(Key.ShiftRight);
        var laying = frame.IsKeyDown(Key.W) || frame.IsKeyDown(Key.Up)
            || frame.IsKeyDown(Key.S) || frame.IsKeyDown(Key.Down)
            || frame.IsKeyDown(Key.A) || frame.IsKeyDown(Key.Left)
            || frame.IsKeyDown(Key.D) || frame.IsKeyDown(Key.Right);

        if (laying)
            _layHoldSeconds += MathF.Max(1e-4f, frame.DeltaSeconds);
        else
            _layHoldSeconds = 0f;

        // Soft take-up on wall clock so taps stay small; full rate after LayRampSeconds.
        var ramp = Math.Clamp(_layHoldSeconds / LayRampSeconds, 0.2f, 1f);
        var elevate = (fine ? FineMilsPerSecond : CrewDrill.ElevateMilsPerSecond) / NatoMils.MilsPerDegree * battleSeconds * ramp;
        var traverse = (fine ? FineMilsPerSecond : CrewDrill.TraverseMilsPerSecond) / NatoMils.MilsPerDegree * battleSeconds * ramp;

        var dElev = 0f;
        var dTrav = 0f;
        if (frame.IsKeyDown(Key.W) || frame.IsKeyDown(Key.Up))
            dElev += elevate;
        if (frame.IsKeyDown(Key.S) || frame.IsKeyDown(Key.Down))
            dElev -= elevate;
        // A / Left = turn left (CCW / +azimuth). D / Right = turn right.
        if (frame.IsKeyDown(Key.A) || frame.IsKeyDown(Key.Left))
            dTrav += traverse;
        if (frame.IsKeyDown(Key.D) || frame.IsKeyDown(Key.Right))
            dTrav -= traverse;

        if (dElev != 0f || dTrav != 0f)
        {
            _crew.NudgeOrder(_gun, dElev, dTrav);
            _aimDirty = true;
        }
    }

    /// <summary>
    /// Optional killcam: only meaningful while a round is in flight (or the short post-impact hold).
    /// F toggles shell follow; after impact + 3s the camera returns to the gun.
    /// </summary>
    private void ToggleOptionalShellFollow()
    {
        var round = RoundForCamera();
        var roundLive = round is not null
            && (round.Phase is BallisticTrajectoryPhase.InFlight
                || (round.Phase == BallisticTrajectoryPhase.Impacted
                    && _burstAge >= 0f
                    && _burstAge < KillcamReturnSeconds));

        if (!roundLive)
        {
            // No live round — stay on the gun.
            _camera.FollowProjectile = false;
            _killcamActive = false;
            return;
        }

        _shot = round!;
        _camera.ToggleFollowTarget();
        _killcamActive = _camera.FollowProjectile;
        if (!_killcamActive)
            _camera.SnapToGun(_terrain.GunBaseline, _gun.BarrelDirection());
    }

    private BallisticTrajectoryRunner? RoundForCamera()
    {
        // Prefer any live flyer (newest first) so shell-cam works beyond the first round.
        for (var i = _rounds.Count - 1; i >= 0; i--)
        {
            if (_rounds[i].Phase == BallisticTrajectoryPhase.InFlight)
                return _rounds[i];
        }

        // Short post-impact hold for killcam — only the most recent impact still in the window.
        if (_burstAge >= 0f && _burstAge < KillcamReturnSeconds)
        {
            for (var i = _rounds.Count - 1; i >= 0; i--)
            {
                if (_rounds[i].Phase == BallisticTrajectoryPhase.Impacted)
                    return _rounds[i];
            }
        }

        return null;
    }

    private Vector3 PredictedImpact() =>
        _orderedPreview.Count == 0 ? _terrain.GunBaseline : _orderedPreview[^1];

    private static float HorizontalRange(Vector3 origin, Vector3 point)
    {
        var dx = point.X - origin.X;
        var dz = point.Z - origin.Z;
        return MathF.Sqrt(dx * dx + dz * dz);
    }

    private static float EstimateTofSeconds(IReadOnlyList<Vector3> preview)
    {
        if (preview.Count < 2)
            return 0f;
        // Ordered preview uses 1/30 s steps (see BuildPreview).
        return (preview.Count - 1) / 30f;
    }

    private void DrawAtmosphere(WorldSolidBatch solids, Vector3 camera)
    {
        var center = new Vector3(camera.X, MathF.Min(camera.Y, _terrain.GunBaseline.Y + 40f), camera.Z);
        solids.SkyDome(center, radius: 28_000f, SkyZenith, SkyHorizon, slices: 28, stacks: 10);
    }

    private void DrawSkyHaze(SilkWireBatch wires, Vector3 camera)
    {
        // Soft volumetric feel without a visible wireframe sun disc.
        var light = new Vector3(
            _terrain.ExtentMeters * 0.55f,
            6800f,
            _terrain.GunBaseline.Z - 4000f);
        var toLight = Vector3.Normalize(light - camera);
        var origin = camera + toLight * 2200f;
        for (var i = 0; i < 10; i++)
        {
            var t = i / 9f;
            var lateral = Vector3.Normalize(Vector3.Cross(toLight, Vector3.UnitY));
            if (lateral.LengthSquared() < 1e-4f)
                lateral = Vector3.UnitZ;
            var offset = lateral * ((t - 0.5f) * 900f) + Vector3.UnitY * ((t - 0.5f) * 120f);
            var a = origin + offset;
            var b = a + toLight * (4800f + t * 2200f);
            var alpha = (int)(28 + 22 * (1f - MathF.Abs(t - 0.5f) * 2f));
            wires.Line(a, b, Color.FromArgb(alpha, 255, 200, 140));
        }

        // Horizon bloom rings
        var gun = _terrain.GunBaseline;
        for (var r = 2; r <= 5; r++)
        {
            var radius = r * 1800f;
            var alpha = 18 + r * 4;
            wires.Ring(
                new Vector3(gun.X, gun.Y + 40f + r * 12f, gun.Z),
                radius,
                Color.FromArgb(alpha, 180, 150, 110),
                segments: 40,
                yLift: 0f);
        }
    }

    private void DrawMuzzleFlash(SilkWireBatch wires)
    {
        if (_muzzleFlash <= 0f)
            return;

        var muzzle = _gun.MuzzlePosition(_terrain.GunBaseline);
        var radius = 1.4f + (0.22f - _muzzleFlash) * 18f;
        wires.WireSphere(muzzle, radius, FlashColor, segments: 10);
    }

    private void DrawMinRangeRing(SilkWireBatch wires)
    {
        var gun = _terrain.GunBaseline;
        wires.Ring(gun, _gun.Spec.MinEffectiveRangeMeters, BallisticArcColors.MinRange);
    }

    private void DrawAimPreview(SilkWireBatch wires)
    {
        var unsafeSol = SolutionUnsafe;
        var prob = unsafeSol ? BallisticArcColors.ProbabilityUnsafe : BallisticArcColors.Probability;
        var ordered = unsafeSol ? BallisticArcColors.OrderedUnsafe : BallisticArcColors.Ordered;
        var mpiColor = unsafeSol ? BallisticArcColors.MpiMarkUnsafe : BallisticArcColors.MpiMark;

        // One aimed solution path only — PE is a ground footprint, not twin arcs.
        DrawProbabilityFootprint(wires, unsafeSol);

        if (_tubePreview.Count >= 2)
            DrawArc(wires, _tubePreview, BallisticArcColors.Tube, maxSegments: 160);

        if (_orderedPreview.Count >= 2)
        {
            DrawArc(wires, _orderedPreview, ordered, maxSegments: 280);
            var mpi = _orderedPreview[^1];
            wires.WireSphere(mpi, 28f, mpiColor, segments: 10);
            wires.Ring(mpi, 40f, mpiColor);
            var ground = _terrain.Field.SampleHeight(mpi.X, mpi.Z);
            wires.Line(mpi, new Vector3(mpi.X, ground + 1f, mpi.Z), mpiColor);
        }
    }

    private void DrawProbabilityFootprint(SilkWireBatch wires, bool unsafeSol)
    {
        if (_dispersionPreviews.Count < 2 || _orderedPreview.Count < 2)
            return;

        var footprint = unsafeSol
            ? BallisticArcColors.ProbabilityFootprintUnsafe
            : BallisticArcColors.ProbabilityFootprint;

        var impacts = new List<Vector3>(_dispersionPreviews.Count + 1);
        foreach (var arc in _dispersionPreviews)
            impacts.Add(arc[^1]);
        impacts.Add(_orderedPreview[^1]);

        var cx = 0f;
        var cz = 0f;
        foreach (var p in impacts)
        {
            cx += p.X;
            cz += p.Z;
        }

        cx /= impacts.Count;
        cz /= impacts.Count;
        var radius = 0f;
        foreach (var p in impacts)
        {
            var dx = p.X - cx;
            var dz = p.Z - cz;
            radius = MathF.Max(radius, MathF.Sqrt(dx * dx + dz * dz));
        }

        radius = Math.Clamp(radius, 40f, 900f);
        var y = _terrain.Field.SampleHeight(cx, cz) + 2.5f;
        wires.Ring(new Vector3(cx, y, cz), radius, footprint, segments: 36, yLift: 0f);
    }

    private static void DrawArc(SilkWireBatch wires, IReadOnlyList<Vector3> points, Color color, int maxSegments = 180)
    {
        var stride = Math.Max(1, points.Count / Math.Max(8, maxSegments));
        for (var i = stride; i < points.Count; i += stride)
            wires.Line(points[i - stride], points[i], color);

        var last = points.Count - 1;
        var drawn = last - (last % stride);
        if (drawn != last && drawn >= 0)
            wires.Line(points[drawn], points[last], color);
    }

    private void DrawShot(SilkWireBatch wires)
    {
        foreach (var round in _rounds)
        {
            if (round.Phase is not (BallisticTrajectoryPhase.InFlight or BallisticTrajectoryPhase.Impacted))
                continue;

            var trail = round.Trail;
            for (var i = 1; i < trail.Count; i++)
            {
                var heat = i / (float)trail.Count;
                var color = Color.FromArgb(
                    (int)(TrailCool.A + (TrailHot.A - TrailCool.A) * heat),
                    (int)(TrailCool.R + (TrailHot.R - TrailCool.R) * heat),
                    (int)(TrailCool.G + (TrailHot.G - TrailCool.G) * heat),
                    (int)(TrailCool.B + (TrailHot.B - TrailCool.B) * heat));
                wires.Line(trail[i - 1], trail[i], color);
            }

            if (round.Phase != BallisticTrajectoryPhase.InFlight)
                continue;

            var span = HorizontalRange(_terrain.GunBaseline, round.CurrentPosition);
            var radius = Math.Clamp(span * 0.012f, 14f, 70f);
            wires.WireSphere(round.CurrentPosition, radius * 0.45f, TrailHot, segments: 10);
            wires.WireSphere(round.CurrentPosition, radius, FlashColor, segments: 10);
            var ground = _terrain.Field.SampleHeight(round.CurrentPosition.X, round.CurrentPosition.Z);
            var drop = new Vector3(round.CurrentPosition.X, ground + 2f, round.CurrentPosition.Z);
            wires.Line(round.CurrentPosition, drop, Color.FromArgb(160, TrailHot.R, TrailHot.G, TrailHot.B));
        }

        if (_burstAge >= 0f)
            DrawBurst(wires);
    }

    private Vector3 BurstPoint(Vector3 impact)
    {
        var warhead = _gun.Spec.Warhead;
        var ground = _terrain.Field.SampleHeight(impact.X, impact.Z);
        var height = warhead.IsAirburst ? warhead.AirburstHeightMeters : MathF.Max(2f, impact.Y - ground);
        return new Vector3(impact.X, ground + height, impact.Z);
    }

    private void DrawBurst(SilkWireBatch wires)
    {
        var age = MathF.Min(_burstAge, 8f);
        var fade = 1f - age / 8f;
        var alpha = (int)(220 * Math.Clamp(fade, 0.35f, 1f));
        var color = Color.FromArgb(alpha, BallisticArcColors.Impact.R, BallisticArcColors.Impact.G, BallisticArcColors.Impact.B);
        var pale = Color.FromArgb(alpha / 2, BallisticArcColors.Impact.R, BallisticArcColors.Impact.G, BallisticArcColors.Impact.B);
        var column = 40f + age * 30f;
        var ground = new Vector3(_burstAt.X, _terrain.Field.SampleHeight(_burstAt.X, _burstAt.Z), _burstAt.Z);
        wires.Line(ground, _burstAt + Vector3.UnitY * column, color);
        wires.WireSphere(_burstAt, 22f + age * 8f, color, segments: 12);
        wires.WireSphere(_burstAt, 10f + age * 3f, pale, segments: 10);
        var caught = _mission?.CaughtMeters ?? _gun.Spec.Warhead.CaughtMeters;
        var pinned = _mission?.PinnedMeters ?? _gun.Spec.Warhead.PinnedMeters;
        wires.Ring(ground + Vector3.UnitY * 2f, caught, color);
        wires.Ring(ground + Vector3.UnitY * 2f, pinned, pale);
    }
}
