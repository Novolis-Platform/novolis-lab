using System.Drawing;
using System.Numerics;
using Novolis.Physics.Cloth;
using Novolis.Raylib.Game;
using Novolis.Raylib.Interact;
using Novolis.Simulation.View;
using Input = Novolis.Raylib.Interact.Input;
using RayCamera = Novolis.Raylib.Rendering.Camera;

namespace ClothPlay.Game;

internal sealed class ClothPlayGame
{
    private static readonly Color Background = Color.FromArgb(255, 18, 24, 30);
    private static readonly Color Floor = Color.FromArgb(255, 42, 52, 58);
    private static readonly Color GridLine = Color.FromArgb(255, 58, 72, 82);
    private static readonly Color WallWire = Color.FromArgb(255, 70, 88, 100);
    private static readonly Color HudText = Color.FromArgb(255, 210, 225, 230);
    private static readonly Color HudWarn = Color.FromArgb(255, 240, 120, 80);

    private readonly DiagnosticsOverlay _diagnostics = new();
    private readonly OrbitCameraRig _camera = new();

    private PlayRoom _room = null!;
    private ClothSheet _cloth = null!;
    private ClothPinMode _pinMode = ClothPinMode.TopRow;
    private KatanaEdge _katanaEdge = KatanaEdge.Up;

    public void Initialize(RayGameContext ctx)
    {
        _ = ctx;
        _room = PlayRoom.Create();
        _cloth = new ClothSheet();
        ResetFlag();
        _camera.SnapTarget(_room.FloorCenter + new Vector3(0f, 2.2f, 0f));
        _camera.Distance = 9f;
        _camera.MinDistance = 4f;
        _camera.MaxDistance = 18f;
        _camera.Yaw = 0.55f;
        _camera.Pitch = 0.42f;
        _camera.FieldOfViewDegrees = 50f;
    }

    public void Update(RayGameContext ctx)
    {
        UpdateCamera(ctx);
        HandleKeys(ctx);

        var pose = _camera.BuildViewPose(ctx.DeltaSeconds);
        TryImpulseFromClick(ctx, pose);
        _diagnostics.ToggleIfKeyPressed(ctx);
        _cloth.Step(_room.CollisionWorld, ctx.DeltaSeconds);

        ctx.Clear(Background);
        var camera = RayCamera.Perspective(pose.Position, pose.Target, pose.Up, pose.FieldOfViewDegrees);
        ctx.BeginWorld(camera);
        DrawRoom(ctx);
        _room.Sword?.Draw(ctx);
        ClothRenderer.Draw(ctx, _cloth);
        ctx.EndWorld();

        var hudColor = _cloth.HitGround ? HudWarn : HudText;
        ctx.Text(
            "LMB shove | R flag | 3 drape | 4 cut | 5 edge-up | 6 edge-down | B blast | W wind | F3",
            16,
            16,
            18,
            hudColor);
        if (_cloth.HitGround)
            ctx.Text("GROUND HIT — cloth should rest on katana / hang as flag, not the floor", 16, 40, 18, HudWarn);

        _diagnostics.Draw(ctx, (_, lines) =>
        {
            lines.Add($"scenario {_cloth.Scenario}  katana {_katanaEdge}  cut {(_cloth.CuttingEnabled ? "on" : "off")}");
            lines.Add($"particles {_cloth.Spheres.Count}  joints {_cloth.Joints.Count}  severed {_cloth.LastSeveredJoints}");
            lines.Add($"joint corr {_cloth.LastJointCorrections}  wind {(_cloth.WindEnabled ? "on" : "off")}  ground {(_cloth.HitGround ? "BAD" : "ok")}");
        });
    }

    private void HandleKeys(RayGameContext ctx)
    {
        if (ctx.IsKeyPressed(KeyboardKey.R))
            ResetFlag();
        if (ctx.IsKeyPressed(KeyboardKey.Three))
            StartDropDrape();
        if (ctx.IsKeyPressed(KeyboardKey.Four))
            StartDropCut();
        if (ctx.IsKeyPressed(KeyboardKey.Five))
            SetKatanaEdge(KatanaEdge.Up);
        if (ctx.IsKeyPressed(KeyboardKey.Six))
            SetKatanaEdge(KatanaEdge.Down);
        if (ctx.IsKeyPressed(KeyboardKey.B))
            DetonateAtClothCenter();
        if (ctx.IsKeyPressed(KeyboardKey.W))
            _cloth.ToggleWind();
        if (ctx.IsKeyPressed(KeyboardKey.One) && _cloth.Scenario == ClothScenario.Flag)
        {
            _pinMode = ClothPinMode.TopRow;
            _cloth.SetPinMode(_pinMode, _room);
        }

        if (ctx.IsKeyPressed(KeyboardKey.Two) && _cloth.Scenario == ClothScenario.Flag)
        {
            _pinMode = ClothPinMode.TopCorners;
            _cloth.SetPinMode(_pinMode, _room);
        }
    }

