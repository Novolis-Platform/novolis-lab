using System.Drawing;
using System.Numerics;
using Novolis.Raylib.Game;
using Novolis.Raylib.Interact;
using Input = Novolis.Raylib.Interact.Input;
using RayCamera = Novolis.Raylib.Rendering.Camera;

namespace RtsLite.Game;

internal sealed class RtsLiteGame
{
    private static readonly Color Sky = Color.FromArgb(255, 72, 88, 108);
    private static readonly Color Sand = Color.FromArgb(255, 168, 142, 88);
    private static readonly Color SandDark = Color.FromArgb(255, 138, 112, 68);
    private static readonly Color Water = Color.FromArgb(255, 48, 110, 118);
    private static readonly Color Tiberium = Color.FromArgb(255, 58, 168, 62);
    private static readonly Color WallRock = Color.FromArgb(255, 90, 78, 62);
    private static readonly Color AlliedTint = Color.FromArgb(255, 200, 220, 255);
    private static readonly Color SovietTint = Color.FromArgb(255, 255, 170, 150);
    private static readonly Color AlliedTank = Color.FromArgb(255, 72, 118, 188);
    private static readonly Color SovietTank = Color.FromArgb(255, 188, 62, 52);
    private static readonly Color OrderLine = Color.FromArgb(180, 255, 240, 120);
    private static readonly Color HudText = Color.FromArgb(255, 235, 228, 200);
    private static readonly Color Sidebar = Color.FromArgb(230, 28, 32, 38);
    private static readonly Color SidebarHi = Color.FromArgb(255, 72, 88, 108);

    private readonly DiagnosticsOverlay _diagnostics = new();
    private readonly RtsClassicCamera _camera = new();
    private readonly RtsSelection _selection = new();
    private readonly RtsBuildPlacer _build = new();
    private readonly List<RtsUnit> _units = [];

    private RtsArena _arena = null!;
    private RtsBuildingArt _art = null!;
    private float _enemyPulse;
    private float _spawnPulse;

    public void Initialize(RayGameContext ctx)
    {
        _arena = RtsArena.Create();
        _art = new RtsBuildingArt(ctx, ResolveAssetPath("Assets/buildings_set_small.png"));
        SpawnForces();
        _camera.SnapTo(_arena.CellCenter(RtsArena.GridSize / 2, RtsArena.GridSize / 2));
    }

    public void Update(RayGameContext ctx)
    {
        _camera.Update(ctx);
        HandleBuildKeys(ctx);
        var pose = _camera.BuildViewPose();
        var ground = _camera.ScreenToGround(Input.GetMousePosition(), ctx.Width, ctx.Height);

        if (_build.IsActive)
        {
            if (ctx.IsMousePressed(MouseButton.Left))
                _build.TryPlaceAtGround(_arena, ground, UnitTeam.Player);
            if (ctx.IsMousePressed(MouseButton.Right))
                _build.Cancel();
        }
        else
        {
            HandleSelection(ctx, ground);
        }

        TickUnits(ctx.DeltaSeconds);
        TickProduction(ctx.DeltaSeconds);
        _diagnostics.ToggleIfKeyPressed(ctx);

        ctx.Clear(Sky);
        var camera = RayCamera.Perspective(pose.Position, pose.Target, pose.Up, pose.FieldOfViewDegrees);
        ctx.BeginWorld(camera);
        DrawArena(ctx);
        DrawBuildings(ctx, camera);
        if (_build.IsActive && _build.TryGetGhostFootprint(_arena, ground, UnitTeam.Player, out var gCenter, out var gW, out var gD, out var ok))
        {
            var tint = ok
                ? Color.FromArgb(200, 120, 255, 140)
                : Color.FromArgb(200, 255, 90, 90);
            if (_build.ActiveType is { } ghostType)
            {
                var scale = ghostType switch
                {
                    RtsBuildingType.ConstructionYard => 2.8f,
                    RtsBuildingType.PowerPlant => 1.2f,
                    RtsBuildingType.Barracks => 2.1f,
                    RtsBuildingType.OreRefinery => 2.2f,
                    RtsBuildingType.WarFactory => 2.4f,
                    _ => 1.5f,
                };
                _art.Draw(camera, ctx, ghostType, gCenter, scale, tint);
            }

            ctx.DrawShipWires(gCenter with { Y = 0.05f }, new Vector3(gW, 0.1f, gD), tint);
        }
        DrawUnits(ctx);
        ctx.EndWorld();

        if (!_build.IsActive && _selection.ActiveDragRect(Input.GetMousePosition()) is { } drag)
            ctx.HudRect((int)drag.X, (int)drag.Y, (int)drag.Width, (int)drag.Height, Color.FromArgb(60, 120, 200, 255));

        DrawSidebar(ctx);
        RtsMinimap.Draw(ctx, _arena, _units);
        _diagnostics.Draw(ctx, (_, lines) =>
        {
            var selected = _units.Count(u => u.Team == UnitTeam.Player && u.Selected);
            lines.Add($"units {_units.Count}  selected {selected}  buildings {_arena.Buildings.Count}");
            if (_build.ActiveType is { } t)
                lines.Add($"placing {t.Label()}");
        });
    }

