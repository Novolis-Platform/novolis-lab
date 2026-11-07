using System.Drawing;
using System.Numerics;
using Novolis.Agent.Surface;
using Novolis.Math.Geometry;
using Novolis.Physics.Ballistics;
using Novolis.Silk;

namespace ArtillerySimulator;

internal sealed class ArtilleryApp : IDisposable
{
    private static readonly int[] PaceSteps = [2, 6, 16, 48];
    private const float MilsPerDegree = 6400f / 360f;
    private const float FineMilsPerSecond = 28f;

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

    private readonly SmoothedFps _fps = new();
    private readonly CrewDrill _crew = new();
    private int _paceIndex = 1;
    private KristiansandDefenseMission? _mission;
    private IReadOnlyList<Vector3> _orderedPreview = [];
    private IReadOnlyList<Vector3> _tubePreview = [];
    private bool _aimDirty = true;
    private float _muzzleFlash;
    private float _burstAge = -1f;
    private Vector3 _burstAt;
    private readonly ArtilleryLiveHost _agent;
    private AgentSurface? _surface;
    private FrameProfiler? _profiler;
    private int _profileFramesRemaining = -1;
    private string? _profileJsonPath;

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

    internal void SetPace(int index) => _paceIndex = Math.Clamp(index, 0, PaceSteps.Length - 1);

    internal void SetFollowCamera(bool followShell) => _camera.FollowProjectile = followShell;

    internal string? FireBlockReason()
    {
        if (_shot.Phase == BallisticTrajectoryPhase.InFlight)
            return "A shot is already in flight.";
        if (!_crew.Ready(_gun))
            return _crew.Phrase(_gun);
        return null;
    }

    internal bool TryFire()
    {
        if (FireBlockReason() is not null)
            return false;

        var muzzle = _gun.MuzzlePosition(_terrain.GunBaseline);
        _shot.Begin(_gun.CreateProjectileState(muzzle, _gun.BarrelDirection()));
        _muzzleFlash = 0.22f;
        _burstAge = -1f;
        _crew.BeginReload();
        _camera.FollowProjectile = true;
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
        _profiler?.BeginFrame();
        if (_profiler is not null)
        {
            _profiler.Time("simulate", () => _agent.RunFrame(() => Simulate(frame)));
            _profiler.Time("render", () => Render(frame));
            _profiler.SetGeometry(
                _terrain.TerrainLineCount,
                _terrain.RoadLineCount,
                _wires.AsSpan().Length / 2,
                FireControlUi.LastOverlayLineCount);
            _profiler.EndFrame();
            if (_profileFramesRemaining > 0)
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
    }

    public void Dispose() => _terrain.Dispose();

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

    private void Simulate(SilkFrame frame)
    {
        var battleSeconds = PaceSteps[_paceIndex] * (1f / 120f);
        HandleInput(frame, battleSeconds);
        _crew.Advance(_gun, battleSeconds);
        if (_shot.Phase == BallisticTrajectoryPhase.InFlight)
        {
            for (var i = 0; i < PaceSteps[_paceIndex] && _shot.Phase == BallisticTrajectoryPhase.InFlight; i++)
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
            _burstAge += frame.DeltaSeconds;
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

        if (_profiler is not null)
            _profiler.Time("draw.static", () => _terrain.DrawStatic(frame, mvp));
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
                showPivotGlow: _shot.Phase != BallisticTrajectoryPhase.InFlight && _crew.Ready(_gun),
                loading: _crew.Loading);
            DrawMuzzleFlash(_wires);
            DrawShot(_wires);
        }

        if (_profiler is not null)
            _profiler.Time("draw.dynamic.build", BuildDynamic);
        else
            BuildDynamic();

        if (_profiler is not null)
            _profiler.Time("draw.dynamic.upload", () => frame.DrawWorldLines(_wires.AsSpan(), mvp));
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
                _crew.Ready(_gun) ? null : _crew.Phrase(_gun));
        }

        if (_profiler is not null)
            _profiler.Time("draw.hud", DrawHud);
        else
            DrawHud();
    }

    private void RebuildAimPreview()
    {
        _orderedPreview = BuildPreview(_crew.OrderedElevation, _crew.OrderedAzimuth, _gun.MuzzleSpeedMps);
        var tubeDiffers = MathF.Abs(_gun.ElevationDegrees - _crew.OrderedElevation) > 0.05f
            || MathF.Abs(_gun.AzimuthDegrees - _crew.OrderedAzimuth) > 0.05f;
        _tubePreview = tubeDiffers
            ? BuildPreview(_gun.ElevationDegrees, _gun.AzimuthDegrees, _gun.MuzzleSpeedMps)
            : [];
        _aimDirty = false;
    }

    private IReadOnlyList<Vector3> BuildPreview(float elevation, float azimuth, float speed)
    {
        var start = _gun.PredictState(_terrain.GunBaseline, elevation, azimuth, speed);
        return BallisticTrajectoryRunner.BuildPreview(
            _ballistics,
            _atmosphere.BallisticEnvironment(start.Position.Y),
            _terrain.Field,
            start,
            dtSeconds: 1.0 / 60.0,
            maxTimeSeconds: 90.0,
            maxPoints: 6_000);
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

        if (actions.ToggleFollow || frame.IsKeyPressed(Key.C))
            _camera.ToggleFollowTarget();

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
        if (frame.IsKeyDown(Key.A) || frame.IsKeyDown(Key.Left))
            dTrav -= traverse;
        if (frame.IsKeyDown(Key.D) || frame.IsKeyDown(Key.Right))
            dTrav += traverse;

        if (dElev != 0f || dTrav != 0f)
        {
            _crew.NudgeOrder(_gun, dElev, dTrav);
            _aimDirty = true;
        }
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
        if (_shot.Phase == BallisticTrajectoryPhase.InFlight)
            return;

        if (_tubePreview.Count >= 2)
            DrawArc(wires, _tubePreview, BallisticArcColors.Tube);

        if (_orderedPreview.Count >= 2)
        {
            DrawArc(wires, _orderedPreview, BallisticArcColors.Ordered);
            var mpi = _orderedPreview[^1];
            wires.WireSphere(mpi, 28f, BallisticArcColors.MpiMark, segments: 10);
            wires.Ring(mpi, 40f, BallisticArcColors.MpiMark);
            var ground = _terrain.Field.SampleHeight(mpi.X, mpi.Z);
            wires.Line(mpi, new Vector3(mpi.X, ground + 1f, mpi.Z), BallisticArcColors.MpiMark);
        }
    }

    private static void DrawArc(SilkWireBatch wires, IReadOnlyList<Vector3> points, Color color)
    {
        var stride = Math.Max(1, points.Count / 180);
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
