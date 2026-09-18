using System.Numerics;
using Novolis.Lab.TwoD;
using Novolis.Math.Geometry;
using Novolis.Rendering.Backends.TwoD.Silk;
using Novolis.Rendering.TwoD;
using RtsLite.Game;
using Novolis.Rendering.Presentation;

namespace RtsLiteTwoD.Game;

/// <summary>
/// Top-down C&amp;C-style lite RTS on <see cref="TwoDScene"/> (orthographic).
/// For the classic RA diagonal camera + PNG billboards, run <c>RtsLite</c> (Raylib).
/// </summary>
internal sealed class RtsLiteTwoDGame
{
    private static readonly Rgba32 Sand = new(168, 142, 88);
    private static readonly Rgba32 Water = new(48, 110, 118);
    private static readonly Rgba32 Tiberium = new(58, 168, 62);
    private static readonly Rgba32 WallRock = new(90, 78, 62);
    private static readonly Rgba32 AlliedBuilding = new(72, 118, 188);
    private static readonly Rgba32 SovietBuilding = new(188, 62, 52);
    private static readonly Rgba32 AlliedUnit = new(90, 140, 220);
    private static readonly Rgba32 SovietUnit = new(220, 80, 60);
    private static readonly Rgba32 OrderLine = new(255, 240, 120, 140);
    private static readonly Rgba32 HudText = new(235, 228, 200);

    private readonly OrthoPanCamera _camera = new();
    private readonly RtsSelection _selection = new();
    private readonly RtsBuildPlacer _build = new();
    private readonly List<RtsUnit> _units = [];
    private readonly List<TwoDStaticPolygon> _frameOverlays = [];

    private RtsArena _arena = null!;
    private float _enemyPulse;
    private float _spawnPulse;
    private bool _terrainBuilt;

    public void Initialize(SilkTwoDGameContext ctx)
    {
        _arena = RtsArena.Create();
        SpawnForces();
        _camera.Center = _arena.SpawnPlayer + new Vector3(6f, 0f, 4f);
        _camera.WorldUnitsPerPixel = 1f / 22f;
        ctx.Scene.Camera.ClearColor = new Rgba32(40, 48, 58);
        _camera.ApplyTo(ctx.Scene.Camera, ctx.Width, ctx.Height);
    }

    public void Update(SilkTwoDGameContext ctx)
    {
        var scene = ctx.Scene;
        UpdateCamera(ctx);

        var mouse = ReadMouse(ctx);
        var ground = ScreenToGround(scene.Camera, mouse);
        HandleBuildKeys(ctx);
        HandleSelection(ctx, ground, mouse);

        TickUnits(ctx.DeltaSeconds);
        TickProduction(ctx.DeltaSeconds);

        if (!_terrainBuilt)
        {
            BuildTerrain(scene);
            DrawBuildings(scene);
            _terrainBuilt = true;
        }

        ClearFrameOverlays(scene);
        DrawBuildGhost(scene, ground);
        DrawUnits(scene);
        DrawHud(ctx);
    }

    private void UpdateCamera(SilkTwoDGameContext ctx)
    {
        var dt = ctx.DeltaSeconds;
        var pan = 14f * dt / _camera.WorldUnitsPerPixel;
        if (ctx.IsKeyDown(Key.W))
        {
            _camera.Pan(new Vector3(0f, 0f, -pan));
        }

        if (ctx.IsKeyDown(Key.S))
        {
            _camera.Pan(new Vector3(0f, 0f, pan));
        }

        if (ctx.IsKeyDown(Key.A))
        {
            _camera.Pan(new Vector3(-pan, 0f, 0f));
        }

        if (ctx.IsKeyDown(Key.D))
        {
            _camera.Pan(new Vector3(pan, 0f, 0f));
        }

        if (ctx.IsKeyPressed(Key.Equal) || ctx.IsKeyPressed(Key.KeypadAdd))
        {
            _camera.Zoom(-1f);
        }

        if (ctx.IsKeyPressed(Key.Minus) || ctx.IsKeyPressed(Key.KeypadSubtract))
        {
            _camera.Zoom(1f);
        }

        if (MathF.Abs(ctx.MouseDelta.Y) > 0.01f)
        {
            _camera.Zoom(ctx.MouseDelta.Y > 0 ? -0.15f : 0.15f);
        }

        _camera.ApplyTo(ctx.Scene.Camera, ctx.Width, ctx.Height);
    }

