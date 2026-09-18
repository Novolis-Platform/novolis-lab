using System.Numerics;
using Novolis.Math.Geometry;
using Novolis.Raylib.Game;
using Novolis.Raylib.Interact;
using Novolis.Simulation.Kinematics;
using Novolis.Simulation.View;
using RayCamera = Novolis.Raylib.Rendering.Camera;

namespace DoomLite3D.Game;

internal sealed class PlayerController
{
    private const float MoveSpeed = 4.5f;
    private const float EyeHeight = 1.6f;
    private const float MouseSensitivity = 0.0022f;
    private const float PlayerRadius = 0.25f;
    private const float JumpSpeed = 6.5f;
    private const float Gravity = 22f;

    public readonly YawPitchController Camera = new();
    private float _verticalOffset;
    private float _verticalVelocity;

    public void Reset(LevelMap level)
    {
        var spawn = level.CellToWorld(level.PlayerSpawn.X, level.PlayerSpawn.Y);
        Camera.Position = new Vector3(spawn.X, 0f, spawn.Z);
        Camera.Yaw = 0f;
        Camera.Pitch = 0f;
        _verticalOffset = 0f;
        _verticalVelocity = 0f;
    }

    public void Update(RayGameContext ctx, LevelMap level, bool allowMovement)
    {
        var delta = ctx.MouseDelta;
        Camera.AddLookDelta(-delta.X * MouseSensitivity, -delta.Y * MouseSensitivity);

        if (!allowMovement)
            return;

        var move = Vector3.Zero;
        if (ctx.IsKeyDown(KeyboardKey.W))
            move += Camera.GetForwardXZ();
        if (ctx.IsKeyDown(KeyboardKey.S))
            move -= Camera.GetForwardXZ();
        if (ctx.IsKeyDown(KeyboardKey.A))
            move += Camera.GetRightXZ();
        if (ctx.IsKeyDown(KeyboardKey.D))
            move -= Camera.GetRightXZ();

        if (move.LengthSquared() > 1e-6f)
        {
            move = Vector3.Normalize(move) * (MoveSpeed * ctx.DeltaSeconds);
            var pos = new Vector3(Camera.Position.X, 0f, Camera.Position.Z);
            pos = PlanarAgent.Move(level.Walls, pos, move, PlayerRadius, LevelMap.CellSize);
            Camera.Position = pos;
        }

        UpdateJump(ctx);
    }

    private void UpdateJump(RayGameContext ctx)
    {
        var dt = ctx.DeltaSeconds;
        var grounded = _verticalOffset <= 0.001f && _verticalVelocity <= 0f;

        if (grounded && ctx.IsKeyPressed(KeyboardKey.Space))
        {
            _verticalVelocity = JumpSpeed;
            grounded = false;
        }

        if (!grounded)
        {
            _verticalVelocity -= Gravity * dt;
            _verticalOffset += _verticalVelocity * dt;
            if (_verticalOffset <= 0f)
            {
                _verticalOffset = 0f;
                _verticalVelocity = 0f;
            }
        }
    }

    public Vector3 EyePosition =>
        Camera.Position + new Vector3(0f, EyeHeight + _verticalOffset, 0f);

    public RayCamera BuildRaylibCamera()
    {
        var pose = new ViewPose(
            EyePosition,
            EyePosition + Camera.GetLookDirection() * 10f,
            Vector3.UnitY,
            70f);
        return RayCamera.Perspective(pose.Position, pose.Target, pose.Up, pose.FieldOfViewDegrees);
    }

    public Novolis.Math.Geometry.Ray GetLookRay()
    {
        var origin = EyePosition;
        var dir = Camera.GetLookDirection();
        return new Novolis.Math.Geometry.Ray(origin, dir);
    }
}
