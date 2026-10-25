using System.Numerics;
using Novolis.Physics.Ballistics;
using Novolis.Raylib.Game;
using Novolis.Raylib.Interact;
using Novolis.Simulation.View;
using RayCamera = Novolis.Raylib.Rendering.Camera;

namespace ArtillerySimulator.Game;

internal sealed class ArtilleryCameras
{
    private const float MouseSensitivity = 0.0022f;
    private const float FreeMoveSpeed = 1800f;

    private readonly FreeLookCameraRig _free = new();
    private readonly OrbitCameraRig _orbit = new();

    private float _zoom = 1f;

    public CameraMode Mode { get; private set; } = CameraMode.Orbit;

    public void ToggleMode()
    {
        Mode = Mode == CameraMode.Freecam ? CameraMode.Orbit : CameraMode.Freecam;
        _orbit.ResetSmoothing();
    }

    public void SnapToGun(Vector3 gunPivot, Vector3 barrelDir)
    {
        var orbitTarget = gunPivot + new Vector3(0f, 2.2f, 0f);
        _orbit.MinDistance = 16f;
        _orbit.MaxDistance = 16_000f;
        _orbit.FieldOfViewDegrees = 36f;
        _orbit.SmoothRate = 3.5f;
        _orbit.SnapTarget(orbitTarget);
        _orbit.Yaw = MathF.Atan2(-barrelDir.X, -barrelDir.Z) + 0.7f;
        _orbit.Pitch = 0.52f;
        _orbit.Distance = 150f;
        _zoom = 1f;

        _free.Position = gunPivot + SimulationUnits.FixedCamEyeOffset;
        _free.Yaw = _orbit.Yaw + MathF.PI * 0.15f;
        _free.Pitch = -0.12f;
    }

    public void Update(RayGameContext ctx, Vector3 gunPivot, BallisticTrajectoryRunner shot)
    {
        if (Mode == CameraMode.Freecam)
            UpdateFreecam(ctx);
        else
            UpdateOrbit(ctx, gunPivot, shot);
    }

    public RayCamera Build(float deltaSeconds)
    {
        if (Mode == CameraMode.Orbit)
        {
            var pose = _orbit.BuildViewPose(deltaSeconds);
            return RayCamera.Perspective(pose.Position, pose.Target, pose.Up, pose.FieldOfViewDegrees);
        }

        var freePose = _free.BuildViewPose(SimulationUnits.FixedCamLookAhead);
        return RayCamera.Perspective(freePose.Position, freePose.Target, freePose.Up, freePose.FieldOfViewDegrees);
    }

    private void UpdateFreecam(RayGameContext ctx)
    {
        if (ctx.IsMouseDown(MouseButton.Right))
        {
            var delta = ctx.MouseDelta;
            _free.AddLookDelta(-delta.X * MouseSensitivity, -delta.Y * MouseSensitivity);
        }

        var move = Vector3.Zero;
        var forward = _free.GetLookDirection();
        var right = Vector3.Normalize(Vector3.Cross(forward, Vector3.UnitY));

        if (ctx.IsKeyDown(KeyboardKey.W))
            move += forward;
        if (ctx.IsKeyDown(KeyboardKey.S))
            move -= forward;
        if (ctx.IsKeyDown(KeyboardKey.A))
            move -= right;
        if (ctx.IsKeyDown(KeyboardKey.D))
            move += right;

        if (move.LengthSquared() > 1e-8f)
        {
            move = Vector3.Normalize(move) * (FreeMoveSpeed * ctx.DeltaSeconds);
            _free.MoveAlongLook(move);
        }
    }

    private void UpdateOrbit(RayGameContext ctx, Vector3 gunPivot, BallisticTrajectoryRunner shot)
    {
        Frame(gunPivot, shot);

        if (ctx.IsKeyDown(KeyboardKey.H))
            _zoom = Math.Clamp(_zoom - 0.85f * ctx.DeltaSeconds, 0.4f, 2.6f);
        if (ctx.IsKeyDown(KeyboardKey.M))
            _zoom = Math.Clamp(_zoom + 0.85f * ctx.DeltaSeconds, 0.4f, 2.6f);

        if (!ctx.IsMouseDown(MouseButton.Right))
            return;

        var delta = ctx.MouseDelta;
        _orbit.AddLookDelta(-delta.X * MouseSensitivity, -delta.Y * MouseSensitivity);
    }

    private void Frame(Vector3 gunPivot, BallisticTrajectoryRunner shot)
    {
        Vector3 focus;
        float desired;
        if (shot.Phase == BallisticTrajectoryPhase.Ready)
        {
            focus = gunPivot + new Vector3(0f, 3f, 0f);
            desired = 150f;
        }
        else
        {
            var flat = shot.CurrentPosition - gunPivot;
            flat.Y = 0f;
            var span = flat.Length();
            var altitude = MathF.Max(0f, shot.CurrentPosition.Y - gunPivot.Y);
            focus = Vector3.Lerp(gunPivot + new Vector3(0f, 6f, 0f), shot.CurrentPosition, 0.42f);
            focus.Y = MathF.Max(focus.Y, gunPivot.Y + altitude * 0.45f);
            desired = Math.Clamp(120f + span * 0.55f + altitude * 0.5f, 120f, 9_000f);
        }

        _orbit.Target = focus;
        _orbit.Distance = Math.Clamp(desired * _zoom, _orbit.MinDistance, _orbit.MaxDistance);
    }
}