    private void HandleBuildKeys(SilkTwoDGameContext ctx)
    {
        if (ctx.IsKeyPressed(Key.Number1))
        {
            _build.Select(RtsBuildingType.ConstructionYard);
        }

        if (ctx.IsKeyPressed(Key.Number2))
        {
            _build.Select(RtsBuildingType.PowerPlant);
        }

        if (ctx.IsKeyPressed(Key.Number3))
        {
            _build.Select(RtsBuildingType.Barracks);
        }

        if (ctx.IsKeyPressed(Key.Number4))
        {
            _build.Select(RtsBuildingType.OreRefinery);
        }

        if (ctx.IsKeyPressed(Key.Number5))
        {
            _build.Select(RtsBuildingType.WarFactory);
        }

        if (ctx.IsKeyPressed(Key.B))
        {
            _build.Cancel();
        }
    }

    private void HandleSelection(SilkTwoDGameContext ctx, Vector3 ground, Vector2 mouse)
    {
        var leftPressed = ctx.IsMouseButtonPressed(MouseButton.Left) || ctx.IsKeyPressed(Key.J);
        var leftDown = ctx.IsMouseButtonDown(MouseButton.Left) || ctx.IsKeyDown(Key.J);
        var rightPressed = ctx.IsMouseButtonPressed(MouseButton.Right) || ctx.IsKeyPressed(Key.H);

        if (_build.IsActive)
        {
            if (leftPressed)
            {
                _build.TryPlaceAtGround(_arena, ground, UnitTeam.Player);
            }

            if (rightPressed)
            {
                _build.Cancel();
            }

            return;
        }

        _selection.Update(_units, p => ScreenToGround(ctx.Scene.Camera, p), leftPressed, leftDown, rightPressed, mouse);
    }

    private static Vector2 ReadMouse(SilkTwoDGameContext ctx) => ctx.MousePosition;

    private static Vector3 ScreenToGround(TwoDViewport camera, Vector2 screen) =>
        camera.ScreenToWorld(screen.X, screen.Y);

    private void SpawnForces()
    {
        _units.Clear();
        SpawnSquad(UnitTeam.Player, _arena.SpawnPlayer, 8);
        SpawnSquad(UnitTeam.Enemy, _arena.SpawnEnemy, 6);
    }

    private void SpawnSquad(UnitTeam team, Vector3 anchor, int count)
    {
        for (var i = 0; i < count; i++)
        {
            var offset = new Vector3((i % 4 - 1.5f) * 0.9f, 0f, (i / 4 - 0.5f) * 0.9f);
            _units.Add(new RtsUnit { Team = team, Position = anchor + offset });
        }
    }

    private void TickUnits(float dt)
    {
        foreach (var unit in _units)
        {
            unit.Tick(_arena, dt);
        }

        _enemyPulse += dt;
        if (_enemyPulse < 3f)
        {
            return;
        }

        _enemyPulse = 0f;
        var playerUnits = _units.Where(u => u.Team == UnitTeam.Player).ToList();
        if (playerUnits.Count == 0)
        {
            return;
        }

        var target = playerUnits[Random.Shared.Next(playerUnits.Count)].Position;
        foreach (var enemy in _units.Where(u => u.Team == UnitTeam.Enemy))
        {
            enemy.MoveTarget = target + new Vector3(Random.Shared.NextSingle() - 0.5f, 0f, Random.Shared.NextSingle() - 0.5f);
        }
    }

    private void TickProduction(float dt)
    {
        _spawnPulse += dt;
        if (_spawnPulse < 4f)
        {
            return;
        }

        _spawnPulse = 0f;
        TrySpawnFromBarracks(UnitTeam.Player);
        TrySpawnFromBarracks(UnitTeam.Enemy);
    }

    private void TrySpawnFromBarracks(UnitTeam team)
    {
        foreach (var b in _arena.Buildings)
        {
            if (b.Team != team || b.Type is not (RtsBuildingType.Barracks or RtsBuildingType.WarFactory))
            {
                continue;
            }

            var spawn = b.WorldCenter(_arena) + new Vector3(Random.Shared.NextSingle() - 0.5f, 0f, Random.Shared.NextSingle() - 0.5f);
            if (!_arena.IsBlocked(spawn.X, spawn.Z))
            {
                _units.Add(new RtsUnit { Team = team, Position = spawn });
            }
        }
    }

