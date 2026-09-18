using System.Globalization;
using System.Numerics;
using KatoriLab.Ui;
using Novolis.Simulation.Humanoid;

namespace KatoriLab.Demo;

/// <summary>
/// Continuous ken timeline → spine/stance FK + blade path + <see cref="HumanoidFullBodyIk"/> holds.
/// Two-hand kamae: fit ken for reach/head clearance, IK lock, then snap tsuka onto the hands.
/// </summary>
internal sealed class KatoriKataDriver
{
    const float StanceHalfWidth = 0.30f;
    const float StanceDepth = 0.24f;

    readonly HumanoidBindPose _bind;
    readonly HumanoidPose _pose = new();
    readonly KenHoldSet _holds;
    readonly Vector3 _bindHips;
    HumanoidWorldPose _world;
    float _time;
    Matrix4x4 _weaponWorld = Matrix4x4.Identity;
    Vector3 _kashira;
    Vector3 _tsuba;
    Vector3 _kissaki;
    Vector3 _holdPrimaryWorld;
    Vector3 _holdSecondaryWorld;

    public KatoriKataDriver()
    {
        _bind = HumanoidBindPose.CreateDefaultTPose(1.72f);
        _bindHips = _bind[HumanoidBone.Hips];
        _holds = KenHoldSet.ForCenteredBokken(KenTimeline.BokkenLength);
        _world = HumanoidPoseSolver.SolveWorld(_bind, HumanoidPose.FromBind(_bind));
        HoldMode = true;
        Seek(0f); // start at the door
    }

    public bool Paused { get; set; }
    public bool HoldMode { get; set; }
    public string Phase { get; private set; } = "Chūdan-no-kamae";
    public float TimeSeconds => _time;
    public float DurationSeconds => KenTimeline.Duration;
    public HumanoidBindPose Bind => _bind;
    public HumanoidWorldPose World => _world;
    public KenHoldSet Holds => _holds;
    public string SkinSource => "ken-timeline";
    public string ClipId => KatoriKataClips.ClipId;
    public Vector3 Kashira => _kashira;
    public Vector3 Tsuba => _tsuba;
    public Vector3 Kissaki => _kissaki;
    public Vector3 HoldPrimaryWorld => _holdPrimaryWorld;
    public Vector3 HoldSecondaryWorld => _holdSecondaryWorld;

    public void Tick(float dt)
    {
        if (!Paused)
            _time = (_time + dt) % KenTimeline.Duration;
        ApplyFrame();
    }

    public void Seek(float timeSeconds)
    {
        _time = MathF.Max(0f, timeSeconds);
        ApplyFrame();
    }

    public void SeekPhase(string phase) => Seek(KenTimeline.TimeForPhase(phase));

    public void Paint(StickFigurePane front, StickFigurePane side)
    {
        PaintPane(front, StickViewMode.FrontXy, "Front — full dojo kata");
        PaintPane(side, StickViewMode.SideZy, "Side — full dojo kata");
    }

    void PaintPane(StickFigurePane pane, StickViewMode mode, string caption)
    {
        pane.ViewMode = mode;
        pane.Caption = $"{caption}  ·  {Phase}  t={_time:0.0}s";
        pane.ClearExtras();
        pane.SetBoneGuides(HumanoidDebugDraw.BuildSegments(_world));
        pane.SetMannequin(MannequinBuilder.FromWorldPose(_world), MannequinBuilder.HeadCenter(_world));

        var joints = new Vector3[(int)HumanoidBone.Count];
        for (var i = 0; i < joints.Length; i++)
            joints[i] = _world.Position((HumanoidBone)i);
        pane.SetJointDots(joints);

        if (!HoldMode)
            return;

        var sample = KenTimeline.Evaluate(_time);
        var blade = _kissaki - _kashira;
        var side = MathF.Abs(Vector3.Dot(Vector3.Normalize(blade), Vector3.UnitY)) > 0.9f
            ? Vector3.UnitX
            : Vector3.Normalize(Vector3.Cross(blade, Vector3.UnitY));
        var segs = new List<Vector3>
        {
            _kashira, _kissaki,
            // Tsuba cross (handguard)
            _tsuba - side * 0.07f, _tsuba + side * 0.07f,
            _tsuba - Vector3.Normalize(Vector3.Cross(side, blade)) * 0.07f,
            _tsuba + Vector3.Normalize(Vector3.Cross(side, blade)) * 0.07f,
            _holdPrimaryWorld, _holdPrimaryWorld + new Vector3(0.05f, 0f, 0f),
            _holdPrimaryWorld, _holdPrimaryWorld + new Vector3(0f, 0.05f, 0f),
            _world.Position(HumanoidBone.RightHand), _holdPrimaryWorld,
        };
        if (sample.TwoHand >= 0.35f)
        {
            segs.Add(_holdSecondaryWorld);
            segs.Add(_holdSecondaryWorld + new Vector3(0.05f, 0f, 0f));
            segs.Add(_holdSecondaryWorld);
            segs.Add(_holdSecondaryWorld + new Vector3(0f, 0.05f, 0f));
            segs.Add(_world.Position(HumanoidBone.LeftHand));
            segs.Add(_holdSecondaryWorld);
        }

        pane.SetOverlaySegments(segs.ToArray());
    }