    private void ResetFlag()
    {
        _cloth.SpawnFlag(_room);
        _pinMode = ClothPinMode.TopRow;
        _camera.SnapTarget(_room.FloorCenter + new Vector3(0f, 2.2f, 0f));
    }

    private void StartDropDrape()
    {
        _cloth.SpawnDropDrape(_room, _katanaEdge);
        _camera.SnapTarget(_room.FloorCenter + new Vector3(0f, 1.7f, 0f));
    }

    private void StartDropCut()
    {
        _cloth.SpawnDropCut(_room, _katanaEdge);
        _camera.SnapTarget(_room.FloorCenter + new Vector3(0f, 1.7f, 0f));
    }

    private void SetKatanaEdge(KatanaEdge edge)
    {
        _katanaEdge = edge;
        if (_cloth.Scenario is ClothScenario.DropDrape or ClothScenario.DropCut)
        {
            if (_cloth.Scenario == ClothScenario.DropCut)
                StartDropCut();
            else
                StartDropDrape();
        }
    }

    private void DetonateAtClothCenter()
    {
        if (_cloth.Spheres.Count == 0)
            return;

        var sum = Vector3.Zero;
        foreach (var s in _cloth.Spheres)
            sum += s.Position;
        var center = sum / _cloth.Spheres.Count;
        _cloth.DetonateBlast(center, radius: 0.45f, impulseSpeed: 5.5f);
    }

    private void UpdateCamera(RayGameContext ctx)
    {
        if (ctx.IsMouseDown(MouseButton.Middle))
        {
            var delta = ctx.MouseDelta;
            const float sensitivity = 0.004f;
            _camera.AddLookDelta(-delta.X * sensitivity, -delta.Y * sensitivity);
        }

        _camera.Pitch = Math.Clamp(_camera.Pitch, 0.2f, 1.15f);
        _camera.AdjustDistance(Input.GetMouseWheelMove() * -0.7f);
    }

    private void TryImpulseFromClick(RayGameContext ctx, ViewPose pose)
    {
        if (!ctx.IsMousePressed(MouseButton.Left))
            return;

        var mouse = Input.GetMousePosition();
        var nx = (mouse.X / ctx.Width - 0.5f) * 2f;
        var ny = (0.5f - mouse.Y / ctx.Height) * 2f;
        var aspect = (float)ctx.Width / Math.Max(ctx.Height, 1);
        var (origin, direction) = BuildPickRay(pose, nx, ny, aspect);

        if (!ClothRenderer.TryPickParticle(origin, direction, _cloth.Spheres, 0.12f, out var best, out _))
            return;

        var impulseDir = Vector3.Normalize(direction + new Vector3(0f, 0.15f, 0f));
        _cloth.ApplyImpulse(best, impulseDir * 4f);
    }

    private static (Vector3 Origin, Vector3 Direction) BuildPickRay(ViewPose pose, float nx, float ny, float aspect)
    {
        var forward = Vector3.Normalize(pose.Target - pose.Position);
        var right = Vector3.Normalize(Vector3.Cross(forward, pose.Up));
        var up = Vector3.Normalize(Vector3.Cross(right, forward));
        var fovTan = MathF.Tan(pose.FieldOfViewDegrees * MathF.PI / 360f);
        var dir = Vector3.Normalize(forward + right * (nx * fovTan * aspect) + up * (ny * fovTan));
        return (pose.Position, dir);
    }

    private void DrawRoom(RayGameContext ctx)
    {
        ctx.DrawPlane(_room.FloorCenter, new Vector2(PlayRoom.GridSize, PlayRoom.GridSize), Floor);

        var half = PlayRoom.GridSize * PlayRoom.CellSize * 0.5f;
        for (var i = 0; i <= PlayRoom.GridSize; i++)
        {
            var t = i * PlayRoom.CellSize;
            ctx.DrawBolt(new Vector3(t, 0.02f, 0f), new Vector3(t, 0.02f, half * 2f), GridLine);
            ctx.DrawBolt(new Vector3(0f, 0.02f, t), new Vector3(half * 2f, 0.02f, t), GridLine);
        }

        var h = PlayRoom.WallHeight * 0.5f;
        for (var y = 0u; y < PlayRoom.GridSize; y++)
        for (var x = 0u; x < PlayRoom.GridSize; x++)
        {
            if (x != 0 && y != 0 && x != PlayRoom.GridSize - 1 && y != PlayRoom.GridSize - 1)
                continue;

            var cx = (x + 0.5f) * PlayRoom.CellSize;
            var cz = (y + 0.5f) * PlayRoom.CellSize;
            ctx.DrawShipWires(
                new Vector3(cx, h, cz),
                new Vector3(PlayRoom.CellSize, PlayRoom.WallHeight, PlayRoom.CellSize),
                WallWire);
        }
    }
}
