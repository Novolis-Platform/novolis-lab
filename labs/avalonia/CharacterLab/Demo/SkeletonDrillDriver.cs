using System.Numerics;
using Novolis.Game.Humanoid;
using Novolis.Simulation.Humanoid;

namespace CharacterLab.Demo;

/// <summary>
/// Skeleton-first drill: FK clip + <see cref="HumanoidFullBodyIk"/> hands locked to rifle hold points.
/// No character/weapon meshes — wire sticks + rifle gizmo only until motion reads correctly.
/// </summary>
internal sealed class SkeletonDrillDriver
{
    const float RifleLengthMeters = 1.05f;

    readonly HumanoidBindPose _bind;
    readonly HumanoidClipBank _bank;
    readonly HumanoidPose _pose = new();
    readonly WeaponHoldSet _holds;
    HumanoidWorldPose _world;
    float _time;
    Matrix4x4 _weaponWorld = Matrix4x4.Identity;
    Vector3 _rifleButt;
    Vector3 _rifleTip;
    Vector3 _holdPrimaryWorld;
    Vector3 _holdSecondaryWorld;

    public SkeletonDrillDriver()
    {
        _bind = HumanoidBindPose.CreateDefaultTPose(1.72f);
        _bank = DrillClips.CreateBank(_bind);
        _holds = WeaponHoldSet.ForCenteredLongGun(mesh: null!, RifleLengthMeters);
        _world = HumanoidPoseSolver.SolveWorld(_bind, HumanoidPose.FromBind(_bind));
        Seek(0.6f);
    }

    public bool Paused { get; set; }
    public string Phase { get; private set; } = "Order Arms";
    public float TimeSeconds => _time;
    public HumanoidBindPose Bind => _bind;
    public HumanoidWorldPose World => _world;
    public WeaponHoldSet Holds => _holds;
    public string SkinSource => "skeleton-wire (no mesh)";

    public void Tick(float dt)
    {
        if (!Paused)
            _time += dt;
        ApplyFrame();
    }

    public void Seek(float timeSeconds)
    {
        _time = timeSeconds;
        ApplyFrame();
    }

    public void SeekPhase(string phase) => Seek(DrillClips.TimeForPhase(phase));

    public void Paint(CharacterLab.Ui.StickFigurePane front, CharacterLab.Ui.StickFigurePane side)
    {
        PaintPane(front, CharacterLab.Ui.StickViewMode.FrontXy, "Front — skeleton + hold-point rifle");
        PaintPane(side, CharacterLab.Ui.StickViewMode.SideZy, "Side — skeleton + hold-point rifle");
    }

    void PaintPane(CharacterLab.Ui.StickFigurePane pane, CharacterLab.Ui.StickViewMode mode, string caption)
    {
        pane.ViewMode = mode;
        pane.Caption = $"{caption}  ·  {Phase}  t={_time:0.0}s";
        pane.ClearExtras();
        pane.SetBoneGuides(HumanoidDebugDraw.BuildSegments(_world));
        pane.SetMannequin(CharacterLab.Ui.MannequinBuilder.FromWorldPose(_world), CharacterLab.Ui.MannequinBuilder.HeadCenter(_world));

        var joints = new Vector3[(int)HumanoidBone.Count];
        for (var i = 0; i < joints.Length; i++)
            joints[i] = _world.Position((HumanoidBone)i);
        pane.SetJointDots(joints);

        // Rifle gizmo (butt→muzzle) + hold markers as short crosses.
        pane.SetOverlaySegments(
            _rifleButt, _rifleTip,
            _holdPrimaryWorld, _holdPrimaryWorld + new Vector3(0.06f, 0f, 0f),
            _holdPrimaryWorld, _holdPrimaryWorld + new Vector3(0f, 0.06f, 0f),
            _holdSecondaryWorld, _holdSecondaryWorld + new Vector3(0.06f, 0f, 0f),
            _holdSecondaryWorld, _holdSecondaryWorld + new Vector3(0f, 0.06f, 0f),
            _world.Position(HumanoidBone.RightHand), _holdPrimaryWorld,
            _world.Position(HumanoidBone.LeftHand),
            Phase.Contains("Present", StringComparison.Ordinal) || Phase.Contains("Salute", StringComparison.Ordinal)
                ? (Phase.Contains("Salute", StringComparison.Ordinal) ? _holdPrimaryWorld : _holdSecondaryWorld)
                : _world.Position(HumanoidBone.LeftHand));
    }