    private static string ResolveAssetPath(string relative)
    {
        var baseDir = AppContext.BaseDirectory;
        return Path.Combine(baseDir, relative);
    }

    private void HandleBuildKeys(RayGameContext ctx)
    {
        if (ctx.IsKeyPressed(KeyboardKey.One))
            _build.Select(RtsBuildingType.ConstructionYard);
        if (ctx.IsKeyPressed(KeyboardKey.Two))
            _build.Select(RtsBuildingType.PowerPlant);
        if (ctx.IsKeyPressed(KeyboardKey.Three))
            _build.Select(RtsBuildingType.Barracks);
        if (ctx.IsKeyPressed(KeyboardKey.Four))
            _build.Select(RtsBuildingType.OreRefinery);
        if (ctx.IsKeyPressed(KeyboardKey.Five))
            _build.Select(RtsBuildingType.WarFactory);
        if (ctx.IsKeyPressed(KeyboardKey.B))
            _build.Cancel();
    }

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

    private void HandleSelection(RayGameContext ctx, Vector3 groundClick)
    {
        var mouse = Input.GetMousePosition();
        _selection.Update(
            _units,
            p => _camera.ScreenToGround(p, ctx.Width, ctx.Height),
            ctx.IsMousePressed(MouseButton.Left),
            ctx.IsMouseDown(MouseButton.Left),
            ctx.IsMousePressed(MouseButton.Right),
            mouse);
    }

    private void TickUnits(float dt)
    {
        foreach (var unit in _units)
            unit.Tick(_arena, dt);

        _enemyPulse += dt;
        if (_enemyPulse < 3f)
            return;

        _enemyPulse = 0f;
        var playerUnits = _units.Where(u => u.Team == UnitTeam.Player).ToList();
        if (playerUnits.Count == 0)
            return;

        var target = playerUnits[Random.Shared.Next(playerUnits.Count)].Position;
        foreach (var enemy in _units.Where(u => u.Team == UnitTeam.Enemy))
            enemy.MoveTarget = target + new Vector3(Random.Shared.NextSingle() - 0.5f, 0f, Random.Shared.NextSingle() - 0.5f);
    }

    private void TickProduction(float dt)
    {
        _spawnPulse += dt;
        if (_spawnPulse < 4f)
            return;

        _spawnPulse = 0f;
        TrySpawnFromBarracks(UnitTeam.Player, AlliedTank);
        TrySpawnFromBarracks(UnitTeam.Enemy, SovietTank);
    }

    private void TrySpawnFromBarracks(UnitTeam team, Color _)
    {
        foreach (var b in _arena.Buildings)
        {
            if (b.Team != team || b.Type is not (RtsBuildingType.Barracks or RtsBuildingType.WarFactory))
                continue;

            var spawn = b.WorldCenter(_arena) + new Vector3(Random.Shared.NextSingle() - 0.5f, 0f, Random.Shared.NextSingle() - 0.5f);
            if (!_arena.IsBlocked(spawn.X, spawn.Z))
                _units.Add(new RtsUnit { Team = team, Position = spawn });
        }
    }

