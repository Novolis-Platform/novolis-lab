using System.Drawing;
using System.Numerics;
using Novolis.Physics.Ballistics;
using Novolis.Raylib.Game;
using Novolis.Raylib.Interact;

namespace ArtillerySimulator.Game;

internal sealed class ArtillerySimulatorGame
{
    private const int PhysicsStepsPerFrame = 32;
    private const int CamToggleKey = 67;
    private const int TerrainStyleKey = 84;
    private const int KeyQ = 81;
    private const int KeyE = 69;
    private const float AimDegreesPerSecond = 28f;

    private static readonly Color Background = Color.FromArgb(255, 14, 18, 26);
    private static readonly Color TrailColor = Color.FromArgb(255, 200, 220, 160);
    private static readonly Color PreviewColor = Color.FromArgb(120, 140, 160, 90);
    private static readonly Color ImpactColor = Color.FromArgb(255, 255, 210, 120);

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
    private bool _flatTerrain;
    private TerrainStyle _terrainStyle = TerrainStyle.Rugged;
    private IReadOnlyList<Vector3> _aimPreview = [];

    public void Initialize(RayGameContext ctx)
    {
        // Raylib default font is ASCII-ish; keep F3 diagnostics off so it doesn't sit on the title.
        if (_diagnostics.Visible)
            _diagnostics.Toggle();

        ctx.DisableCursor();
        _terrain.Rebuild(_terrainSeed, _flatTerrain, _terrainStyle);
        _shot.Reset();
        _camera.SnapToGun(_terrain.GunBaseline, _gun.BarrelDirection());
        RebuildAimPreview();
    }

    public void Update(RayGameContext ctx)
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
                PhysicsStepsPerFrame);
        }

        if (_shot.Phase == BallisticTrajectoryPhase.Ready)
            RebuildAimPreview();

        _camera.Update(ctx, _terrain.GunBaseline, _shot);

        ctx.Clear(Background);
        var barrelDir = _gun.BarrelDirection();
        var cam = _camera.Build(ctx.DeltaSeconds, _terrain.GunBaseline, _shot);
        RaylibClipPlanes.ApplyForExtent(cam.Position, _terrain.ExtentMeters);
        ctx.BeginWorld(cam);
        _terrain.Draw(ctx);
        DrawAimPreview(ctx);
        _gun.Draw(ctx, _terrain.GunBaseline, showPivotGlow: _shot.Phase == BallisticTrajectoryPhase.Ready);
        DrawShot(ctx);
        ctx.EndWorld();

        _fps.Update(ctx.DeltaSeconds);
        SimulationHud.Draw(ctx, _gun, _terrain, _atmosphere, _shot, _camera, _fps.Value);
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

        if (ctx.IsKeyPressed(KeyboardKey.Space) && _shot.Phase != BallisticTrajectoryPhase.InFlight)
        {
            var muzzle = _gun.MuzzlePosition(_terrain.GunBaseline);
            var state = _gun.CreateProjectileState(muzzle, _gun.BarrelDirection());
            _shot.Begin(state);
            if (_flatTerrain && !_gun.DragEnabled)
                LogVacuumFlatSanity(state.Velocity);
        }
    }

    private static void LogVacuumFlatSanity(Vector3 velocity)
    {
        var vx = velocity.X;
        var vy = velocity.Y;
        var g = 9.80665;
        var expected = 2.0 * vx * vy / g;
        Console.WriteLine($"[ArtillerySimulator] vacuum flat expected range ~ {expected:F1} m (2*vx*vy/g)");
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
        for (var i = 1; i < trail.Count; i++)
            ctx.DrawBolt(trail[i - 1], trail[i], TrailColor);

        if (_shot.Phase != BallisticTrajectoryPhase.Ready)
            ctx.DrawGlowSphere(_shot.CurrentPosition, GunModel.MuzzleRadius, TrailColor);

        if (_shot.Impact is { } impact)
        {
            ctx.DrawGlowSphereWires(impact.Position, 8f, ImpactColor);
            var p = impact.Position;
            ctx.DrawBolt(p + new Vector3(-12f, 0f, 0f), p + new Vector3(12f, 0f, 0f), ImpactColor);
            ctx.DrawBolt(p + new Vector3(0f, 0f, -12f), p + new Vector3(0f, 0f, 12f), ImpactColor);
        }
    }
}