    public PoseSampleReport SamplePose() =>
        new(
            Phase,
            _time,
            _world.Position(HumanoidBone.Hips),
            _world.Position(HumanoidBone.Head),
            _world.Position(HumanoidBone.LeftHand),
            _world.Position(HumanoidBone.RightHand),
            _world.Position(HumanoidBone.LeftFoot),
            _world.Position(HumanoidBone.RightFoot),
            _kashira,
            _kissaki);

    public HoldLockReport SampleHolds()
    {
        var rHand = _world.Position(HumanoidBone.RightHand);
        var lHand = _world.Position(HumanoidBone.LeftHand);
        var sample = KenTimeline.Evaluate(_time);
        var twoHand = HoldMode && sample.TwoHand > 0.35f;
        return new HoldLockReport(
            Phase, _time,
            _holdPrimaryWorld, _holdSecondaryWorld,
            rHand, lHand,
            twoHand ? Vector3.Distance(rHand, _holdPrimaryWorld) : 0f,
            twoHand ? Vector3.Distance(lHand, _holdSecondaryWorld) : 0f);
    }

    public string Diagnose()
    {
        var hips = _world.Position(HumanoidBone.Hips);
        var spine = _world.Position(HumanoidBone.Spine2);
        var head = _world.Position(HumanoidBone.Head);
        var rShoulder = _world.Position(HumanoidBone.RightShoulder);
        var lShoulder = _world.Position(HumanoidBone.LeftShoulder);
        var rHand = _world.Position(HumanoidBone.RightHand);
        var lHand = _world.Position(HumanoidBone.LeftHand);
        var rElbow = _world.Position(HumanoidBone.RightForeArm);
        var lElbow = _world.Position(HumanoidBone.LeftForeArm);
        var blade = _kissaki - _kashira;
        var bladeLen = blade.Length();
        var bladeDir = bladeLen > 1e-4f ? blade / bladeLen : Vector3.UnitZ;
        var forwardDot = Vector3.Dot(bladeDir, Vector3.UnitZ);
        var upDot = Vector3.Dot(bladeDir, Vector3.UnitY);
        var gripSpan = Vector3.Distance(_holdPrimaryWorld, _holdSecondaryWorld);
        var handSpan = Vector3.Distance(rHand, lHand);
        var rReach = Vector3.Distance(rShoulder, rHand);
        var lReach = Vector3.Distance(lShoulder, lHand);
        var rArmLen = Vector3.Distance(_bind[HumanoidBone.RightArm], _bind[HumanoidBone.RightHand]);
        var lArmLen = Vector3.Distance(_bind[HumanoidBone.LeftArm], _bind[HumanoidBone.LeftHand]);
        var holds = SampleHolds();
        var tipVsSpine = _kissaki - spine;
        var elbowsBelowShoulders = rElbow.Y < rShoulder.Y - 0.02f && lElbow.Y < lShoulder.Y - 0.02f;
        var s = KenTimeline.Evaluate(_time);

        return string.Create(CultureInfo.InvariantCulture,
            $"phase={Phase}; t={_time:0.###}; bladeLen={bladeLen:0.###}; forwardDot={forwardDot:0.###}; upDot={upDot:0.###}; tipY={_kissaki.Y:0.###}; tipZ={_kissaki.Z:0.###}; tipVsSpine=({tipVsSpine.X:0.###},{tipVsSpine.Y:0.###},{tipVsSpine.Z:0.###}); gripSpan={gripSpan:0.###}; handSpan={handSpan:0.###}; rErr={holds.RightHandError:0.####}; lErr={holds.LeftHandError:0.####}; rReach={rReach:0.###}/{rArmLen:0.###}; lReach={lReach:0.###}/{lArmLen:0.###}; elbowsDown={elbowsBelowShoulders}; stance={s.Stance:0.##}; twoHand={s.TwoHand:0.##}; walk={s.Walk:0.##}; rootZ={s.RootOffset.Z:0.##}; hipsY={hips.Y:0.###}; headY={head.Y:0.###}; rHand=({rHand.X:0.###},{rHand.Y:0.###},{rHand.Z:0.###}); lHand=({lHand.X:0.###},{lHand.Y:0.###},{lHand.Z:0.###})");
    }