    private void BuildTerrain(TwoDScene scene)
    {
        var size = RtsArena.GridSize * RtsArena.CellSize;
        RtsLiteTwoDRender.AddSandField(scene, size, Sand);

        for (var z = 0u; z < _arena.Walls.Height; z++)
        for (var x = 0u; x < _arena.Walls.Width; x++)
        {
            var minX = x * RtsArena.CellSize;
            var minZ = z * RtsArena.CellSize;
            if (_arena.Tiberium[x, z, 0] != 0)
            {
                scene.AddPlatform(minX, minZ, minX + 1f, minZ + 1f, Tiberium);
                continue;
            }

            if (_arena.Walls[x, z, 0] == 0)
            {
                continue;
            }

            var pond = x is >= 16 and <= 22 && z is >= 16 and <= 22;
            scene.AddPlatform(minX, minZ, minX + 1f, minZ + 1f, pond ? Water : WallRock);
        }
    }

    private void DrawBuildings(TwoDScene scene)
    {
        foreach (var b in _arena.Buildings)
        {
            var center = b.WorldCenter(_arena);
            var w = b.Width * RtsArena.CellSize * 0.92f;
            var d = b.Height * RtsArena.CellSize * 0.92f;
            var color = b.Team == UnitTeam.Player ? AlliedBuilding : SovietBuilding;
            scene.AddPlatform(center.X - w * 0.5f, center.Z - d * 0.5f, center.X + w * 0.5f, center.Z + d * 0.5f, color);
        }
    }

    private void ClearFrameOverlays(TwoDScene scene)
    {
        foreach (var marker in _frameOverlays)
        {
            scene.StaticPolygons.Remove(marker);
        }

        _frameOverlays.Clear();
    }

    private void DrawBuildGhost(TwoDScene scene, Vector3 ground)
    {
        if (!_build.TryGetGhostFootprint(_arena, ground, UnitTeam.Player, out var center, out var w, out var d, out var ok))
        {
            return;
        }

        _frameOverlays.Add(RtsLiteTwoDRender.AddFootprintGhost(scene, center, w, d, ok));
    }

    private void DrawUnits(TwoDScene scene)
    {
        foreach (var unit in _units)
        {
            var color = unit.Team == UnitTeam.Player ? AlliedUnit : SovietUnit;
            var yaw = FacingYaw(unit);
            if (unit.Selected)
            {
                _frameOverlays.Add(RtsLiteTwoDRender.AddTankMarker(
                    scene,
                    unit.Position,
                    new Rgba32(255, 255, 255, 90),
                    yaw,
                    sortKey: 900));
            }

            _frameOverlays.Add(RtsLiteTwoDRender.AddTankMarker(scene, unit.Position, color, yaw, sortKey: 1000));

            if (unit.Team == UnitTeam.Player && unit.Selected && unit.MoveTarget is { } target)
            {
                if (RtsLiteTwoDRender.AddOrderSegment(scene, unit.Position, target, OrderLine) is { } line)
                {
                    _frameOverlays.Add(line);
                }
            }
        }
    }

    private static float FacingYaw(RtsUnit unit)
    {
        if (unit.MoveTarget is { } target)
        {
            var d = target - unit.Position;
            return MathF.Atan2(d.Z, d.X);
        }

        return unit.Team == UnitTeam.Player ? 0f : MathF.PI;
    }

    private void DrawHud(SilkTwoDGameContext ctx)
    {
        var scene = ctx.Scene;
        scene.Hud.Elements.Clear();
        scene.Hud.AddText("RTS Lite TwoD (top-down) — RA camera: run RtsLite", 12, 12, 2f, HudText);
        scene.Hud.AddText("WASD pan  wheel/+/- zoom  |  1-5 build  B cancel", 12, 36, 2f, HudText);
        scene.Hud.AddText("LMB select/drag  RMB move order  |  build: LMB place", 12, 60, 2f, HudText);
        var selected = _units.Count(u => u.Team == UnitTeam.Player && u.Selected);
        scene.Hud.AddText($"units {_units.Count}  selected {selected}", 12, 84, 2f, HudText);
        if (_build.ActiveType is { } t)
        {
            scene.Hud.AddText($"placing {t.Label()}", 12, 108, 2f, HudText);
        }
    }
}
