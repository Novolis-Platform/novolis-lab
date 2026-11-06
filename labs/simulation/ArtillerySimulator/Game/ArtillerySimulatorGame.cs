using System.Drawing;
using System.Numerics;
using Novolis.Agent.Surface;
using Novolis.Math.Geometry;
using Novolis.Physics.Ballistics;
using Novolis.Silk;

namespace ArtillerySimulator.Game;

internal sealed class ArtillerySimulatorGame
{
    private static readonly int[] PaceSteps = [2, 6, 16, 48];
    private const float MilsPerDegree = 6400f / 360f;
    private const float FineMilsPerSecond = 20f;
    private const float CoarseMilsPerPixel = 0.35f;
    private const float FineMilsPerPixel = 0.07f;

    private static readonly Color TrailHot = Color.FromArgb(255, 255, 176, 86);
    private static readonly Color TrailCool = Color.FromArgb(90, 180, 140, 70);
    private static readonly Color PreviewColor = Color.FromArgb(210, 255, 186, 92);
    private static readonly Color ImpactColor = Color.FromArgb(255, 255, 120, 72);
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
    private readonly ProjectileBallisticSimulation _vacuum = new();
    private readonly ProjectileBallisticSimulation _drag = new(GunModel.Educational155Profile, DragCoefficientLaw.BoatTail);
    private readonly ArtilleryCameras _camera = new();
    private readonly AtmosphereModel _atmosphere = AtmosphereModel.CreateRegionalDefault();
    private readonly SilkWireBatch _wires = new();

    private readonly SmoothedFps _fps = new();
    private readonly CrewDrill _crew = new();
    private int _paceIndex = 1;
    private bool _flatTerrain;
    private KristiansandDefenseMission? _mission;
    private IReadOnlyList<Vector3> _aimPreview = [];
    private float _muzzleFlash;
    private float _burstAge = -1f;
    private Vector3 _burstAt;
    private readonly ArtilleryLiveHost _agent;
    private AgentSurface? _surface;

    public ArtillerySimulatorGame()
    {
        _agent = new ArtilleryLiveHost(this);
    }

    internal GunModel Gun => _gun;
    internal TerrainWorld Terrain => _terrain;
    internal BallisticTrajectoryRunner Shot => _shot;
    internal CameraMode CameraMode => _camera.Mode;
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

    internal void SetDrag(bool enabled)
    {
        if (_gun.DragEnabled != enabled)
            _gun.ToggleDrag();
    }

    internal void SetPace(int index) => _paceIndex = Math.Clamp(index, 0, PaceSteps.Length - 1);

