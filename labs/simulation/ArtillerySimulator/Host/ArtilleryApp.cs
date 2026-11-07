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
    /// <summary>Battle-time multipliers vs wall clock (1× / 3× / 8× / 24×). Independent of FPS.</summary>
    private static readonly float[] PaceMultipliers = [1f, 3f, 8f, 24f];
    private const float PhysicsHz = 120f;
    private const float MilsPerDegree = 6400f / 360f;
    private const float FineMilsPerSecond = 22f;
    private const float KillcamReturnSeconds = 3f;
    /// <summary>Educational laying / MV / met uncertainty for the probability envelope (not a classified table).</summary>
    private const float DispersionElevMils = 10f;
    private const float DispersionDeflMils = 14f;
    private const float DispersionMuzzleFrac = 0.018f;
    private const float DispersionWindFrac = 0.35f;

    private static readonly Color TrailHot = BallisticArcColors.Flight;
    private static readonly Color TrailCool = Color.FromArgb(90, 180, 140, 70);
    private static readonly Color FlashColor = Color.FromArgb(255, 255, 214, 150);
    private static readonly Color SunColor = Color.FromArgb(120, 255, 186, 110);

    private readonly TerrainWorld _terrain = new();
    private readonly GunModel _gun = new();
    private readonly BallisticTrajectoryRunner _shot = new(new BallisticTrajectoryRunnerOptions
    {
        Step = new ProjectileTerrainStepOptions
        {
            DtSeconds = 1.0 / 120.0,
            ProjectileRadius = GunModel.MuzzleRadius,
        },
    });
    private readonly ProjectileBallisticSimulation _ballistics = new(GunModel.Educational155Profile, DragCoefficientLaw.BoatTail);
    private readonly ArtilleryCameras _camera = new();
    private readonly AtmosphereModel _atmosphere = AtmosphereModel.CreateRegionalDefault();
    private readonly SilkWireBatch _wires = new();
    private readonly FireControlUi _ui = new();
    private readonly ProfileOverlay _profileOverlay = new();
    private readonly FrameProfiler _liveProfiler = new(warmupFrames: 5);

    private readonly SmoothedFps _fps = new();
    private readonly CrewDrill _crew = new();
    private int _paceIndex = 1;
    private KristiansandDefenseMission? _mission;
    private IReadOnlyList<Vector3> _orderedPreview = [];
    private IReadOnlyList<Vector3> _tubePreview = [];
    private readonly List<IReadOnlyList<Vector3>> _dispersionPreviews = [];
    private bool _aimDirty = true;
    private float _muzzleFlash;
    private float _burstAge = -1f;
    private Vector3 _burstAt;
    private bool _killcamActive;
    private readonly ArtilleryLiveHost _agent;
    private AgentSurface? _surface;
    private FrameProfiler? _profiler;
    private int _profileFramesRemaining = -1;
    private string? _profileJsonPath;
    private bool _showProfile;
    private string _profileFlash = "";
    private float _profileFlashAge = -1f;

    public ArtilleryApp()
    {
        _agent = new ArtilleryLiveHost(this);
    }

    public void EnableProfile(int frames, string? jsonPath = null)
    {
        _profiler = new FrameProfiler(warmupFrames: Math.Min(20, Math.Max(0, frames / 6)));
        _profileFramesRemaining = Math.Max(1, frames);
        _profileJsonPath = jsonPath;
    }

    public FrameProfiler? Profiler => _profiler;

    internal GunModel Gun => _gun;
    internal TerrainWorld Terrain => _terrain;
    internal BallisticTrajectoryRunner Shot => _shot;
    internal CameraMode CameraMode => CameraMode.Orbit;
    internal string PaceText => PaceLabel();
    internal KristiansandDefenseMission? Mission => _mission;
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

    internal void SetPace(int index) => _paceIndex = Math.Clamp(index, 0, PaceMultipliers.Length - 1);

    internal void SetFollowCamera(bool followShell) => _camera.FollowProjectile = followShell;

    internal string? FireBlockReason()
    {
        if (_shot.Phase == BallisticTrajectoryPhase.InFlight)
            return "A shot is already in flight.";
        if (!_crew.BreechReady)
            return _crew.Phrase(_gun);
        return null;
    }

    internal bool TryFire()
    {
        if (FireBlockReason() is not null)
            return false;

        // Fire the ordered lay the instant the breech is closed — do not wait out the slew.
        _crew.SnapTube(_gun);
        var muzzle = _gun.MuzzlePosition(_terrain.GunBaseline);
        _shot.Begin(_gun.CreateProjectileState(muzzle, _gun.BarrelDirection()));
        _muzzleFlash = 0.22f;
        _burstAge = -1f;
        _crew.BeginReload();
        // Stay on the gun; press F while the round is in flight for optional killcam.
        _camera.FollowProjectile = false;
        _killcamActive = false;
        _aimDirty = true;
        return true;
    }

    public void Initialize(SilkFrame frame)
    {
        _terrain.Rebuild();
        _mission = new KristiansandDefenseMission(_terrain);
        _terrain.PlaceGun(_mission.GunX, _mission.GunZ);
        _gun.LayCenter(_mission.TrailBearingDegrees);
        _gun.SetCharge(_mission.OpeningChargeIndex);
        _gun.SetElevation(_mission.OpeningElevationDegrees);
        _gun.NudgeAzimuth(_mission.OpeningAzimuthDegrees - _mission.TrailBearingDegrees);
        _crew.SeatOpening(_gun);
        _shot.Reset();
        _camera.SnapToGun(_terrain.GunBaseline, _gun.BarrelDirection());
        RebuildAimPreview();
        _surface = AgentSurface.AttachAll(
            _agent,
            AgentSurfaceDefinition.From<IArtillerySurface>(),
            new AgentAttachOptions { EnableStdio = false, EnableMcpStdio = false });
        _ = _surface;
        _ = frame;
    }

    public void Update(SilkFrame frame)
    {
        HandleProfileHotkeys(frame);

        var active = ActiveProfiler();
        active?.BeginFrame();
        if (active is not null)
        {
            active.Time("simulate", () => _agent.RunFrame(() => Simulate(frame)));
            active.Time("render", () => Render(frame));
            active.SetGeometry(
                _terrain.TerrainLineCount,
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

        if (_profileFlashAge >= 0f)
            _profileFlashAge += frame.DeltaSeconds;
    }

    public void Dispose()
    {
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
            $"GEO  terrain {_terrain.TerrainLineCount}  roads {_terrain.RoadLineCount}  dyn {_wires.AsSpan().Length / 2}");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"VIEW  {frame.Width}x{frame.Height}  pace {PaceLabel()}  follow={(_camera.FollowProjectile ? "shell" : "gun")}");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"GUN  QE {_gun.ElevationMils:F0} mil  DEFL {_gun.DeflectionMils:F0}  {_gun.ChargeLabel} {_gun.MuzzleSpeedMps:F0} m/s");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"SHOT  {_shot.Phase}  t={_shot.TimeSeconds:F2}s  range={PredictedRangeMeters:F0}m");
        if (_mission is { } m)
        {
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"OPFOR  round {m.Round}  alive {m.Remaining}  stopped {m.Stopped}  nearest {m.NearestMeters / 1000f:F2} km  spawns {m.SpawnPointCount}");
        }

        var wind = _atmosphere.SurfaceWindMetersPerSecond;
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"MET  wind {wind.Length():F1} m/s  rho {_atmosphere.DensityKgPerM3At(_terrain.GunBaseline.Y):F3}");
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
            $"follow={(_camera.FollowProjectile ? "shell" : "gun")}  pace={PaceLabel()}");
        sb.AppendLine("--- terrain ---");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"east={_terrain.EastMeters:F0}  north={_terrain.NorthMeters:F0}  gun={_terrain.GunBaseline}");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"terrainLines={_terrain.TerrainLineCount}  roadLines={_terrain.RoadLineCount}");
        return sb.ToString();
    }

    private void Simulate(SilkFrame frame)
    {
        // Wall-clock × pace — aim/crew/shell stay correct even when FPS dips.
        var battleSeconds = PaceMultipliers[_paceIndex] * MathF.Max(1e-4f, frame.DeltaSeconds);
        // Advance the crew before fire input so Space/FIRE works on the same frame the breech closes.
        _crew.Advance(_gun, battleSeconds);
        HandleInput(frame, battleSeconds);
        if (_shot.Phase == BallisticTrajectoryPhase.InFlight)
        {
            var steps = Math.Clamp((int)MathF.Round(battleSeconds * PhysicsHz), 1, 512);
            for (var i = 0; i < steps && _shot.Phase == BallisticTrajectoryPhase.InFlight; i++)
            {
                var env = _atmosphere.BallisticEnvironment(_shot.CurrentPosition.Y);
                _shot.AdvanceOne(_ballistics, env, _terrain.Collision, _terrain.Field, _terrain.GunBaseline);
            }
        }

        Vector3? incoming = _shot.Phase == BallisticTrajectoryPhase.InFlight ? _shot.CurrentPosition : null;
        _mission?.Advance(battleSeconds, incoming);

        if (_shot.Phase == BallisticTrajectoryPhase.Impacted && _burstAge < 0f && _shot.Impact is { } landed)
        {
            _burstAge = 0f;
            _burstAt = landed.Position;
            _mission?.ResolveImpact(landed.Position);
        }

        if (_burstAge >= 0f)
        {
            _burstAge += frame.DeltaSeconds;
            if (_killcamActive && _burstAge >= KillcamReturnSeconds)
            {
                _killcamActive = false;
                _camera.FollowProjectile = false;
                _camera.SnapToGun(_terrain.GunBaseline, _gun.BarrelDirection());
            }
        }

        if (_muzzleFlash > 0f)
            _muzzleFlash = MathF.Max(0f, _muzzleFlash - frame.DeltaSeconds);

        if (_aimDirty || !_crew.Ready(_gun))
            RebuildAimPreview();

        if (_mission is { } mission)
            _camera.SetBattleFocus(mission.BattleFocus(_terrain.GunBaseline));
        _camera.Update(frame, _terrain.GunBaseline, _shot, allowMouseLook: !_ui.PointerOverUi);
    }

    private void Render(SilkFrame frame)
    {
        frame.Clear(Rgba32.Black);
        var cam = _camera.Build(frame.DeltaSeconds);
        var aspect = frame.Width / (float)Math.Max(1, frame.Height);
        var view = _camera.MatchKommunekartOrientation
            ? KristiansandGeoref.CreateCesiumMatchedView(cam.Position, cam.Target, cam.Up)
            : KristiansandGeoref.CreateMapView(cam.Position, cam.Target, cam.Up);
        var near = 2f;
        var far = MathF.Max(40_000f, Vector3.Distance(cam.Position, cam.Target) * 8f);
        var proj = Matrix4x4.CreatePerspectiveFieldOfView(
            cam.FieldOfViewDegrees * (MathF.PI / 180f),
            aspect,
            near,
            far);
        var mvp = view * proj;

        var prof = ActiveProfiler();
        if (prof is not null)
            prof.Time("draw.static", () => _terrain.DrawStatic(frame, mvp));
        else
            _terrain.DrawStatic(frame, mvp);

        _wires.Clear();
        void BuildDynamic()
        {
            DrawSky(_wires);
            _terrain.DrawDynamic(_wires);
            _mission?.Draw(_wires);
            DrawMinRangeRing(_wires);
            DrawAimPreview(_wires);
            _gun.Draw(
                _wires,
                _terrain.GunBaseline,
                showPivotGlow: _shot.Phase != BallisticTrajectoryPhase.InFlight && _crew.BreechReady,
                loading: _crew.Loading);
            DrawMuzzleFlash(_wires);
            DrawShot(_wires);
        }

        if (prof is not null)
            prof.Time("draw.dynamic.build", BuildDynamic);
        else
            BuildDynamic();

        if (prof is not null)
            prof.Time("draw.dynamic.upload", () => frame.DrawWorldLines(_wires.AsSpan(), mvp));
        else
            frame.DrawWorldLines(_wires.AsSpan(), mvp);

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
                _paceIndex,
                PaceLabel(),
                _mission?.HudLine(),
                _crew.BreechReady ? null : _crew.Phrase(_gun));
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
        var tubeDiffers = MathF.Abs(_gun.ElevationDegrees - elev) > 0.05f
            || MathF.Abs(_gun.AzimuthDegrees - az) > 0.05f;
        _tubePreview = tubeDiffers
            ? BuildPreview(_gun.ElevationDegrees, _gun.AzimuthDegrees, speed, windScale: 1f, coarse: false)
            : [];

        // Physics probability envelope: lay mils, muzzle scatter, wind shear — translucent sibling arcs.
        _dispersionPreviews.Clear();
        var dElev = DispersionElevMils / MilsPerDegree;
        var dAz = DispersionDeflMils / MilsPerDegree;
        ReadOnlySpan<(float De, float Da, float Mv, float Wind)> samples =
        [
            (dElev, 0f, 0f, 0f),
            (-dElev, 0f, 0f, 0f),
            (0f, dAz, 0f, 0f),
            (0f, -dAz, 0f, 0f),
            (dElev * 0.7f, dAz * 0.7f, DispersionMuzzleFrac, DispersionWindFrac),
            (-dElev * 0.7f, -dAz * 0.7f, -DispersionMuzzleFrac, -DispersionWindFrac),
            (dElev * 0.5f, -dAz * 0.5f, DispersionMuzzleFrac, -DispersionWindFrac),
            (-dElev * 0.5f, dAz * 0.5f, -DispersionMuzzleFrac, DispersionWindFrac),
        ];
        foreach (var s in samples)
        {
            var arc = BuildPreview(
                elev + s.De,
                az + s.Da,
                speed * (1f + s.Mv),
                windScale: 1f + s.Wind,
                coarse: true);
            if (arc.Count >= 2)
                _dispersionPreviews.Add(arc);
        }

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
        var baseEnv = _atmosphere.BallisticEnvironment(start.Position.Y);
        var env = windScale == 1f
            ? baseEnv
            : new ProjectileBallisticEnvironment(
                baseEnv.GravityMetersPerSecondSquared,
                baseEnv.AirDensityKgPerM3,
                baseEnv.WindMetersPerSecond * windScale,
                baseEnv.TemperatureKelvin,
                baseEnv.LatitudeDegrees);
        return BallisticTrajectoryRunner.BuildPreview(
            _ballistics,
            env,
            _terrain.Field,
            start,
            dtSeconds: coarse ? 1.0 / 30.0 : 1.0 / 60.0,
            maxTimeSeconds: 90.0,
            maxPoints: coarse ? 1_800 : 6_000);
    }

    private void HandleInput(SilkFrame frame, float battleSeconds)
    {
        var actions = _ui.HandleMouse(frame);
        if (actions.ChargeIndex is { } charge)
        {
            _crew.OrderCharge(_gun, charge);
            _aimDirty = true;
        }

        if (actions.PaceIndex is { } pace)
            _paceIndex = pace;

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
        var fine = frame.IsKeyDown(Key.ShiftLeft) || frame.IsKeyDown(Key.ShiftRight);
        var elevate = (fine ? FineMilsPerSecond : CrewDrill.ElevateMilsPerSecond) / MilsPerDegree * battleSeconds;
        var traverse = (fine ? FineMilsPerSecond : CrewDrill.TraverseMilsPerSecond) / MilsPerDegree * battleSeconds;

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
        var roundLive = _shot.Phase is BallisticTrajectoryPhase.InFlight
            || (_shot.Phase == BallisticTrajectoryPhase.Impacted && _burstAge >= 0f && _burstAge < KillcamReturnSeconds);

        if (!roundLive)
        {
            // No live round — stay on the gun.
            _camera.FollowProjectile = false;
            _killcamActive = false;
            return;
        }

        _camera.ToggleFollowTarget();
        _killcamActive = _camera.FollowProjectile;
        if (!_killcamActive)
            _camera.SnapToGun(_terrain.GunBaseline, _gun.BarrelDirection());
    }

    private Vector3 PredictedImpact() =>
        _orderedPreview.Count == 0 ? _terrain.GunBaseline : _orderedPreview[^1];

    private string PaceLabel() => _paceIndex switch
    {
        0 => "pace 1x",
        1 => "pace 3x",
        2 => "pace 8x",
        _ => "pace 24x",
    };

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
        // Preview built at 1/60 s steps.
        return (preview.Count - 1) / 60f;
    }

    private void DrawSky(SilkWireBatch wires)
    {
        var sun = new Vector3(_terrain.ExtentMeters * 0.55f, 6800f, _terrain.GunBaseline.Z - 4000f);
        wires.WireSphere(sun, 900f, SunColor, segments: 12);
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
        wires.Ring(gun, SimulationUnits.MinEffectiveRangeMeters, BallisticArcColors.MinRange);
    }

    private void DrawAimPreview(SilkWireBatch wires)
    {
        // Keep the aimed (cyan) solution visible while a round is in flight so the gunner can keep laying.
        foreach (var arc in _dispersionPreviews)
            DrawArc(wires, arc, BallisticArcColors.Probability, maxSegments: 90);

        DrawProbabilityEnvelope(wires);

        if (_tubePreview.Count >= 2)
            DrawArc(wires, _tubePreview, BallisticArcColors.Tube, maxSegments: 160);

        if (_orderedPreview.Count >= 2)
        {
            // Aimed trajectory — bright cyan, denser samples so it reads as the fire solution.
            DrawArc(wires, _orderedPreview, BallisticArcColors.Ordered, maxSegments: 280);
            var mpi = _orderedPreview[^1];
            wires.WireSphere(mpi, 28f, BallisticArcColors.MpiMark, segments: 10);
            wires.Ring(mpi, 40f, BallisticArcColors.MpiMark);
            var ground = _terrain.Field.SampleHeight(mpi.X, mpi.Z);
            wires.Line(mpi, new Vector3(mpi.X, ground + 1f, mpi.Z), BallisticArcColors.MpiMark);
        }
    }

    private void DrawProbabilityEnvelope(SilkWireBatch wires)
    {
        if (_dispersionPreviews.Count < 2 || _orderedPreview.Count < 2)
            return;

        // Connect impacts into a ground footprint ring (CEP-ish ellipse proxy).
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
        wires.Ring(new Vector3(cx, y, cz), radius, BallisticArcColors.ProbabilityFootprint, segments: 36, yLift: 0f);

        // Pair left/right-ish samples into translucent loft bands along the arc.
        for (var a = 0; a + 1 < _dispersionPreviews.Count; a += 2)
        {
            var left = _dispersionPreviews[a];
            var right = _dispersionPreviews[a + 1];
            var n = Math.Min(left.Count, right.Count);
            var step = Math.Max(1, n / 48);
            for (var i = 0; i < n; i += step)
                wires.Line(left[i], right[i], BallisticArcColors.Probability);
        }
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
        var trail = _shot.Trail;
        var spanNow = HorizontalRange(_terrain.GunBaseline, _shot.CurrentPosition);
        var ribbon = Math.Clamp(spanNow * 0.0035f, 1.5f, 22f);
        for (var i = 1; i < trail.Count; i++)
        {
            var heat = i / (float)trail.Count;
            var color = Color.FromArgb(
                (int)(TrailCool.A + (TrailHot.A - TrailCool.A) * heat),
                (int)(TrailCool.R + (TrailHot.R - TrailCool.R) * heat),
                (int)(TrailCool.G + (TrailHot.G - TrailCool.G) * heat),
                (int)(TrailCool.B + (TrailHot.B - TrailCool.B) * heat));
            var lift = Vector3.UnitY * (ribbon * heat);
            wires.Line(trail[i - 1], trail[i], color);
            wires.Line(trail[i - 1] + lift, trail[i] + lift, color);
        }

        if (_shot.Phase == BallisticTrajectoryPhase.InFlight)
        {
            var span = HorizontalRange(_terrain.GunBaseline, _shot.CurrentPosition);
            var radius = Math.Clamp(span * 0.012f, 14f, 70f);
            wires.WireSphere(_shot.CurrentPosition, radius * 0.45f, TrailHot, segments: 10);
            wires.WireSphere(_shot.CurrentPosition, radius, FlashColor, segments: 10);
            var ground = _terrain.Field.SampleHeight(_shot.CurrentPosition.X, _shot.CurrentPosition.Z);
            var drop = new Vector3(_shot.CurrentPosition.X, ground + 2f, _shot.CurrentPosition.Z);
            wires.Line(_shot.CurrentPosition, drop, Color.FromArgb(160, TrailHot.R, TrailHot.G, TrailHot.B));
        }

        if (_burstAge >= 0f)
            DrawBurst(wires);
    }

    private void DrawBurst(SilkWireBatch wires)
    {
        var age = MathF.Min(_burstAge, 8f);
        var fade = 1f - age / 8f;
        var alpha = (int)(220 * Math.Clamp(fade, 0.35f, 1f));
        var color = Color.FromArgb(alpha, BallisticArcColors.Impact.R, BallisticArcColors.Impact.G, BallisticArcColors.Impact.B);
        var pale = Color.FromArgb(alpha / 2, BallisticArcColors.Impact.R, BallisticArcColors.Impact.G, BallisticArcColors.Impact.B);
        var column = 40f + age * 30f;
        wires.Line(_burstAt, _burstAt + Vector3.UnitY * column, color);
        wires.WireSphere(_burstAt + Vector3.UnitY * (10f + age * 4f), 18f + age * 6f, color, segments: 12);
        wires.Ring(_burstAt, KristiansandDefenseMission.CaughtMeters, color);
        wires.Ring(_burstAt, KristiansandDefenseMission.PinnedMeters, pale);
    }
}