    public BoneTravelReport MeasureBoneTravel(float timeA, float timeB)
    {
        var saved = _time;
        Seek(timeA);
        var wa = CloneTips();
        Seek(timeB);
        var wb = CloneTips();
        Seek(saved);

        float D(Vector3 a, Vector3 b) => Vector3.Distance(a, b);
        return new BoneTravelReport(
            KenTimeline.PhaseName(timeA), KenTimeline.PhaseName(timeB), timeA, timeB,
            D(wa.Head, wb.Head), D(wa.RightHand, wb.RightHand), D(wa.LeftHand, wb.LeftHand),
            D(wa.RightFoot, wb.RightFoot), D(wa.LeftFoot, wb.LeftFoot),
            D(wa.Hips, wb.Hips), D(wa.Spine2, wb.Spine2));
    }

    public VertexDeltaReport MeasureVertexDelta(float timeA, float timeB)
    {
        var travel = MeasureBoneTravel(timeA, timeB);
        var tips = new[]
        {
            travel.Head, travel.RightHand, travel.LeftHand,
            travel.RightFoot, travel.LeftFoot, travel.Hips, travel.Spine2,
        };
        return new VertexDeltaReport(
            travel.PhaseA, travel.PhaseB, timeA, timeB,
            tips.Max(), tips.Average(),
            MathF.Max(travel.RightHand, MathF.Max(travel.LeftHand, travel.Head)),
            (travel.RightFoot + travel.LeftFoot) * 0.5f,
            _bind[HumanoidBone.Head].Y);
    }

    void ApplyFrame()
    {
        var sample = KenTimeline.Evaluate(_time);
        Phase = sample.Label;

        ApplyBody(sample);
        _world = HumanoidPoseSolver.SolveWorld(_bind, _pose);

        ApplyStanceFeet(sample);
        // Spine/hips moved — re-read body frame for blade.
        PlaceWeapon(sample);
        if (HoldMode)
        {
            if (sample.TwoHand >= 0.35f)
            {
                FitWeaponReachAndClear(sample);
                LockHands(sample);
                SnapKenToHands(sample);
                RelockTwoHands();
                // Snap can drift tip/hands toward the skull — nudge clear and re-seat grips.
                if (HeadClearanceDeficit() > 0f)
                {
                    FitWeaponReachAndClear(sample);
                    LockHands(sample);
                    SnapKenToHands(sample);
                    RelockTwoHands();
                }
            }
            else
            {
                LockHands(sample);
            }
        }

        HumanoidPoseSolver.BakeLocal(_bind, _world, _pose);
    }

