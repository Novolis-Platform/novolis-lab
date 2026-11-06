using System.Numerics;
using Novolis.Physics.Ballistics;
using Novolis.Raylib.Game;
using Novolis.Raylib.Interact;
using Novolis.Simulation.View;
using Input = Novolis.Raylib.Interact.Input;
using RayCamera = Novolis.Raylib.Rendering.Camera;

namespace ArtillerySimulator.Game;

internal sealed class ArtilleryCameras
{
    private const float MouseSensitivity = 0.0022f;
    private const float FreeMoveSpeed = 1800f;

    private readonly FreeLookCameraRig _free = new();
    private readonly OrbitCameraRig _orbit = new();
    private Vector3 _battleFocus;
    private bool _trackBattle;
    private bool _lockMapOverview;

    public CameraMode Mode { get; private set; } = CameraMode.Orbit;

    public void ToggleMode() => Use(Mode == CameraMode.Freecam ? CameraMode.Orbit : CameraMode.Freecam);

    public void Use(CameraMode mode)
    {
        if (Mode == mode)
            return;

        Mode = mode;
        _orbit.ResetSmoothing();
    }

    public void ShowMapOverview(float eastMeters, float northMeters)
    {
        _trackBattle = false;
        _lockMapOverview = true;
        Mode = CameraMode.Orbit;
        var cx = eastMeters * 0.5f;
        var cz = northMeters * 0.5f;
        _orbit.MinDistance = 80f;
        _orbit.MaxDistance = 40_000f;
        _orbit.FieldOfViewDegrees = 42f;
        _orbit.SmoothRate = 8f;
        _orbit.SnapTarget(new Vector3(cx, KristiansandReferenceMap.TableYMeters, cz));
        _orbit.Distance = MathF.Max(eastMeters, northMeters) * 1.08f;
        _orbit.Pitch = 1.28f;
        _orbit.MinPitch = -0.12f;
        _orbit.Yaw = NorthFacingYaw;
    }

    /// <summary>Orbit south of the look target, facing north (Kommunekart-style harbor altitude).</summary>
    public void ShowKommunekartView(TerrainWorld terrain, Kommunekart3DView.KommunekartViewState view)
    {
        _trackBattle = false;
        Mode = CameraMode.Orbit;
        _orbit.MinDistance = 80f;
        _orbit.MaxDistance = 42_000f;
        _orbit.FieldOfViewDegrees = 42f;
        _orbit.SmoothRate = 8f;
        _orbit.MinPitch = -0.05f;

        var xz = KristiansandHeightSampler.ToWorldMeters(view.Latitude, view.Longitude);
        var ground = terrain.Field.SampleHeight(xz.X, xz.Y);
        _orbit.SnapTarget(new Vector3(xz.X, ground + 90f, xz.Y));

        var heightAboveGround = MathF.Max(400f, (float)view.CameraHeightMeters - ground);
        _orbit.Distance = Math.Clamp(heightAboveGround, 2_500f, 38_000f);

        var pitchDownDegrees = (float)Math.Clamp(-view.PitchDegrees, 22f, 72f);
        _orbit.Pitch = pitchDownDegrees * (MathF.PI / 180f);
        _orbit.Yaw = NorthFacingYaw;
    }

    /// <summary>Eye south of target; view toward +Z (north).</summary>
    private const float NorthFacingYaw = MathF.PI;

    public void SnapToGun(Vector3 gunPivot, Vector3 barrelDir)
    {
        _lockMapOverview = false;
        var orbitTarget = gunPivot + new Vector3(0f, 2.2f, 0f);
        _orbit.MinDistance = 8f;
        _orbit.MaxDistance = 16_000f;
        _orbit.FieldOfViewDegrees = 36f;
        _orbit.SmoothRate = 6f;
        _orbit.SnapTarget(orbitTarget);
        _orbit.Yaw = MathF.Atan2(-barrelDir.X, -barrelDir.Z) + 0.7f;
        _orbit.Pitch = 0.52f;
        _orbit.Distance = 150f;

        _free.Position = gunPivot + SimulationUnits.FixedCamEyeOffset;
        _free.Yaw = _orbit.Yaw + MathF.PI * 0.15f;
        _free.Pitch = -0.12f;
    }

    public void LookAtBattle(Vector3 gunPivot, Vector3 downrange)
    {
        _lockMapOverview = false;
        var mid = (gunPivot + downrange) * 0.5f;
        _battleFocus = new Vector3(mid.X, MathF.Max(gunPivot.Y, downrange.Y) + 60f, mid.Z);
        _trackBattle = true;
        var span = Vector2.Distance(new Vector2(gunPivot.X, gunPivot.Z), new Vector2(downrange.X, downrange.Z));
        _orbit.SnapTarget(_battleFocus);
        _orbit.Distance = Math.Clamp(span * 1.05f, 700f, 7_000f);
        _orbit.Pitch = 0.58f;
        var dir = downrange - gunPivot;
        _orbit.Yaw = MathF.Atan2(-dir.X, -dir.Z);
    }

    public void SetBattleFocus(Vector3 focus) => _battleFocus = focus;

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

        var wheel = Input.GetMouseWheelMove();
        if (MathF.Abs(wheel) > 0.001f)
            _free.MoveAlongLook(_free.GetLookDirection() * (wheel * 160f));

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
        ApplyOrbitZoom(ctx);
        Frame(gunPivot, shot);

        if (!ctx.IsMouseDown(MouseButton.Right))
            return;

        var delta = ctx.MouseDelta;
        _orbit.AddLookDelta(-delta.X * MouseSensitivity, -delta.Y * MouseSensitivity);
    }

    private void ApplyOrbitZoom(RayGameContext ctx)
    {
        var wheel = Input.GetMouseWheelMove();
        if (ctx.IsKeyDown(KeyboardKey.H))
            _orbit.Distance *= MathF.Exp(-1.8f * ctx.DeltaSeconds);
        if (ctx.IsKeyDown(KeyboardKey.M))
            _orbit.Distance *= MathF.Exp(1.8f * ctx.DeltaSeconds);
        if (MathF.Abs(wheel) > 0.001f)
            _orbit.Distance *= MathF.Pow(0.82f, wheel);

        _orbit.Distance = Math.Clamp(_orbit.Distance, _orbit.MinDistance, _orbit.MaxDistance);
    }

    private void Frame(Vector3 gunPivot, BallisticTrajectoryRunner shot)
    {
        if (_lockMapOverview)
            return;

        if (shot.Phase == BallisticTrajectoryPhase.Ready)
        {
            _orbit.SmoothRate = 6f;
            _orbit.Target = _trackBattle ? _battleFocus : gunPivot + new Vector3(0f, 3f, 0f);
            return;
        }

        var onShell = shot.Phase == BallisticTrajectoryPhase.Impacted && shot.Impact is { } hit
            ? hit.Position
            : shot.CurrentPosition;
        _orbit.SnapTarget(onShell);
    }
}