    private void DrawArena(RayGameContext ctx)
    {
        var center = _arena.CellCenter(RtsArena.GridSize / 2, RtsArena.GridSize / 2);
        ctx.DrawPlane(center, new Vector2(RtsArena.GridSize, RtsArena.GridSize), Sand);

        for (var z = 0u; z < _arena.Walls.Height; z++)
        for (var x = 0u; x < _arena.Walls.Width; x++)
        {
            var c = _arena.CellCenter(x, z);
            if (_arena.Tiberium[x, z, 0] != 0)
            {
                ctx.DrawShipBox(c + new Vector3(0f, 0.12f, 0f), new Vector3(0.5f, 0.2f, 0.5f), Tiberium);
                continue;
            }

            if (_arena.Walls[x, z, 0] == 0)
            {
                if ((x + z) % 5 == 0)
                    ctx.DrawShipBox(c + new Vector3(0f, 0.02f, 0f), new Vector3(0.95f, 0.04f, 0.95f), SandDark);
                continue;
            }

            var color = _arena.Walls[x, z, 0] != 0 && x is >= 16 and <= 22 && z is >= 16 and <= 22
                ? Water
                : WallRock;
            ctx.DrawShipBox(c + new Vector3(0f, 0.25f, 0f), new Vector3(RtsArena.CellSize, 0.5f, RtsArena.CellSize), color);
        }
    }

    private void DrawBuildings(RayGameContext ctx, RayCamera camera)
    {
        foreach (var b in _arena.Buildings)
        {
            var tint = b.Team == UnitTeam.Player ? AlliedTint : SovietTint;
            _art.Draw(camera, ctx, b.Type, b.WorldCenter(_arena), b.BillboardScale, tint);
        }
    }

    private void DrawUnits(RayGameContext ctx)
    {
        foreach (var unit in _units)
        {
            var color = unit.Team == UnitTeam.Player ? AlliedTank : SovietTank;
            var pos = unit.Position + new Vector3(0f, 0.2f, 0f);
            if (unit.Selected)
                ctx.DrawShipWires(pos, new Vector3(0.9f, 0.35f, 1.2f), Color.FromArgb(255, 255, 255, 200));

            ctx.DrawShipBox(pos, new Vector3(0.75f, 0.28f, 1.1f), color);
            ctx.DrawShipBox(pos + new Vector3(0f, 0.18f, 0.35f), new Vector3(0.35f, 0.2f, 0.5f), Color.FromArgb(255, 40, 40, 40));

            if (unit.MoveTarget is { } target)
                ctx.DrawBolt(pos, target + new Vector3(0f, 0.2f, 0f), OrderLine);
        }
    }

    private void DrawSidebar(RayGameContext ctx)
    {
        var w = 200;
        var x = 8;
        var y = ctx.Height - 168;
        ctx.HudRect(x, y, w, 160, Sidebar);
        ctx.HudRect(x, y, w, 22, SidebarHi);
        ctx.HudText("ALLIED BUILD", x + 8, y + 4, 14, HudText);
        ctx.HudText("∞ ORE", x + 118, y + 4, 14, Color.FromArgb(255, 140, 255, 120));

        var row = y + 28;
        DrawBuildButton(ctx, x + 8, row, RtsBuildingType.ConstructionYard, "1");
        DrawBuildButton(ctx, x + 8, row + 24, RtsBuildingType.PowerPlant, "2");
        DrawBuildButton(ctx, x + 8, row + 48, RtsBuildingType.Barracks, "3");
        DrawBuildButton(ctx, x + 8, row + 72, RtsBuildingType.OreRefinery, "4");
        DrawBuildButton(ctx, x + 8, row + 96, RtsBuildingType.WarFactory, "5");

        ctx.HudText("MMB drag | edge scroll | WASD", x + 8, y + 142, 11, Color.FromArgb(200, 180, 170, 150));
        ctx.HudText("LMB place  RMB cancel build", x + 8, ctx.Height - 18, 12, HudText);
    }

    private void DrawBuildButton(RayGameContext ctx, int x, int y, RtsBuildingType type, string key)
    {
        var active = _build.ActiveType == type;
        var bg = active ? Color.FromArgb(255, 90, 120, 160) : Color.FromArgb(180, 48, 52, 60);
        ctx.HudRect(x, y, 184, 20, bg);
        ctx.HudText($"{key} {type.Label()}", x + 6, y + 3, 13, HudText);
    }
}
