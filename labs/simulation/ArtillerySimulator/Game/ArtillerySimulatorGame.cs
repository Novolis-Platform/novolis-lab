using System.Drawing;
using System.Numerics;
using Novolis.Agent.Surface;
using Novolis.Physics.Ballistics;
using Novolis.Raylib.Game;
using Novolis.Raylib.Interact;

namespace ArtillerySimulator.Game;

internal sealed class ArtillerySimulatorGame
{
    private const int CamToggleKey = 67;
    private const int PaceSlowerKey = 44;
    private const int PaceFasterKey = 46;
    private static readonly int[] PaceSteps = [2, 6, 16, 48];
    private const int KeyQ = 81;
    private const int KeyE = 69;
    private const int KeyW = 87;
    private const int KeyS = 83;
    private const int KeyV = 86;
    private const float MilsPerDegree = 6400f / 360f;
    private const float FineMilsPerSecond = 20f;
    private const float CoarseMilsPerPixel = 0.35f;
    private const float FineMilsPerPixel = 0.07f;

    private static readonly Color Background = Color.FromArgb(255, 17, 22, 34);
    private static readonly Color SunColor = Color.FromArgb(255, 255, 186, 110);
    private static readonly Color SunHalo = Color.FromArgb(70, 255, 170, 90);
    private static readonly Color TrailHot = Color.FromArgb(255, 255, 176, 86);
    private static readonly Color TrailCool = Color.FromArgb(90, 180, 140, 70);
    private static readonly Color PreviewColor = Color.FromArgb(210, 255, 186, 92);
    private static readonly Color ImpactColor = Color.FromArgb(255, 255, 120, 72);
    private static readonly Color FlashColor = Color.FromArgb(255, 255, 214, 150);

    private readonly DiagnosticsOverlay _diagnostics = new();
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

    public void Initialize(RayGameContext ctx)
    {
        // Raylib default font is ASCII-ish; keep F3 diagnostics off so it doesn't sit on the title.
        if (_diagnostics.Visible)
            _diagnostics.Toggle();

        ctx.EnableCursor();
        _terrain.Rebuild(42, flat: false, TerrainStyle.Rugged);
        _mission = new KristiansandDefenseMission(_terrain);
        _terrain.PlaceGun(_mission.GunX, _mission.GunZ);
        _gun.LayCenter(_mission.TrailBearingDegrees);
        _gun.SetCharge(_mission.OpeningChargeIndex);
        _gun.SetElevation(_mission.OpeningElevationDegrees);
        _gun.NudgeAzimuth(_mission.OpeningAzimuthDegrees - _mission.TrailBearingDegrees);
        _crew.SeatOpening(_gun);
        _shot.Reset();
        _camera.ShowMapOverview(_terrain.EastMeters, _terrain.NorthMeters);
        RebuildAimPreview();
        _surface = AgentSurface.AttachAll(
            _agent,
            AgentSurfaceDefinition.From<IArtillerySurface>(),
            new AgentAttachOptions { EnableStdio = false, EnableMcpStdio = false });
        _ = _surface;
    }

    public void Update(RayGameContext ctx)
    {
        _agent.RunFrame(() => Simulate(ctx));
        Render(ctx);
    }

    private void Simulate(RayGameContext ctx)
    {
        var battleSeconds = PaceSteps[_paceIndex] * (1f / 120f);
        HandleInput(ctx, battleSeconds);
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
            _burstAge += ctx.DeltaSeconds;
        if (_muzzleFlash > 0f)
            _muzzleFlash = MathF.Max(0f, _muzzleFlash - ctx.DeltaSeconds);

        if (_shot.Phase != BallisticTrajectoryPhase.InFlight)
            RebuildAimPreview();

        if (_mission is { } mission)
            _camera.SetBattleFocus(mission.BattleFocus(_terrain.GunBaseline));
        _camera.Update(ctx, _terrain.GunBaseline, _shot);
    }