    void ApplyBody(KenTimeline.Sample s)
    {
        // Fresh bind each frame so arm IK starts from a clean T-pose shoulder chain.
        for (var i = 0; i < (int)HumanoidBone.Count; i++)
            _pose[(HumanoidBone)i] = Quaternion.Identity;

        _pose.RootTranslation = _bindHips + s.RootOffset;

        // Facing / lean: yaw around Y, pitch around X (local after yaw).
        var spine = Quaternion.CreateFromAxisAngle(Vector3.UnitY, s.SpineYaw) *
                    Quaternion.CreateFromAxisAngle(Vector3.UnitX, s.SpinePitch);
        _pose[HumanoidBone.Hips] = Quaternion.CreateFromAxisAngle(Vector3.UnitY, s.SpineYaw * 0.55f);
        _pose[HumanoidBone.Spine] = spine;
        _pose[HumanoidBone.Spine1] = Quaternion.CreateFromAxisAngle(Vector3.UnitX, s.SpinePitch * 0.35f) *
                                     Quaternion.CreateFromAxisAngle(Vector3.UnitY, s.SpineYaw * 0.15f);
        _pose[HumanoidBone.Spine2] = Quaternion.CreateFromAxisAngle(Vector3.UnitX, s.SpinePitch * 0.20f);
        _pose[HumanoidBone.Neck] = Quaternion.CreateFromAxisAngle(Vector3.UnitX, s.HeadPitch * 0.5f);
        _pose[HumanoidBone.Head] = Quaternion.CreateFromAxisAngle(Vector3.UnitX, s.HeadPitch);
    }

    void ApplyStanceFeet(KenTimeline.Sample s)
    {
        var hips = _world.Position(HumanoidBone.Hips);
        var yaw = s.SpineYaw;
        var forward = new Vector3(MathF.Sin(yaw), 0f, MathF.Cos(yaw));
        var right = new Vector3(MathF.Cos(yaw), 0f, -MathF.Sin(yaw));

        // Heisoku → migi-hanmi; while walking, alternate a short step cycle.
        var narrowR = hips + right * 0.11f;
        var narrowL = hips - right * 0.11f;
        var hanmiR = hips + right * (StanceHalfWidth * 0.55f) + forward * StanceDepth;
        var hanmiL = hips - right * StanceHalfWidth - forward * (StanceDepth * 0.35f);
        var rightFoot = Vector3.Lerp(narrowR, hanmiR, s.Stance);
        var leftFoot = Vector3.Lerp(narrowL, hanmiL, s.Stance);

        if (s.Walk > 0.05f)
        {
            var step = MathF.Sin(_time * MathF.PI * 1.7f) * s.Walk;
            var stride = 0.16f * s.Walk;
            rightFoot += forward * (step * stride);
            leftFoot -= forward * (step * stride);
            // Soft lift on the swinging foot.
            rightFoot.Y = 0.02f + MathF.Max(0f, step) * 0.04f * s.Walk;
            leftFoot.Y = 0.02f + MathF.Max(0f, -step) * 0.04f * s.Walk;
        }
        else
        {
            rightFoot.Y = 0.02f;
            leftFoot.Y = 0.02f;
        }

        var targets = HumanoidFullBodyIkTargets.WithDefaults();
        targets.RightFoot = rightFoot;
        targets.LeftFoot = leftFoot;
        targets.RightFootPole = forward;
        targets.LeftFootPole = forward;
        HumanoidFullBodyIk.Apply(_world, _bind, targets);
    }

    void PlaceWeapon(KenTimeline.Sample s)
    {
        var hips = _world.Position(HumanoidBone.Hips);
        var yaw = Quaternion.CreateFromAxisAngle(Vector3.UnitY, s.SpineYaw);
        var gripMid = hips + Vector3.Transform(s.GripLocal, yaw);
        var tipDir = Vector3.Normalize(Vector3.Transform(s.TipDir, yaw));
        // Align blade +Z to tipDir; map grip-mid local → gripMid world (correct hand spacing).
        var basis = KenBasis(tipDir);
        var midLocal = _holds.GripMidLocal;
        _weaponWorld = basis * Matrix4x4.CreateTranslation(gripMid - Vector3.Transform(midLocal, basis));
        RefreshHoldsFromWeapon();
    }

