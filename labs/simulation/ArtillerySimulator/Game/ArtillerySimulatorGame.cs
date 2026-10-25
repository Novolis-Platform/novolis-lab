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
    private const int TerrainStyleKey = 84;
    private const int KeyQ = 81;
    private const int KeyE = 69;
    private const float AimDegreesPerSecond = 28f;

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
    private readonly ProjectileBallisticSimulation _drag = new(GunModel.Educational155Profile);
    private readonly ArtilleryCameras _camera = new();
    private readonly AtmosphereModel _atmosphere = AtmosphereModel.CreateRegionalDefault();

    private readonly SmoothedFps _fps = new();
    private int _terrainSeed = 42;
    private int _paceIndex = 1;
    private bool _flatTerrain;
    private TerrainStyle _terrainStyle = TerrainStyle.Rugged;
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
    internal float PredictedRangeMeters => HorizontalRange(_terrain.GunBaseline, PredictedImpact());

    internal void AimAt(float elevation, float azimuth)
    {
        _gun.NudgeElevation(elevation - _gun.ElevationDegrees);
        _gun.NudgeAzimuth(azimuth - _gun.AzimuthDegrees);
    }

    internal void SetDrag(bool enabled)
    {
        if (_gun.DragEnabled != enabled)
            _gun.ToggleDrag();
    }

    internal void SetPace(int index) => _paceIndex = Math.Clamp(index, 0, PaceSteps.Length - 1);

    internal void SetFollowCamera(bool follow) =>
        _camera.Use(follow ? CameraMode.Orbit : CameraMode.Freecam);

    internal bool TryFire()
    {
        if (_shot.Phase == BallisticTrajectoryPhase.InFlight)
            return false;

        var muzzle = _gun.MuzzlePosition(_terrain.GunBaseline);
        _shot.Begin(_gun.CreateProjectileState(muzzle, _gun.BarrelDirection()));
        _muzzleFlash = 0.22f;
        _burstAge = -1f;
        return true;
    }

    public void Initialize(RayGameContext ctx)
    {
        // Raylib default font is ASCII-ish; keep F3 diagnostics off so it doesn't sit on the title.
        if (_diagnostics.Visible)
            _diagnostics.Toggle();

        ctx.EnableCursor();
        _terrain.Rebuild(_terrainSeed, _flatTerrain, _terrainStyle);
        _shot.Reset();
        _camera.SnapToGun(_terrain.GunBaseline, _gun.BarrelDirection());
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
        HandleInput(ctx);

        if (_shot.Phase == BallisticTrajectoryPhase.InFlight)
        {
            var sim = _gun.DragEnabled ? _drag : _vacuum;
            var env = _atmosphere.BallisticEnvironment(_gun.DragEnabled, _shot.CurrentPosition.Y);
            _shot.AdvanceWithBudget(
                sim,
                env,
                _terrain.Collision,
                _terrain.Field,
                _terrain.GunBaseline,
                PaceSteps[_paceIndex]);
        }

        if (_shot.Phase == BallisticTrajectoryPhase.Impacted && _burstAge < 0f && _shot.Impact is { } landed)
        {
            _burstAge = 0f;
            _burstAt = landed.Position;
        }

        if (_burstAge >= 0f)
            _burstAge += ctx.DeltaSeconds;
        if (_muzzleFlash > 0f)
            _muzzleFlash = MathF.Max(0f, _muzzleFlash - ctx.DeltaSeconds);

        if (_shot.Phase == BallisticTrajectoryPhase.Ready)
            RebuildAimPreview();

        _camera.Update(ctx, _terrain.GunBaseline, _shot);
    }

    private void Render(RayGameContext ctx)
    {
        ctx.Clear(Background);
        var cam = _camera.Build(ctx.DeltaSeconds);
        RaylibClipPlanes.ApplyForExtent(cam.Position, _terrain.ExtentMeters);
        ctx.BeginWorld(cam);
        DrawSky(ctx);
        _terrain.Draw(ctx);
        var predicted = PredictedImpact();
        DrawAimPreview(ctx);
        _gun.Draw(ctx, _terrain.GunBaseline, showPivotGlow: _shot.Phase == BallisticTrajectoryPhase.Ready);
        DrawMuzzleFlash(ctx);
        DrawShot(ctx);
        ctx.EndWorld();

        _fps.Update(ctx.DeltaSeconds);
        var predictedRange = HorizontalRange(_terrain.GunBaseline, predicted);
        SimulationHud.Draw(
            ctx,
            _gun,
            _terrain,
            _atmosphere,
            _shot,
            _camera,
            _fps.Value,
            predictedRange,
            PaceLabel());
        _diagnostics.ToggleIfKeyPressed(ctx);
        _diagnostics.Draw(ctx);
    }

    private void RebuildAimPreview()
    {
        var muzzle = _gun.MuzzlePosition(_terrain.GunBaseline);
        var start = _gun.CreateProjectileState(muzzle, _gun.BarrelDirection());
        var sim = _gun.DragEnabled ? _drag : _vacuum;
        _aimPreview = BallisticTrajectoryRunner.BuildPreview(
            sim,
            _atmosphere.BallisticEnvironment(_gun.DragEnabled, muzzle.Y),
            _terrain.Field,
            start,
            dtSeconds: 1.0 / 60.0,
            maxTimeSeconds: 90.0,
            maxPoints: 512);
    }

    private void HandleInput(RayGameContext ctx)
    {
        var aimStep = AimDegreesPerSecond * ctx.DeltaSeconds;

        if (ctx.IsKeyDown(KeyboardKey.LeftShift))
            _gun.NudgeElevation(aimStep);
        if (ctx.IsKeyDown(KeyboardKey.LeftControl))
            _gun.NudgeElevation(-aimStep);

        if (ctx.IsKeyDown((KeyboardKey)KeyQ))
            _gun.NudgeAzimuth(-aimStep);
        if (ctx.IsKeyDown((KeyboardKey)KeyE))
            _gun.NudgeAzimuth(aimStep);

        if (ctx.IsKeyPressed(KeyboardKey.One))
            _gun.SetCharge(0);
        if (ctx.IsKeyPressed(KeyboardKey.Two))
            _gun.SetCharge(1);
        if (ctx.IsKeyPressed(KeyboardKey.Three))
            _gun.SetCharge(2);

        if (ctx.IsKeyPressed(KeyboardKey.D))
            _gun.ToggleDrag();

        if (ctx.IsKeyPressed((KeyboardKey)PaceSlowerKey))
            _paceIndex = Math.Max(0, _paceIndex - 1);
        if (ctx.IsKeyPressed((KeyboardKey)PaceFasterKey))
            _paceIndex = Math.Min(PaceSteps.Length - 1, _paceIndex + 1);

        if (ctx.IsKeyPressed((KeyboardKey)CamToggleKey))
            _camera.ToggleMode();

        if (ctx.IsKeyPressed(KeyboardKey.F))
        {
            _flatTerrain = !_flatTerrain;
            _terrain.Rebuild(_terrainSeed, _flatTerrain, _terrainStyle);
            _shot.Reset();
            _camera.SnapToGun(_terrain.GunBaseline, _gun.BarrelDirection());
        }

        if (ctx.IsKeyPressed((KeyboardKey)TerrainStyleKey) && !_flatTerrain)
        {
            _terrainStyle = (TerrainStyle)(((int)_terrainStyle + 1) % 3);
            _terrain.Rebuild(_terrainSeed, _flatTerrain, _terrainStyle);
            _shot.Reset();
            _camera.SnapToGun(_terrain.GunBaseline, _gun.BarrelDirection());
        }

        if (ctx.IsKeyPressed(KeyboardKey.R))
        {
            _terrainSeed = Random.Shared.Next();
            _terrain.Rebuild(_terrainSeed, _flatTerrain, _terrainStyle);
            _shot.Reset();
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
        if (_shot.Phase != BallisticTrajectoryPhase.Ready || _aimPreview.Count < 2)
            return;

        for (var i = 1; i < _aimPreview.Count; i++)
            ctx.DrawBolt(_aimPreview[i - 1], _aimPreview[i], PreviewColor);
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
        var age = MathF.Min(_burstAge, 3.5f);
        var fade = 1f - age / 3.5f;
        var alpha = (int)(255 * Math.Clamp(fade, 0.25f, 1f));
        var color = Color.FromArgb(alpha, ImpactColor.R, ImpactColor.G, ImpactColor.B);
        var column = 30f + age * 48f;
        ctx.DrawBolt(_burstAt, _burstAt + Vector3.UnitY * column, color);
        ctx.DrawGlowSphere(_burstAt + Vector3.UnitY * (8f + age * 6f), 6f + age * 10f, color);
        var ring = 12f + age * 36f;
        var previous = _burstAt + new Vector3(ring, 2f, 0f);
        const int segments = 16;
        for (var i = 1; i <= segments; i++)
        {
            var angle = i / (float)segments * MathF.Tau;
            var next = _burstAt + new Vector3(MathF.Cos(angle) * ring, 2f, MathF.Sin(angle) * ring);
            ctx.DrawBolt(previous, next, color);
            previous = next;
        }
    }
}