    internal void SetFollowCamera(bool follow) =>
        _camera.Use(follow ? CameraMode.Orbit : CameraMode.Freecam);

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
        return true;
    }

    public void Initialize(SilkFrame frame)
    {
        _terrain.Rebuild(42, flat: false, TerrainStyle.Rugged);
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
        _agent.RunFrame(() => Simulate(frame));
        Render(frame);
    }

    private void Simulate(SilkFrame frame)
    {
        var battleSeconds = PaceSteps[_paceIndex] * (1f / 120f);
        HandleInput(frame, battleSeconds);
        _crew.Advance(_gun, battleSeconds);
        if (_shot.Phase == BallisticTrajectoryPhase.InFlight)
        {
            var sim = _gun.DragEnabled ? _drag : _vacuum;
            for (var i = 0; i < PaceSteps[_paceIndex] && _shot.Phase == BallisticTrajectoryPhase.InFlight; i++)
            {
                var env = _atmosphere.BallisticEnvironment(_gun.DragEnabled, _shot.CurrentPosition.Y);
                _shot.AdvanceOne(sim, env, _terrain.Collision, _terrain.Field, _terrain.GunBaseline);
            }
        }

        if (!_flatTerrain)
        {
            Vector3? incoming = _shot.Phase == BallisticTrajectoryPhase.InFlight ? _shot.CurrentPosition : null;
            _mission?.Advance(battleSeconds, incoming);
        }

        if (_shot.Phase == BallisticTrajectoryPhase.Impacted && _burstAge < 0f && _shot.Impact is { } landed)
        {
            _burstAge = 0f;
            _burstAt = landed.Position;
            if (!_flatTerrain)
                _mission?.ResolveImpact(landed.Position);
        }

        if (_burstAge >= 0f)
            _burstAge += frame.DeltaSeconds;
        if (_muzzleFlash > 0f)
            _muzzleFlash = MathF.Max(0f, _muzzleFlash - frame.DeltaSeconds);

        if (_shot.Phase != BallisticTrajectoryPhase.InFlight)
            RebuildAimPreview();

        if (_mission is { } mission)
            _camera.SetBattleFocus(mission.BattleFocus(_terrain.GunBaseline));
        _camera.Update(frame, _terrain.GunBaseline, _shot);
    }

    private void Render(SilkFrame frame)
    {
        frame.Clear(Rgba32.Black);
        var cam = _camera.Build(frame.DeltaSeconds);
        var aspect = frame.Width / (float)Math.Max(1, frame.Height);
        // RH look-at + Y-up Z-north would put east on screen-left; CreateMapView compensates.
        var view = KristiansandGeoref.CreateMapView(cam.Position, cam.Target, cam.Up);
        var near = 2f;
        var far = MathF.Max(40_000f, Vector3.Distance(cam.Position, cam.Target) * 8f);
        var proj = Matrix4x4.CreatePerspectiveFieldOfView(
            cam.FieldOfViewDegrees * (MathF.PI / 180f),
            aspect,
            near,
            far);
        var mvp = view * proj;

        // Cached terrain wires (built once). Dynamic overlay rebuilt each frame.
        frame.DrawWorldLines(_terrain.CachedWires, mvp);

        _wires.Clear();
        DrawSky(_wires);
        _terrain.DrawDynamic(_wires);
        if (!_flatTerrain)
            KristiansandMapFeatures.DrawLinework(_wires, _terrain);
        _mission?.Draw(_wires);
        DrawAimPreview(_wires);
        _gun.Draw(
            _wires,
            _terrain.GunBaseline,
            showPivotGlow: _shot.Phase != BallisticTrajectoryPhase.InFlight && _crew.Ready(_gun),
            loading: _crew.Loading);
        DrawMuzzleFlash(_wires);
        DrawShot(_wires);
        frame.DrawWorldLines(_wires.AsSpan(), mvp);

        _fps.Update(frame.DeltaSeconds);
        var predicted = PredictedImpact();
        var predictedRange = HorizontalRange(_terrain.GunBaseline, predicted);
        frame.SetTitle(SimulationHud.TitleLine(
            _gun,
            _crew.OrderedElevation,
            _crew.OrderedAzimuth,
            _terrain,
            _atmosphere,
            _shot,
            _camera,
            _fps.Value,
            predictedRange,
            PaceLabel(),
            _mission?.HudLine(_flatTerrain),
            _crew.Ready(_gun) ? null : _crew.Phrase(_gun)));
    }

    private void RebuildAimPreview()
    {
        var start = _gun.PredictState(
            _terrain.GunBaseline,
            _crew.OrderedElevation,
            _crew.OrderedAzimuth,
            _gun.MuzzleSpeedMps);
        var sim = _gun.DragEnabled ? _drag : _vacuum;
        _aimPreview = BallisticTrajectoryRunner.BuildPreview(
            sim,
            _atmosphere.BallisticEnvironment(_gun.DragEnabled, start.Position.Y),
            _terrain.Field,
            start,
            dtSeconds: 1.0 / 60.0,
            maxTimeSeconds: 90.0,
            maxPoints: 6_000);
    }

    private void HandleInput(SilkFrame frame, float battleSeconds)
    {
        var fine = frame.IsKeyDown(Key.ShiftLeft) || frame.IsKeyDown(Key.ShiftRight);
        var elevate = (fine ? FineMilsPerSecond : CrewDrill.ElevateMilsPerSecond) / MilsPerDegree * battleSeconds;
        var traverse = (fine ? FineMilsPerSecond : CrewDrill.TraverseMilsPerSecond) / MilsPerDegree * battleSeconds;

        if (_camera.Mode != CameraMode.Freecam)
        {
            if (frame.IsKeyDown(Key.W))
                _crew.NudgeOrder(_gun, elevate, 0f);
            if (frame.IsKeyDown(Key.S))
                _crew.NudgeOrder(_gun, -elevate, 0f);
        }

        if (frame.IsKeyDown(Key.Q))
            _crew.NudgeOrder(_gun, 0f, -traverse);
        if (frame.IsKeyDown(Key.E))
            _crew.NudgeOrder(_gun, 0f, traverse);

        // Left-drag lays the piece; Shift+Left is orbit pan (see ArtilleryCameras).
        if (frame.IsMouseButtonDown(MouseButton.Left)
            && !frame.IsKeyDown(Key.ShiftLeft)
            && !frame.IsKeyDown(Key.ShiftRight))
        {
            var milsPerPixel = fine ? FineMilsPerPixel : CoarseMilsPerPixel;
            var degreesPerPixel = milsPerPixel / MilsPerDegree;
            var azimuth = Math.Clamp(frame.MouseDeltaX * degreesPerPixel, -traverse, traverse);
            var elevation = Math.Clamp(-frame.MouseDeltaY * degreesPerPixel, -elevate, elevate);
            _crew.NudgeOrder(_gun, elevation, azimuth);
        }

        if (frame.IsKeyPressed(Key.Number1))
            _crew.OrderCharge(_gun, 0);
        if (frame.IsKeyPressed(Key.Number2))
            _crew.OrderCharge(_gun, 1);
        if (frame.IsKeyPressed(Key.Number3))
            _crew.OrderCharge(_gun, 2);
        if (frame.IsKeyPressed(Key.Number4))
            _crew.OrderCharge(_gun, 3);

        if (frame.IsKeyPressed(Key.D) && _camera.Mode != CameraMode.Freecam)
            _gun.ToggleDrag();

        if (frame.IsKeyPressed(Key.Comma) || frame.IsKeyPressed(Key.KeypadSubtract))
            _paceIndex = Math.Max(0, _paceIndex - 1);
        if (frame.IsKeyPressed(Key.Period) || frame.IsKeyPressed(Key.KeypadAdd))
            _paceIndex = Math.Min(PaceSteps.Length - 1, _paceIndex + 1);

        if (frame.IsKeyPressed(Key.C))
            _camera.ToggleMode();

        if (frame.IsKeyPressed(Key.V) || frame.IsKeyPressed(Key.O))
            _camera.ShowMapOverview(_terrain.EastMeters, _terrain.NorthMeters);

        if (frame.IsKeyPressed(Key.M))
            Kommunekart3DView.ApplyStartupCamera(_camera, _terrain);

        if (frame.IsKeyPressed(Key.G) && _mission is { } mapGun)
        {
            _camera.SnapToGun(_terrain.GunBaseline, _gun.BarrelDirection());
            _camera.LookAtBattle(_terrain.GunBaseline, mapGun.NearestAlive(_terrain.GunBaseline));
        }

        if (frame.IsKeyPressed(Key.R) && _mission is { } fort)
        {
            _camera.SnapToGun(_terrain.GunBaseline, _gun.BarrelDirection());
            _ = fort;
        }

        if (frame.IsKeyPressed(Key.F))
        {
            _flatTerrain = !_flatTerrain;
            _terrain.Rebuild(42, _flatTerrain, TerrainStyle.Rugged);
            if (_mission is { } mission)
                _terrain.PlaceGun(mission.GunX, mission.GunZ);
            _shot.Reset();
            _crew.SeatOpening(_gun);
            _camera.SnapToGun(_terrain.GunBaseline, _gun.BarrelDirection());
        }

        if (frame.IsKeyPressed(Key.Space) && TryFire() && _flatTerrain && !_gun.DragEnabled)
            LogVacuumFlatSanity(_gun.CreateProjectileState(_gun.MuzzlePosition(_terrain.GunBaseline), _gun.BarrelDirection()).Velocity);
    }

    private static void LogVacuumFlatSanity(Vector3 velocity)
    {
        var vx = velocity.X;
        var vy = velocity.Y;
        var g = 9.80665;
        var expected = 2.0 * vx * vy / g;
        Console.WriteLine($"[ArtillerySimulator] vacuum flat expected range ~ {expected:F1} m (2*vx*vy/g)");
    }

    private Vector3 PredictedImpact() =>
        _aimPreview.Count == 0 ? _terrain.GunBaseline : _aimPreview[^1];

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

    private void DrawAimPreview(SilkWireBatch wires)
    {
        if (_shot.Phase == BallisticTrajectoryPhase.InFlight || _aimPreview.Count < 2)
            return;

        var stride = Math.Max(1, _aimPreview.Count / 160);
        for (var i = stride; i < _aimPreview.Count; i += stride)
            wires.Line(_aimPreview[i - stride], _aimPreview[i], PreviewColor);

        var last = _aimPreview.Count - 1;
        var drawn = last - (last % stride);
        if (drawn != last && drawn >= 0)
            wires.Line(_aimPreview[drawn], _aimPreview[last], PreviewColor);
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
        var color = Color.FromArgb(alpha, ImpactColor.R, ImpactColor.G, ImpactColor.B);
        var pale = Color.FromArgb(alpha / 2, ImpactColor.R, ImpactColor.G, ImpactColor.B);
        var column = 40f + age * 30f;
        wires.Line(_burstAt, _burstAt + Vector3.UnitY * column, color);
        wires.WireSphere(_burstAt + Vector3.UnitY * (10f + age * 4f), 18f + age * 6f, color, segments: 12);
        wires.Ring(_burstAt, KristiansandDefenseMission.CaughtMeters, color);
        wires.Ring(_burstAt, KristiansandDefenseMission.PinnedMeters, pale);
    }
}
