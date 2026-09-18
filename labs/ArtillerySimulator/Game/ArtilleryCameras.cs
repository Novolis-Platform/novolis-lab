using System.Numerics;
using Novolis.Physics.Ballistics;
using Novolis.Raylib.Game;
using Novolis.Raylib.Interact;
using Novolis.Simulation.View;
using RayCamera = Novolis.Raylib.Rendering.Camera;

namespace ArtillerySimulator.Game;

internal enum CameraMode
{
    Freecam,
    Orbit,
}

internal sealed class ArtilleryCameras
{
    private const float MouseSensitivity = 0.0022f;
    private const float FreeMoveSpeed = 1800f;

    private readonly FreeLookCameraRig _free = new();
    private readonly OrbitCameraRig _orbit = new();

    public CameraMode Mode { get; private set; } = CameraMode.Freecam;

    public void ToggleMode()
    {
        Mode = Mode == CameraMode.Freecam ? CameraMode.Orbit : CameraMode.Freecam;
        _orbit.ResetSmoothing();
    }

    public void SnapToGun(Vector3 gunPivot, Vector3 barrelDir)
    {
        var orbitTarget = gunPivot + new Vector3(0f, 4f, 0f);
        _orbit.SnapTarget(orbitTarget);
        _orbit.Yaw = MathF.Atan2(barrelDir.X, barrelDir.Z);
        _orbit.Pitch = 0.3f;
        _orbit.Distance = 500f;

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

    public RayCamera Build(float deltaSeconds, Vector3 gunPivot, BallisticTrajectoryRunner shot)
    {
        if (Mode == CameraMode.Orbit)
        {
            _orbit.Target = shot.Phase != BallisticTrajectoryPhase.Ready
                ? shot.CurrentPosition + new Vector3(0f, 6f, 0f)
                : gunPivot + new Vector3(0f, 4f, 0f);
            var pose = _orbit.BuildViewPose(deltaSeconds);
            return RayCamera.Perspective(pose.Position, pose.Target, pose.Up, pose.FieldOfViewDegrees);
        }

        var freePose = _free.BuildViewPose(SimulationUnits.FixedCamLookAhead);
        return RayCamera.Perspective(freePose.Position, freePose.Target, freePose.Up, freePose.FieldOfViewDegrees);
    }

    private void UpdateFreecam(RayGameContext ctx)
    {
        var delta = ctx.MouseDelta;
        _free.AddLookDelta(-delta.X * MouseSensitivity, -delta.Y * MouseSensitivity);

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
        _orbit.Target = shot.Phase != BallisticTrajectoryPhase.Ready
            ? shot.CurrentPosition + new Vector3(0f, 6f, 0f)
            : gunPivot + new Vector3(0f, 4f, 0f);

        var delta = ctx.MouseDelta;
        _orbit.AddLookDelta(-delta.X * MouseSensitivity, -delta.Y * MouseSensitivity);

        if (ctx.IsKeyDown(KeyboardKey.H))
            _orbit.AdjustDistance(-900f * ctx.DeltaSeconds);
        if (ctx.IsKeyDown(KeyboardKey.M))
            _orbit.AdjustDistance(900f * ctx.DeltaSeconds);
    }
}