    void LockHands(KenTimeline.Sample s)
    {
        var targets = HumanoidFullBodyIkTargets.WithDefaults();
        var cut = Phase.Contains("Kesagiri", StringComparison.Ordinal)
                  || Phase.Contains("Cutting", StringComparison.Ordinal);
        var jodan = Phase.Contains("Jōdan", StringComparison.Ordinal)
                    || Phase.Contains("jodan", StringComparison.OrdinalIgnoreCase);
        // Elbows out / down so forearms don't collapse through the skull in jōdan.
        var poleY = cut ? -0.55f : jodan ? -0.35f : -1.0f;
        var poleOut = jodan ? 0.75f : 0.45f;
        targets.RightHandPole = new Vector3(poleOut, poleY, 0.35f);
        targets.LeftHandPole = new Vector3(-poleOut, poleY, 0.35f);

        var leftHang = HangTarget(left: true);
        var rightHang = HangTarget(left: false);

        if (s.TwoHand >= 0.35f)
        {
            targets.RightHand = _holdPrimaryWorld;
            targets.LeftHand = _holdSecondaryWorld;
        }
        else if (s.TwoHand > 0.05f)
        {
            var u = Math.Clamp(s.TwoHand / 0.35f, 0f, 1f);
            targets.RightHand = Vector3.Lerp(rightHang, _holdPrimaryWorld, u);
            targets.LeftHand = Vector3.Lerp(leftHang, _holdSecondaryWorld, u * 0.35f);
        }
        else
        {
            targets.RightHand = _holdPrimaryWorld;
            targets.LeftHand = leftHang;
            targets.LeftHandPole = new Vector3(-0.2f, -1f, 0.15f);
            targets.RightHandPole = new Vector3(0.25f, -1f, 0.15f);
        }

        HumanoidFullBodyIk.Apply(_world, _bind, targets);
    }

    /// <summary>Translate the whole ken until grips/tsuba clear the skull and sit in arm reach.</summary>
    void FitWeaponReachAndClear(KenTimeline.Sample s)
    {
        var head = _world.Position(HumanoidBone.Head) + new Vector3(0f, 0.06f, 0f);
        const float clearR = 0.17f;
        var rLen = Vector3.Distance(_bind[HumanoidBone.RightArm], _bind[HumanoidBone.RightHand]) * 0.94f;
        var lLen = Vector3.Distance(_bind[HumanoidBone.LeftArm], _bind[HumanoidBone.LeftHand]) * 0.94f;
        var rShoulder = _world.Position(HumanoidBone.RightShoulder);
        var lShoulder = _world.Position(HumanoidBone.LeftShoulder);
        var forward = new Vector3(MathF.Sin(s.SpineYaw), 0f, MathF.Cos(s.SpineYaw));
        var clearBias = Vector3.Normalize(-forward * 0.85f + Vector3.UnitY * 0.55f);

        for (var i = 0; i < 14; i++)
        {
            RefreshHoldsFromWeapon();
            var primary = _holdPrimaryWorld;
            var secondary = _holdSecondaryWorld;
            var mid = (primary + secondary) * 0.5f;
            var delta = Vector3.Zero;

            foreach (var pt in new[] { primary, secondary, mid, _tsuba })
            {
                var v = pt - head;
                var d = v.Length();
                if (d >= clearR)
                    continue;
                var push = d < 1e-5f ? clearBias : Vector3.Normalize(v + clearBias);
                delta += push * (clearR - d + 0.02f);
            }

            var rDist = Vector3.Distance(rShoulder, primary);
            if (rDist > rLen)
                delta += Vector3.Normalize(rShoulder - primary) * (rDist - rLen);
            var lDist = Vector3.Distance(lShoulder, secondary);
            if (lDist > lLen)
                delta += Vector3.Normalize(lShoulder - secondary) * (lDist - lLen);

            if (delta.LengthSquared() < 1e-8f)
                break;
            _weaponWorld *= Matrix4x4.CreateTranslation(delta * 0.55f);
        }

        RefreshHoldsFromWeapon();
    }

    float HeadClearanceDeficit()
    {
        var head = _world.Position(HumanoidBone.Head) + new Vector3(0f, 0.06f, 0f);
        const float clearR = 0.17f;
        var mid = (_holdPrimaryWorld + _holdSecondaryWorld) * 0.5f;
        var worst = 0f;
        foreach (var p in new[] { _holdPrimaryWorld, _holdSecondaryWorld, mid, _tsuba })
        {
            var d = Vector3.Distance(p, head);
            if (d < clearR)
                worst = MathF.Max(worst, clearR - d);
        }

        return worst;
    }