    public SkinStatsReport SkinStats() =>
        new("skeleton-wire", 0, 0, (int)HumanoidBone.Count - 1, 0, _bind.HeightMeters);

    public IReadOnlyList<(HumanoidBone Bone, int PrimaryVerts)> BoneCoverage()
    {
        var list = new List<(HumanoidBone, int)>();
        for (var i = 0; i < (int)HumanoidBone.Count; i++)
        {
            var b = (HumanoidBone)i;
            if (b == HumanoidBone.Count) continue;
            list.Add((b, 1));
        }

        return list;
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
            _rifleButt,
            _rifleTip);

    public HoldLockReport SampleHolds()
    {
        var rHand = _world.Position(HumanoidBone.RightHand);
        var lHand = _world.Position(HumanoidBone.LeftHand);
        var isSalute = Phase.Contains("Salute", StringComparison.Ordinal);
        var isPresent = Phase.Contains("Present", StringComparison.Ordinal);
        var rightTarget = isSalute
            ? _world.Position(HumanoidBone.Head) + new Vector3(0.10f, 0.06f, 0.14f)
            : _holdPrimaryWorld;
        var leftTarget = isSalute ? _holdPrimaryWorld
            : isPresent ? _holdSecondaryWorld
            : lHand;
        return new HoldLockReport(
            Phase, _time,
            _holdPrimaryWorld, _holdSecondaryWorld,
            rHand, lHand,
            Vector3.Distance(rHand, rightTarget),
            Vector3.Distance(lHand, leftTarget));
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
            DrillClips.PhaseName(timeA), DrillClips.PhaseName(timeB), timeA, timeB,
            D(wa.Head, wb.Head), D(wa.RightHand, wb.RightHand), D(wa.LeftHand, wb.LeftHand),
            D(wa.RightFoot, wb.RightFoot), D(wa.LeftFoot, wb.LeftFoot),
            D(wa.Hips, wb.Hips), D(wa.Spine2, wb.Spine2));
    }

    /// <summary>Skeleton “vertex” proxy: joint tip travel max/mean between phases.</summary>
    public VertexDeltaReport MeasureVertexDelta(float timeA, float timeB)
    {
        var travel = MeasureBoneTravel(timeA, timeB);
        var tips = new[]
        {
            travel.Head, travel.RightHand, travel.LeftHand,
            travel.RightFoot, travel.LeftFoot, travel.Hips, travel.Spine2,
        };
        var max = tips.Max();
        var mean = tips.Average();
        return new VertexDeltaReport(
            travel.PhaseA, travel.PhaseB, timeA, timeB,
            max, mean,
            MathF.Max(travel.RightHand, MathF.Max(travel.LeftHand, travel.Head)),
            (travel.RightFoot + travel.LeftFoot) * 0.5f,
            _bind[HumanoidBone.Head].Y);
    }

    void ApplyFrame()
    {
        if (!_bank.TryGet("drill", out var clip))
            return;

        clip.Sample(_time, _pose, _bind);
        _world = HumanoidPoseSolver.SolveWorld(_bind, _pose);
        Phase = DrillClips.PhaseName(_time);

        PlaceWeapon(Phase);
        LockHandsWithFullBodyIk(Phase);
        RefreshHoldsFromWeapon();
    }

    void PlaceWeapon(string phase)
    {
        var spine = _world.Position(HumanoidBone.Spine2);
        var rightHip = _world.Position(HumanoidBone.RightUpLeg);
        var isPresent = phase.Contains("Present", StringComparison.Ordinal);

        Vector3 butt, tip;
        if (isPresent)
        {
            var center = spine + new Vector3(0.02f, 0.05f, 0.28f);
            tip = center + new Vector3(0f, RifleLengthMeters * 0.5f, 0.02f);
            butt = center - new Vector3(0f, RifleLengthMeters * 0.5f, 0.02f);
        }
        else
        {
            var side = new Vector3(rightHip.X + 0.20f, 0.02f, rightHip.Z + 0.06f);
            butt = side;
            tip = side + new Vector3(0.02f, RifleLengthMeters, 0f);
        }

        _weaponWorld = RifleWorldMatrix(butt, tip);
        RefreshHoldsFromWeapon();
    }

    void LockHandsWithFullBodyIk(string phase)
    {
        var isPresent = phase.Contains("Present", StringComparison.Ordinal);
        var isSalute = phase.Contains("Salute", StringComparison.Ordinal);
        var targets = HumanoidFullBodyIkTargets.WithDefaults();

        if (isSalute)
        {
            targets.LeftHand = _holdPrimaryWorld;
            targets.RightHand = _world.Position(HumanoidBone.Head) + new Vector3(0.10f, 0.06f, 0.14f);
        }
        else if (isPresent)
        {
            targets.RightHand = _holdPrimaryWorld;
            targets.LeftHand = _holdSecondaryWorld;
        }
        else
        {
            targets.RightHand = _holdPrimaryWorld;
            // Order: left hangs — leave null so FK clip owns it.
        }

        HumanoidFullBodyIk.Apply(_world, _bind, targets);

        // Snap rifle primary to the gripping hand so the gizmo stays locked.
        if (isSalute)
            SnapWeaponPrimaryTo(_world.Position(HumanoidBone.LeftHand), Vector3.UnitY);
        else
            SnapWeaponPrimaryTo(_world.Position(HumanoidBone.RightHand),
                isPresent ? Vector3.UnitY : Vector3.UnitY);

        if (isPresent)
        {
            // Re-lock both hands after snap.
            targets = HumanoidFullBodyIkTargets.WithDefaults();
            targets.RightHand = _holdPrimaryWorld;
            targets.LeftHand = _holdSecondaryWorld;
            HumanoidFullBodyIk.Apply(_world, _bind, targets);
        }
        else if (isSalute)
        {
            targets = HumanoidFullBodyIkTargets.WithDefaults();
            targets.LeftHand = _holdPrimaryWorld;
            targets.RightHand = _world.Position(HumanoidBone.Head) + new Vector3(0.10f, 0.06f, 0.14f);
            HumanoidFullBodyIk.Apply(_world, _bind, targets);
        }
        else
        {
            targets = HumanoidFullBodyIkTargets.WithDefaults();
            targets.RightHand = _holdPrimaryWorld;
            HumanoidFullBodyIk.Apply(_world, _bind, targets);
        }
    }

    void RefreshHoldsFromWeapon()
    {
        _holdPrimaryWorld = _holds.World(_holds.PrimaryGrip, _weaponWorld);
        _holdSecondaryWorld = _holds.World(_holds.SecondaryGrip, _weaponWorld);
        _rifleButt = _holds.World(_holds.Butt, _weaponWorld);
        _rifleTip = _holds.World(_holds.Muzzle, _weaponWorld);
    }

    void SnapWeaponPrimaryTo(Vector3 primaryWorld, Vector3 barrelHint)
    {
        if (barrelHint.LengthSquared() < 1e-8f)
            barrelHint = Vector3.UnitY;
        barrelHint = Vector3.Normalize(barrelHint);
        var basis = RifleBasis(barrelHint);
        var localPrimary = _holds.PrimaryGrip.LocalPosition;
        _weaponWorld = basis * Matrix4x4.CreateTranslation(primaryWorld - Vector3.Transform(localPrimary, basis));
        RefreshHoldsFromWeapon();
    }

    (Vector3 Hips, Vector3 Head, Vector3 RightHand, Vector3 LeftHand, Vector3 RightFoot, Vector3 LeftFoot, Vector3 Spine2) CloneTips() =>
        (_world.Position(HumanoidBone.Hips),
            _world.Position(HumanoidBone.Head),
            _world.Position(HumanoidBone.RightHand),
            _world.Position(HumanoidBone.LeftHand),
            _world.Position(HumanoidBone.RightFoot),
            _world.Position(HumanoidBone.LeftFoot),
            _world.Position(HumanoidBone.Spine2));

    static Matrix4x4 RifleBasis(Vector3 barrelDir)
    {
        barrelDir = Vector3.Normalize(barrelDir);
        var up = MathF.Abs(Vector3.Dot(barrelDir, Vector3.UnitY)) > 0.98f ? Vector3.UnitX : Vector3.UnitY;
        var x = Vector3.Normalize(Vector3.Cross(up, barrelDir));
        var y = Vector3.Cross(barrelDir, x);
        return new Matrix4x4(
            x.X, x.Y, x.Z, 0,
            y.X, y.Y, y.Z, 0,
            barrelDir.X, barrelDir.Y, barrelDir.Z, 0,
            0, 0, 0, 1);
    }

    static Matrix4x4 RifleWorldMatrix(Vector3 butt, Vector3 tip)
    {
        var mid = (butt + tip) * 0.5f;
        var dir = tip - butt;
        if (dir.LengthSquared() < 1e-8f)
            dir = Vector3.UnitY;
        dir = Vector3.Normalize(dir);
        return RifleBasis(dir) * Matrix4x4.CreateTranslation(mid);
    }
}