    private void Render(RayGameContext ctx)
    {
        ctx.Clear(Background);
        var cam = _camera.Build(ctx.DeltaSeconds);
        RaylibClipPlanes.ApplyForExtent(cam.Position, _terrain.EastMeters, _terrain.NorthMeters);
        ctx.BeginWorld(cam);
        DrawSky(ctx);
        _terrain.Draw(ctx, cam);
        if (!_flatTerrain)
        {
            if (KristiansandReferenceMap.IsActive)
                KristiansandMapFeatures.DrawTableLinework(ctx);
            else
            {
                KristiansandMapFeatures.DrawWater(ctx, _terrain);
                KristiansandMapFeatures.DrawLinework(ctx, _terrain);
            }
        }
        _mission?.Draw(ctx);
        var predicted = PredictedImpact();
        DrawAimPreview(ctx);
        _gun.Draw(
            ctx,
            _terrain.GunBaseline,
            showPivotGlow: _shot.Phase != BallisticTrajectoryPhase.InFlight && _crew.Ready(_gun),
            loading: _crew.Loading);
        DrawMuzzleFlash(ctx);
        DrawShot(ctx);
        ctx.EndWorld();

        _fps.Update(ctx.DeltaSeconds);
        var predictedRange = HorizontalRange(_terrain.GunBaseline, predicted);
        SimulationHud.Draw(
            ctx,
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
            _crew.Ready(_gun) ? null : _crew.Phrase(_gun));
        _diagnostics.ToggleIfKeyPressed(ctx);
        _diagnostics.Draw(ctx);
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

    private void HandleInput(RayGameContext ctx, float battleSeconds)
    {
        var fine = ctx.IsKeyDown(KeyboardKey.LeftShift);
        var elevate = (fine ? FineMilsPerSecond : CrewDrill.ElevateMilsPerSecond) / MilsPerDegree * battleSeconds;
        var traverse = (fine ? FineMilsPerSecond : CrewDrill.TraverseMilsPerSecond) / MilsPerDegree * battleSeconds;

        if (_camera.Mode != CameraMode.Freecam)
        {
            if (ctx.IsKeyDown((KeyboardKey)KeyW))
                _crew.NudgeOrder(_gun, elevate, 0f);
            if (ctx.IsKeyDown((KeyboardKey)KeyS))
                _crew.NudgeOrder(_gun, -elevate, 0f);
        }

        if (ctx.IsKeyDown((KeyboardKey)KeyQ))
            _crew.NudgeOrder(_gun, 0f, -traverse);
        if (ctx.IsKeyDown((KeyboardKey)KeyE))
            _crew.NudgeOrder(_gun, 0f, traverse);

        if (ctx.IsMouseDown(MouseButton.Left))
        {
            var milsPerPixel = fine ? FineMilsPerPixel : CoarseMilsPerPixel;
            var degreesPerPixel = milsPerPixel / MilsPerDegree;
            var delta = ctx.MouseDelta;
            var azimuth = Math.Clamp(delta.X * degreesPerPixel, -traverse, traverse);
            var elevation = Math.Clamp(-delta.Y * degreesPerPixel, -elevate, elevate);
            _crew.NudgeOrder(_gun, elevation, azimuth);
        }

        if (ctx.IsKeyPressed(KeyboardKey.One))
            _crew.OrderCharge(_gun, 0);
        if (ctx.IsKeyPressed(KeyboardKey.Two))
            _crew.OrderCharge(_gun, 1);
        if (ctx.IsKeyPressed(KeyboardKey.Three))
            _crew.OrderCharge(_gun, 2);
        if (ctx.IsKeyPressed(KeyboardKey.Four))
            _crew.OrderCharge(_gun, 3);

        if (ctx.IsKeyPressed(KeyboardKey.D))
            _gun.ToggleDrag();

        if (ctx.IsKeyPressed((KeyboardKey)PaceSlowerKey))
            _paceIndex = Math.Max(0, _paceIndex - 1);
        if (ctx.IsKeyPressed((KeyboardKey)PaceFasterKey))
            _paceIndex = Math.Min(PaceSteps.Length - 1, _paceIndex + 1);

        if (ctx.IsKeyPressed((KeyboardKey)CamToggleKey))
            _camera.ToggleMode();

        if (ctx.IsKeyPressed((KeyboardKey)KeyV))
            _camera.ShowMapOverview(_terrain.EastMeters, _terrain.NorthMeters);

        if (ctx.IsKeyPressed(KeyboardKey.M))
            Kommunekart3DView.ApplyStartupCamera(_camera, _terrain);

        if (ctx.IsKeyPressed(KeyboardKey.G) && _mission is { } mapGun)
        {
            _camera.SnapToGun(_terrain.GunBaseline, _gun.BarrelDirection());
            _camera.LookAtBattle(_terrain.GunBaseline, mapGun.NearestAlive(_terrain.GunBaseline));
        }

        if (ctx.IsKeyPressed(KeyboardKey.F))
        {
            _flatTerrain = !_flatTerrain;
            _terrain.Rebuild(42, _flatTerrain, TerrainStyle.Rugged);
            if (_mission is { } mission)
                _terrain.PlaceGun(mission.GunX, mission.GunZ);
            _shot.Reset();
            _crew.SeatOpening(_gun);
            _camera.SnapToGun(_terrain.GunBaseline, _gun.BarrelDirection());
        }

        if (ctx.IsKeyPressed(KeyboardKey.Space) && TryFire() && _flatTerrain && !_gun.DragEnabled)
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

    private void DrawSky(RayGameContext ctx)
    {
        var sun = new Vector3(_terrain.ExtentMeters * 0.55f, 6800f, _terrain.GunBaseline.Z - 4000f);
        ctx.DrawGlowSphere(sun, 900f, SunColor);
        ctx.DrawGlowSphereWires(sun, 1400f, SunHalo);
    }

    private void DrawMuzzleFlash(RayGameContext ctx)
    {
        if (_muzzleFlash <= 0f)
            return;

        var muzzle = _gun.MuzzlePosition(_terrain.GunBaseline);
        var radius = 1.4f + (0.22f - _muzzleFlash) * 18f;
        ctx.DrawGlowSphere(muzzle, radius, FlashColor);
    }

    private void DrawAimPreview(RayGameContext ctx)
    {
        if (_shot.Phase == BallisticTrajectoryPhase.InFlight || _aimPreview.Count < 2)
            return;

        var stride = Math.Max(1, _aimPreview.Count / 160);
        for (var i = stride; i < _aimPreview.Count; i += stride)
            ctx.DrawBolt(_aimPreview[i - stride], _aimPreview[i], PreviewColor);

        var last = _aimPreview.Count - 1;
        var drawn = last - (last % stride);
        if (drawn != last && drawn >= 0)
            ctx.DrawBolt(_aimPreview[drawn], _aimPreview[last], PreviewColor);
    }

    private void DrawShot(RayGameContext ctx)
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
            ctx.DrawBolt(trail[i - 1], trail[i], color);
            ctx.DrawBolt(trail[i - 1] + lift, trail[i] + lift, color);
        }

        if (_shot.Phase == BallisticTrajectoryPhase.InFlight)
        {
            var span = HorizontalRange(_terrain.GunBaseline, _shot.CurrentPosition);
            var radius = Math.Clamp(span * 0.012f, 14f, 70f);
            ctx.DrawGlowSphere(_shot.CurrentPosition, radius * 0.45f, TrailHot);
            ctx.DrawGlowSphereWires(_shot.CurrentPosition, radius, FlashColor);
            var ground = _terrain.Field.SampleHeight(_shot.CurrentPosition.X, _shot.CurrentPosition.Z);
            var drop = new Vector3(_shot.CurrentPosition.X, ground + 2f, _shot.CurrentPosition.Z);
            ctx.DrawBolt(_shot.CurrentPosition, drop, Color.FromArgb(160, TrailHot.R, TrailHot.G, TrailHot.B));
        }

        if (_burstAge >= 0f)
            DrawBurst(ctx);
    }

