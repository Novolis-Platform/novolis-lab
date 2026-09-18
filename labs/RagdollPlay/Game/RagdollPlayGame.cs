using System.Drawing;
using System.Numerics;
using Novolis.Raylib.Game;
using Novolis.Raylib.Interact;
using Novolis.Simulation.View;
using Input = Novolis.Raylib.Interact.Input;
using RayCamera = Novolis.Raylib.Rendering.Camera;

namespace RagdollPlay.Game;

internal sealed class RagdollPlayGame
{
    private static readonly Color Background = Color.FromArgb(255, 22, 26, 34);
    private static readonly Color Floor = Color.FromArgb(255, 48, 52, 62);
    private static readonly Color GridLine = Color.FromArgb(255, 68, 78, 95);
    private static readonly Color WallWire = Color.FromArgb(255, 80, 95, 120);
    private static readonly Color HudText = Color.FromArgb(255, 210, 220, 235);

    private readonly DiagnosticsOverlay _diagnostics = new();
    private readonly OrbitCameraRig _camera = new();

    private PlayRoom _room = null!;
    private RagdollBody _ragdoll = null!;

    public void Initialize(RayGameContext ctx)
    {
        _room = PlayRoom.Create();
        _ragdoll = new RagdollBody();
        ResetRagdoll();
        _camera.SnapTarget(_room.FloorCenter + new Vector3(0f, 1f, 0f));
        _camera.Distance = 10f;
        _camera.MinDistance = 5f;
        _camera.MaxDistance = 16f;
        _camera.Yaw = 0.85f;
        _camera.Pitch = 0.48f;
        _camera.FieldOfViewDegrees = 50f;
    }

    public void Update(RayGameContext ctx)
    {
        UpdateCamera(ctx);
        if (ctx.IsKeyPressed(KeyboardKey.R))
            ResetRagdoll();

        var pose = _camera.BuildViewPose(ctx.DeltaSeconds);
        FollowRagdoll(pose, ctx.DeltaSeconds);
        TryImpulseFromClick(ctx, pose);
        _diagnostics.ToggleIfKeyPressed(ctx);
        _ragdoll.Step(_room.CollisionWorld, ctx.DeltaSeconds);

        ctx.Clear(Background);
        var camera = RayCamera.Perspective(pose.Position, pose.Target, pose.Up, pose.FieldOfViewDegrees);
        ctx.BeginWorld(camera);
        DrawRoom(ctx);
        PainterDollRenderer.Draw(ctx, _ragdoll.Spheres);
        ctx.EndWorld();

        ctx.Text("LMB shove  |  MMB orbit  |  wheel zoom  |  R reset  |  F3 diag", 16, 16, 18, HudText);
        _diagnostics.Draw(ctx, (_, lines) =>
        {
            lines.Add($"bones {RagdollIndices.Count}  joints {_ragdoll.Joints.Count}");
            lines.Add($"joint {_ragdoll.LastJointCorrections}  angle {_ragdoll.LastAngularCorrections}  self {_ragdoll.LastInternalFixes}");
        });
    }

    private void FollowRagdoll(ViewPose pose, float deltaSeconds)
    {
        _ = pose;
        if (_ragdoll.Spheres.Count < RagdollIndices.Count)
            return;

        var hip = _ragdoll.Spheres[RagdollIndices.Hip].Position;
        var t = 1f - MathF.Exp(-4f * MathF.Max(deltaSeconds, 1e-4f));
        _camera.Target = Vector3.Lerp(_camera.Target, hip + new Vector3(0f, 0.5f, 0f), t * 0.15f);
    }

    private void ResetRagdoll()
    {
        var spawn = _room.FloorCenter + new Vector3(0f, 0.05f, 0f);
        _ragdoll.SpawnStanding(spawn, _room);
        _camera.SnapTarget(_ragdoll.Spheres[RagdollIndices.Hip].Position + new Vector3(0f, 0.6f, 0f));
    }

    private void UpdateCamera(RayGameContext ctx)
    {
        if (ctx.IsMouseDown(MouseButton.Middle))
        {
            var delta = ctx.MouseDelta;
            const float sensitivity = 0.004f;
            _camera.AddLookDelta(-delta.X * sensitivity, -delta.Y * sensitivity);
        }

        _camera.Pitch = Math.Clamp(_camera.Pitch, 0.25f, 1.1f);
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

        if (!PainterDollRenderer.TryPickBone(origin, direction, _ragdoll.Spheres, 0.14f, out var best, out _))
            return;

        var impulseDir = Vector3.Normalize(direction + new Vector3(0f, 0.2f, 0f));
        _ragdoll.ApplyImpulse(best, impulseDir * 4.5f);
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
