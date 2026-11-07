using System.Numerics;
using Novolis.Physics.Ballistics;
using Novolis.Silk;
using Novolis.Simulation.View;

namespace ArtillerySimulator;

internal sealed class ArtilleryCameras
{
    private const float MouseSensitivity = 0.0022f;
    private const float FreeMoveSpeed = 1800f;
    private const float OrbitPanPixelsToMeters = 1.15f;

    private readonly FreeLookCameraRig _free = new();
    private readonly OrbitCameraRig _orbit = new();
    private Vector3 _battleFocus;
    private bool _trackBattle;
    private bool _lockMapOverview;
    private bool _userOrbitHold;

    public CameraMode Mode { get; private set; } = CameraMode.Orbit;

    /// <summary>
    /// When true, render with Cesium-matched look-at (no map X-flip) so Kommunekart head/pitch dumps keep N/S.
    /// </summary>
    public bool MatchKommunekartOrientation { get; private set; }

    public void ToggleMode()
    {
        MatchKommunekartOrientation = false;
        Use(Mode == CameraMode.Freecam ? CameraMode.Orbit : CameraMode.Freecam);
    }

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
        _userOrbitHold = false;
        MatchKommunekartOrientation = false;
        Mode = CameraMode.Orbit;
        var cx = eastMeters * 0.5f;
        var cz = northMeters * 0.5f;
        _orbit.MinDistance = 80f;
        _orbit.MaxDistance = 40_000f;
        _orbit.FieldOfViewDegrees = 42f;
        _orbit.SmoothRate = 8f;
        var ground = MathF.Max(8f, KristiansandHeightSampler.MaxElevationMeters * 0.35f);
        _orbit.SnapTarget(new Vector3(cx, ground, cz));
        _orbit.Distance = MathF.Max(eastMeters, northMeters) * 1.08f;
        _orbit.Pitch = 1.28f;
        _orbit.MinPitch = -0.12f;
        _orbit.Yaw = NorthFacingYaw;
    }

    public void ShowKommunekartView(TerrainWorld terrain, Kommunekart3DView.KommunekartViewState view)
    {
        _ = terrain;
        _trackBattle = false;
        _userOrbitHold = false;
        _lockMapOverview = false;
        // URL x/y/z is Cesium camera eye (fromDegrees(lon=y, lat=x, height=z)), not look-at.
        // FreeLook yaw 0 = north / pitch 0 = horizon — same as Cesium heading/pitch signs.
        MatchKommunekartOrientation = true;
        Mode = CameraMode.Freecam;

        var xz = KristiansandHeightSampler.ToWorldMeters(view.Latitude, view.Longitude);
        var eyeY = (float)view.CameraHeightMeters - KristiansandGeoref.GeoidUndulationMeters;
        _free.Position = new Vector3(xz.X, eyeY, xz.Y);
        _free.Yaw = (float)(view.HeadDegrees * (Math.PI / 180.0));
        _free.Pitch = (float)(view.PitchDegrees * (Math.PI / 180.0));
        _free.FieldOfViewDegrees = 42f;
    }

    private const float NorthFacingYaw = MathF.PI;

    public void SnapToGun(Vector3 gunPivot, Vector3 barrelDir)
    {
        _lockMapOverview = false;
        _userOrbitHold = false;
        MatchKommunekartOrientation = false;
        Mode = CameraMode.Orbit;
        var orbitTarget = gunPivot + new Vector3(0f, 2.2f, 0f);
        _orbit.MinDistance = 8f;
        _orbit.MaxDistance = 16_000f;
        _orbit.FieldOfViewDegrees = 42f;
        _orbit.SmoothRate = 6f;
        _orbit.SnapTarget(orbitTarget);
        // From Odderøya south tip: look north toward Kvadraturen (harbor).
        _orbit.Yaw = NorthFacingYaw;
        _orbit.Pitch = 0.55f;
        _orbit.Distance = 420f;
        _ = barrelDir;

        _free.Position = gunPivot + SimulationUnits.FixedCamEyeOffset;
        _free.Yaw = NorthFacingYaw + 0.1f;
        _free.Pitch = -0.12f;
    }

    public void LookAtBattle(Vector3 gunPivot, Vector3 downrange)
    {
        _lockMapOverview = false;
        _userOrbitHold = false;
        MatchKommunekartOrientation = false;
        Mode = CameraMode.Orbit;
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

    public void Update(SilkFrame frame, Vector3 gunPivot, BallisticTrajectoryRunner shot)
    {
        if (Mode == CameraMode.Freecam)
            UpdateFreecam(frame);
        else
            UpdateOrbit(frame, gunPivot, shot);
    }

    public CameraPose Build(float deltaSeconds)
    {
        if (Mode == CameraMode.Orbit)
        {
            var pose = _orbit.BuildViewPose(deltaSeconds);
            return new CameraPose(pose.Position, pose.Target, pose.Up, pose.FieldOfViewDegrees);
        }

        var freePose = _free.BuildViewPose(SimulationUnits.FixedCamLookAhead);
        return new CameraPose(freePose.Position, freePose.Target, freePose.Up, freePose.FieldOfViewDegrees);
    }

    private void UpdateFreecam(SilkFrame frame)
    {
        // Map X-flip (orbit) uses +mouseX; Cesium-matched freecam uses classic -mouseX.
        var lookMx = MatchKommunekartOrientation ? -frame.MouseDeltaX : frame.MouseDeltaX;
        if (frame.IsMouseButtonDown(MouseButton.Right))
            _free.AddLookDelta(lookMx * MouseSensitivity, -frame.MouseDeltaY * MouseSensitivity);

        if (frame.IsMouseButtonDown(MouseButton.Middle)
            || (frame.IsMouseButtonDown(MouseButton.Left) && (frame.IsKeyDown(Key.ShiftLeft) || frame.IsKeyDown(Key.ShiftRight))))
        {
            var forward = _free.GetLookDirection();
            var right = Vector3.Normalize(Vector3.Cross(forward, Vector3.UnitY));
            var up = Vector3.UnitY;
            var scale = FreeMoveSpeed * 0.0025f;
            var panMx = MatchKommunekartOrientation ? -frame.MouseDeltaX : frame.MouseDeltaX;
            _free.MoveAlongLook((right * panMx + up * frame.MouseDeltaY) * scale);
        }

        if (MathF.Abs(frame.ScrollDelta) > 0.001f)
            _free.MoveAlongLook(_free.GetLookDirection() * (frame.ScrollDelta * 160f));

        var move = Vector3.Zero;
        var look = _free.GetLookDirection();
        var strafe = Vector3.Normalize(Vector3.Cross(look, Vector3.UnitY));

        if (frame.IsKeyDown(Key.W))
            move += look;
        if (frame.IsKeyDown(Key.S))
            move -= look;
        if (frame.IsKeyDown(Key.A))
            move -= strafe;
        if (frame.IsKeyDown(Key.D))
            move += strafe;

        if (move.LengthSquared() > 1e-8f)
        {
            move = Vector3.Normalize(move) * (FreeMoveSpeed * frame.DeltaSeconds);
            _free.MoveAlongLook(move);
        }
    }

    private void UpdateOrbit(SilkFrame frame, Vector3 gunPivot, BallisticTrajectoryRunner shot)
    {
        ApplyOrbitZoom(frame);
        ApplyOrbitPan(frame);
        Frame(gunPivot, shot);

        if (!frame.IsMouseButtonDown(MouseButton.Right))
            return;

        _orbit.AddLookDelta(frame.MouseDeltaX * MouseSensitivity, -frame.MouseDeltaY * MouseSensitivity);
    }

    private void ApplyOrbitPan(SilkFrame frame)
    {
        var pan = frame.IsMouseButtonDown(MouseButton.Middle)
            || (frame.IsMouseButtonDown(MouseButton.Left)
                && (frame.IsKeyDown(Key.ShiftLeft) || frame.IsKeyDown(Key.ShiftRight)));
        if (!pan)
            return;

        // Ground-plane pan relative to current orbit view (screen-right / screen-up).
        var yaw = _orbit.Yaw;
        var right = new Vector3(MathF.Cos(yaw), 0f, -MathF.Sin(yaw));
        var forward = new Vector3(MathF.Sin(yaw), 0f, MathF.Cos(yaw));
        var scale = MathF.Max(40f, _orbit.Distance) * 0.0011f * OrbitPanPixelsToMeters;
        var delta = (right * frame.MouseDeltaX + forward * frame.MouseDeltaY) * scale;
        _orbit.Target += delta;
        _orbit.SnapTarget(_orbit.Target);
        _lockMapOverview = false;
        _trackBattle = false;
        _userOrbitHold = true;
    }

    private void ApplyOrbitZoom(SilkFrame frame)
    {
        if (frame.IsKeyDown(Key.H))
            _orbit.Distance *= MathF.Exp(-1.8f * frame.DeltaSeconds);
        if (frame.IsKeyDown(Key.N))
            _orbit.Distance *= MathF.Exp(1.8f * frame.DeltaSeconds);
        if (MathF.Abs(frame.ScrollDelta) > 0.001f)
            _orbit.Distance *= MathF.Pow(0.82f, frame.ScrollDelta);

        _orbit.Distance = Math.Clamp(_orbit.Distance, _orbit.MinDistance, _orbit.MaxDistance);
    }

    private void Frame(Vector3 gunPivot, BallisticTrajectoryRunner shot)
    {
        if (_lockMapOverview || _userOrbitHold)
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

    internal readonly record struct CameraPose(Vector3 Position, Vector3 Target, Vector3 Up, float FieldOfViewDegrees);
}