    private void DrawBurst(RayGameContext ctx)
    {
        var age = MathF.Min(_burstAge, 8f);
        var fade = 1f - age / 8f;
        var alpha = (int)(220 * Math.Clamp(fade, 0.35f, 1f));
        var color = Color.FromArgb(alpha, ImpactColor.R, ImpactColor.G, ImpactColor.B);
        var pale = Color.FromArgb(alpha / 2, ImpactColor.R, ImpactColor.G, ImpactColor.B);
        var column = 40f + age * 30f;
        ctx.DrawBolt(_burstAt, _burstAt + Vector3.UnitY * column, color);
        ctx.DrawGlowSphere(_burstAt + Vector3.UnitY * (10f + age * 4f), 18f + age * 6f, color);
        DrawGroundRing(ctx, _burstAt, KristiansandDefenseMission.CaughtMeters, color);
        DrawGroundRing(ctx, _burstAt, KristiansandDefenseMission.PinnedMeters, pale);
    }

    private static void DrawGroundRing(RayGameContext ctx, Vector3 center, float radius, Color color)
    {
        var previous = center + new Vector3(radius, 3f, 0f);
        const int segments = 28;
        for (var i = 1; i <= segments; i++)
        {
            var angle = i / (float)segments * MathF.Tau;
            var next = center + new Vector3(MathF.Cos(angle) * radius, 3f, MathF.Sin(angle) * radius);
            ctx.DrawBolt(previous, next, color);
            previous = next;
        }
    }
}