    /// <summary>Seat ken on the hands: left@kashira grip, right@tsuba grip, blade along the tsuka.</summary>
    void SnapKenToHands(KenTimeline.Sample s)
    {
        var right = _world.Position(HumanoidBone.RightHand);
        var left = _world.Position(HumanoidBone.LeftHand);
        var along = right - left;
        if (along.LengthSquared() < 1e-6f)
            return;

        var handDir = Vector3.Normalize(along);
        var intended = Vector3.Normalize(Vector3.Transform(s.TipDir,
            Quaternion.CreateFromAxisAngle(Vector3.UnitY, s.SpineYaw)));
        if (Vector3.Dot(handDir, intended) < 0f)
            handDir = -handDir;

        // Hands own the tsuka axis (near-zero gripΔ); tip follows the same straight ken.
        var basis = KenBasis(handDir);
        var secondaryLocal = _holds.SecondaryGrip.LocalPosition;
        _weaponWorld = basis * Matrix4x4.CreateTranslation(left - Vector3.Transform(secondaryLocal, basis));
        RefreshHoldsFromWeapon();

        var fix = (right - _holdPrimaryWorld + (left - _holdSecondaryWorld)) * 0.5f;
        _weaponWorld *= Matrix4x4.CreateTranslation(fix);
        RefreshHoldsFromWeapon();
    }

    void RelockTwoHands()
    {
        var targets = HumanoidFullBodyIkTargets.WithDefaults();
        targets.RightHandPole = new Vector3(0.75f, -0.35f, 0.35f);
        targets.LeftHandPole = new Vector3(-0.75f, -0.35f, 0.35f);
        targets.RightHand = _holdPrimaryWorld;
        targets.LeftHand = _holdSecondaryWorld;
        HumanoidFullBodyIk.Apply(_world, _bind, targets);
    }

    Vector3 HangTarget(bool left)
    {
        var hips = _world.Position(HumanoidBone.Hips);
        var yaw = KenTimeline.Evaluate(_time).SpineYaw;
        var right = new Vector3(MathF.Cos(yaw), 0f, -MathF.Sin(yaw));
        var forward = new Vector3(MathF.Sin(yaw), 0f, MathF.Cos(yaw));
        var side = left ? -right : right;
        // Beside the thigh, slightly forward — natural idle hang, not T-pose.
        return hips + side * 0.20f + forward * 0.04f + new Vector3(0f, -0.12f, 0f);
    }

    void RefreshHoldsFromWeapon()
    {
        _holdPrimaryWorld = _holds.World(_holds.PrimaryGrip, _weaponWorld);
        _holdSecondaryWorld = _holds.World(_holds.SecondaryGrip, _weaponWorld);
        _kashira = _holds.World(_holds.Kashira, _weaponWorld);
        _tsuba = _holds.World(_holds.Tsuba, _weaponWorld);
        _kissaki = _holds.World(_holds.Kissaki, _weaponWorld);
    }

    (Vector3 Hips, Vector3 Head, Vector3 RightHand, Vector3 LeftHand, Vector3 RightFoot, Vector3 LeftFoot, Vector3 Spine2) CloneTips() =>
        (_world.Position(HumanoidBone.Hips),
            _world.Position(HumanoidBone.Head),
            _world.Position(HumanoidBone.RightHand),
            _world.Position(HumanoidBone.LeftHand),
            _world.Position(HumanoidBone.RightFoot),
            _world.Position(HumanoidBone.LeftFoot),
            _world.Position(HumanoidBone.Spine2));

    static Matrix4x4 KenBasis(Vector3 bladeDir)
    {
        bladeDir = Vector3.Normalize(bladeDir);
        var up = MathF.Abs(Vector3.Dot(bladeDir, Vector3.UnitY)) > 0.98f ? Vector3.UnitX : Vector3.UnitY;
        var x = Vector3.Normalize(Vector3.Cross(up, bladeDir));
        var y = Vector3.Cross(bladeDir, x);
        return new Matrix4x4(
            x.X, x.Y, x.Z, 0,
            y.X, y.Y, y.Z, 0,
            bladeDir.X, bladeDir.Y, bladeDir.Z, 0,
            0, 0, 0, 1);
    }

    static Matrix4x4 KenWorldMatrix(Vector3 kashira, Vector3 kissaki)
    {
        var mid = (kashira + kissaki) * 0.5f;
        var dir = kissaki - kashira;
        if (dir.LengthSquared() < 1e-8f)
            dir = Vector3.UnitZ;
        return KenBasis(Vector3.Normalize(dir)) * Matrix4x4.CreateTranslation(mid);
    }
}
