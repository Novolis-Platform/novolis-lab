using System.Numerics;
using Novolis.Game.Humanoid;
using Novolis.Simulation.Humanoid;
using Novolis.Simulation.Humanoid.Import;

namespace CharacterLab.Demo;

internal readonly record struct MocapClipInfo(string Id, string Label, string Source, string? FileName);

/// <summary>
/// CMU BVH mocap player + optional hold-point rifle via <see cref="HumanoidFullBodyIk"/> / <see cref="HumanoidPoseSolver.BakeLocal"/>.
/// </summary>
internal sealed class MocapParadeDriver
{
    const float RifleLengthMeters = 1.05f;
    const string SyntheticDrillId = "synthetic-drill";

    readonly HumanoidBindPose _defaultBind;
    readonly HumanoidClipBank _bank = new();
    readonly Dictionary<string, HumanoidBindPose> _binds = new(StringComparer.OrdinalIgnoreCase);
    readonly HumanoidPose _pose = new();
    readonly WeaponHoldSet _holds;
    readonly List<MocapClipInfo> _clips = [];
    readonly Dictionary<string, LocomotionRemap> _remap = new(StringComparer.OrdinalIgnoreCase);
    readonly string _mocapRoot;
    HumanoidWorldPose _world;
    float _time;
    string _clipId = "";
    bool _holdMode = true;
    Matrix4x4 _weaponWorld = Matrix4x4.Identity;
    Vector3 _rifleButt;
    Vector3 _rifleTip;
    Vector3 _holdPrimaryWorld;
    Vector3 _holdSecondaryWorld;

    /// <summary>
    /// Maps tiny CMU root paths onto Mixamo bind hip height / stride scale.
    /// Lateral root is damped vs forward so gait doesn't read as exaggerated hip sway.
    /// </summary>
    readonly record struct LocomotionRemap(float XzScale, Vector2 OriginXz, bool ForwardAlongZ, bool Enabled);

    /// <summary>Keep this fraction of side-to-side root travel (rest is dropped as feminine sway).</summary>
    const float LateralRootKeep = 0.28f;

    /// <summary>Keep this fraction of pelvis/spine lean away from upright.</summary>
    const float PelvisLeanKeep = 0.32f;

    /// <summary>
    /// Foot lateral offset as a fraction of hip-socket offset (1 = under hip, catwalk ≈ 0).
    /// </summary>
    const float StanceFootUnderHip = 0.85f;

    /// <summary>How hard we pull feet toward under-hip (1 = full, soft avoids knee pops).</summary>
    const float StanceWidenBlend = 0.55f;

    public MocapParadeDriver(string? assetsRoot = null)
    {
        _defaultBind = HumanoidBindPose.CreateDefaultTPose(1.72f);
        _holds = WeaponHoldSet.ForCenteredLongGun(RifleLengthMeters);
        _mocapRoot = ResolveMocapRoot(assetsRoot);
        _world = HumanoidPoseSolver.SolveWorld(_defaultBind, HumanoidPose.FromBind(_defaultBind));

        LoadCatalog();
        if (_clips.Count == 0)
            throw new InvalidOperationException($"No mocap clips found under '{_mocapRoot}' and synthetic drill failed.");

        SelectClip(_clips[0].Id);
    }

    public bool Paused { get; set; }

    public bool HoldMode
    {
        get => _holdMode;
        set
        {
            _holdMode = value;
            ApplyFrame();
        }
    }

    public string Phase { get; private set; } = "";
    public float TimeSeconds => _time;
    public float DurationSeconds { get; private set; }
    public HumanoidBindPose Bind => ActiveBind;

    HumanoidBindPose ActiveBind =>
        _binds.TryGetValue(_clipId, out var b) ? b : _defaultBind;
    public HumanoidWorldPose World => _world;
    public WeaponHoldSet Holds => _holds;
    public string ActiveClipId => _clipId;
    public string SkinSource => ActiveClip?.Source ?? "none";
    public IReadOnlyList<MocapClipInfo> Clips => _clips;
    public MocapClipInfo? ActiveClip
    {
        get
        {
            foreach (var c in _clips)
            {
                if (string.Equals(c.Id, _clipId, StringComparison.OrdinalIgnoreCase))
                    return c;
            }

            return null;
        }
    }

    public Vector3 RifleButt => _rifleButt;
    public Vector3 RifleTip => _rifleTip;
    public Vector3 HoldPrimaryWorld => _holdPrimaryWorld;
    public Vector3 HoldSecondaryWorld => _holdSecondaryWorld;

    public void Tick(float dt)
    {
        if (!Paused)
        {
            _time += dt;
            if (DurationSeconds > 1e-4f)
                _time %= DurationSeconds;
        }

        ApplyFrame();
    }

    public void Seek(float timeSeconds)
    {
        _time = DurationSeconds <= 0f ? 0f : Math.Clamp(timeSeconds, 0f, DurationSeconds);
        ApplyFrame();
    }

    public void SeekPhase(string phase)
    {
        if (string.Equals(_clipId, SyntheticDrillId, StringComparison.OrdinalIgnoreCase))
        {
            Seek(DrillClips.TimeForPhase(phase));
            return;
        }

        // Named phases map to fractions of the mocap clip.
        var p = phase.Trim().ToLowerInvariant();
        var t = p switch
        {
            var s when s.Contains("present") => DurationSeconds * 0.35f,
            var s when s.Contains("salute") => DurationSeconds * 0.65f,
            var s when s.Contains("recover") => DurationSeconds * 0.85f,
            _ => DurationSeconds * 0.1f,
        };
        Seek(t);
    }

    public bool SelectClip(string clipId)
    {
        MocapClipInfo? info = null;
        foreach (var c in _clips)
        {
            if (string.Equals(c.Id, clipId, StringComparison.OrdinalIgnoreCase))
            {
                info = c;
                break;
            }
        }

        if (info is null || !_bank.TryGet(info.Value.Id, out var clip))
            return false;

        _clipId = info.Value.Id;
        DurationSeconds = MathF.Max(clip.DurationSeconds, 1e-3f);
        _time = 0f;
        ApplyFrame();
        return true;
    }

    public void Paint(CharacterLab.Ui.StickFigurePane front, CharacterLab.Ui.StickFigurePane side)
    {
        PaintPane(front, CharacterLab.Ui.StickViewMode.FrontXy, "Front");
        PaintPane(side, CharacterLab.Ui.StickViewMode.SideZy, "Side");
    }

    void PaintPane(CharacterLab.Ui.StickFigurePane pane, CharacterLab.Ui.StickViewMode mode, string title)
    {
        pane.ViewMode = mode;
        pane.Caption = $"{title} · {ActiveClip?.Label ?? _clipId} · t={_time:0.00}s";
        pane.ClearExtras();
        pane.SetBoneGuides(HumanoidDebugDraw.BuildSegments(_world));
        pane.SetMannequin(CharacterLab.Ui.MannequinBuilder.FromWorldPose(_world), CharacterLab.Ui.MannequinBuilder.HeadCenter(_world));

        var joints = new Vector3[(int)HumanoidBone.Count];
        for (var i = 0; i < joints.Length; i++)
        {
            if ((HumanoidBone)i == HumanoidBone.Count) continue;
            joints[i] = _world.Position((HumanoidBone)i);
        }

        pane.SetJointDots(joints);

        if (_holdMode)
        {
            pane.SetOverlaySegments(
                _rifleButt, _rifleTip,
                _holdPrimaryWorld, _holdPrimaryWorld + new Vector3(0.06f, 0f, 0f),
                _holdPrimaryWorld, _holdPrimaryWorld + new Vector3(0f, 0.06f, 0f),
                _holdSecondaryWorld, _holdSecondaryWorld + new Vector3(0.06f, 0f, 0f),
                _holdSecondaryWorld, _holdSecondaryWorld + new Vector3(0f, 0.06f, 0f),
                _world.Position(HumanoidBone.RightHand), _holdPrimaryWorld,
                _world.Position(HumanoidBone.LeftHand), _holdSecondaryWorld);
        }
    }

    public SkinStatsReport SkinStats() =>
        new(SkinSource, 0, 0, (int)HumanoidBone.Count - 1, 0, ActiveBind.HeightMeters);

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
        if (!_holdMode)
        {
            return new HoldLockReport(Phase, _time, _holdPrimaryWorld, _holdSecondaryWorld, rHand, lHand, 0f, 0f);
        }

        return new HoldLockReport(
            Phase, _time,
            _holdPrimaryWorld, _holdSecondaryWorld,
            rHand, lHand,
            Vector3.Distance(rHand, _holdPrimaryWorld),
            Vector3.Distance(lHand, _holdSecondaryWorld));
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
            PhaseName(timeA), PhaseName(timeB), timeA, timeB,
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
        var max = tips.Max();
        var mean = tips.Average();
        return new VertexDeltaReport(
            travel.PhaseA, travel.PhaseB, timeA, timeB,
            max, mean,
            MathF.Max(travel.RightHand, MathF.Max(travel.LeftHand, travel.Head)),
            (travel.RightFoot + travel.LeftFoot) * 0.5f,
            ActiveBind[HumanoidBone.Head].Y);
    }

    void LoadCatalog()
    {
        _clips.Clear();
        if (Directory.Exists(_mocapRoot))
        {
            foreach (var path in Directory.EnumerateFiles(_mocapRoot, "*.bvh").OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    var (clip, bind) = BvhHumanoidImporter.ImportFileWithBind(
                        path, metersPerUnit: 0.01f, BvhHumanoidImporter.RenameCmuJoint, targetHeightMeters: 1.72f);
                    clip.Loop = true;
                    var id = Path.GetFileNameWithoutExtension(path);
                    clip = RenameClip(clip, id);
                    _bank.Set(id, clip);
                    _binds[id] = bind;
                    _remap[id] = BuildRemap(clip, bind);
                    _clips.Add(new MocapClipInfo(id, id.Replace('_', ' '), "cmu-bvh", Path.GetFileName(path)));
                    var r = _remap[id];
                    var hipW = Vector3.Distance(bind[HumanoidBone.LeftUpLeg], bind[HumanoidBone.RightUpLeg]);
                    Console.WriteLine(
                        $"Loaded mocap {id}: keys={clip.Keys.Count} dur={clip.DurationSeconds:0.##}s xzScale={r.XzScale:0.##} hipWidth={hipW:0.###}m");
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Skip BVH '{path}': {ex.Message}");
                }
            }
        }

        var drill = DrillClips.CreateDrill(_defaultBind);
        _bank.Set(SyntheticDrillId, drill);
        _binds[SyntheticDrillId] = _defaultBind;
        _clips.Add(new MocapClipInfo(SyntheticDrillId, "Synthetic drill", "synthetic", null));
    }

    static HumanoidAnimationClip RenameClip(HumanoidAnimationClip clip, string name)
    {
        // HumanoidAnimationClip.Name is get-only via ctor — rebuild keys into named clip.
        var copy = new HumanoidAnimationClip(name) { Loop = clip.Loop };
        foreach (var key in clip.Keys)
            copy.AddKey(key);
        return copy;
    }

    void ApplyFrame()
    {
        if (!_bank.TryGet(_clipId, out var clip) || clip is null)
            return;

        var bind = ActiveBind;
        clip.Sample(_time, _pose, bind);
        RemapRootToBindProportions(bind);
        DampenUnisexPelvisSway();
        _world = HumanoidPoseSolver.SolveWorld(bind, _pose);
        PlantSupportFoot(bind);
        WidenStanceFeet(bind);
        Phase = PhaseName(_time);

        if (_holdMode)
        {
            PlaceWeapon();
            var targets = HumanoidFullBodyIkTargets.WithDefaults();
            targets.RightHand = _holdPrimaryWorld;
            targets.LeftHand = _holdSecondaryWorld;
            HumanoidFullBodyIk.Apply(_world, bind, targets);
            SnapWeaponPrimaryTo(_world.Position(HumanoidBone.RightHand), Vector3.UnitY);
            targets = HumanoidFullBodyIkTargets.WithDefaults();
            targets.RightHand = _holdPrimaryWorld;
            targets.LeftHand = _holdSecondaryWorld;
            HumanoidFullBodyIk.Apply(_world, bind, targets);
        }
        else
        {
            ClearWeapon();
        }

        HumanoidPoseSolver.BakeLocal(bind, _world, _pose);
    }

    /// <summary>
    /// CMU BVH roots are ~0.16 m hip height at metersPerUnit=0.01 while rest bind hips sit ~0.9 m.
    /// Keep bind hip Y; full scale on the walk axis, damped scale on the lateral axis (unisex gait).
    /// </summary>
    void RemapRootToBindProportions(HumanoidBindPose bind)
    {
        if (!_remap.TryGetValue(_clipId, out var remap) || !remap.Enabled)
            return;

        var raw = _pose.RootTranslation;
        var dx = (raw.X - remap.OriginXz.X) * remap.XzScale;
        var dz = (raw.Z - remap.OriginXz.Y) * remap.XzScale;
        if (remap.ForwardAlongZ)
            dx *= LateralRootKeep;
        else
            dz *= LateralRootKeep;

        _pose.RootTranslation = new Vector3(dx, bind[HumanoidBone.Hips].Y, dz);
    }

    /// <summary>
    /// Pulls hips/lower spine toward upright so captured hip-roll sway doesn't read as a runway walk.
    /// </summary>
    void DampenUnisexPelvisSway()
    {
        if (string.Equals(_clipId, SyntheticDrillId, StringComparison.OrdinalIgnoreCase))
            return;

        _pose[HumanoidBone.Hips] = DampenLateralLean(_pose[HumanoidBone.Hips], PelvisLeanKeep);
        _pose[HumanoidBone.Spine] = DampenLateralLean(_pose[HumanoidBone.Spine], MathF.Min(1f, PelvisLeanKeep + 0.15f));
        _pose[HumanoidBone.Spine1] = DampenLateralLean(_pose[HumanoidBone.Spine1], MathF.Min(1f, PelvisLeanKeep + 0.25f));
    }

    /// <summary>Reduces lean of <paramref name="local"/>'s up-axis away from world +Y, keeping heading.</summary>
    static Quaternion DampenLateralLean(Quaternion local, float keepLean)
    {
        keepLean = Math.Clamp(keepLean, 0f, 1f);
        local = Quaternion.Normalize(local);
        var up = Vector3.Transform(Vector3.UnitY, local);
        if (up.LengthSquared() < 1e-8f)
            return local;

        up = Vector3.Normalize(up);
        var targetUp = Vector3.Normalize(Vector3.Lerp(Vector3.UnitY, up, keepLean));
        if (Vector3.Dot(up, targetUp) > 0.9999f)
            return local;

        var correct = QuatFromTo(up, targetUp);
        return Quaternion.Normalize(correct * local);
    }

    static Quaternion QuatFromTo(Vector3 from, Vector3 to)
    {
        from = Vector3.Normalize(from);
        to = Vector3.Normalize(to);
        var dot = Vector3.Dot(from, to);
        if (dot > 0.999999f)
            return Quaternion.Identity;
        if (dot < -0.999999f)
        {
            var axis = Vector3.Cross(Vector3.UnitX, from);
            if (axis.LengthSquared() < 1e-8f)
                axis = Vector3.Cross(Vector3.UnitY, from);
            return Quaternion.CreateFromAxisAngle(Vector3.Normalize(axis), MathF.PI);
        }

        var c = Vector3.Cross(from, to);
        return Quaternion.Normalize(new Quaternion(c.X, c.Y, c.Z, 1f + dot));
    }

    /// <summary>Shift the whole figure so the lower foot rests on y=0 (support foot); other foot may lift.</summary>
    void PlantSupportFoot(HumanoidBindPose bind)
    {
        if (string.Equals(_clipId, SyntheticDrillId, StringComparison.OrdinalIgnoreCase))
            return;

        var leftY = _world.Position(HumanoidBone.LeftFoot).Y;
        var rightY = _world.Position(HumanoidBone.RightFoot).Y;
        var minY = MathF.Min(leftY, rightY);
        if (MathF.Abs(minY) < 1e-4f)
            return;

        _pose.RootTranslation -= new Vector3(0f, minY, 0f);
        _world = HumanoidPoseSolver.SolveWorld(bind, _pose);
    }

    /// <summary>
    /// Catwalk mocap parks both feet on the centerline; softly IK them toward the hip
    /// sockets (clamped to reach) so knees keep bind lengths.
    /// </summary>
    void WidenStanceFeet(HumanoidBindPose bind)
    {
        if (string.Equals(_clipId, SyntheticDrillId, StringComparison.OrdinalIgnoreCase))
            return;

        var hips = _world.Position(HumanoidBone.Hips);
        var lHip = _world.Position(HumanoidBone.LeftUpLeg);
        var rHip = _world.Position(HumanoidBone.RightUpLeg);
        var lateral = new Vector3(rHip.X - lHip.X, 0f, rHip.Z - lHip.Z);
        if (lateral.LengthSquared() < 1e-6f)
            lateral = Vector3.UnitX;
        lateral = Vector3.Normalize(lateral);
        var forward = new Vector3(-lateral.Z, 0f, lateral.X);

        var lFoot = _world.Position(HumanoidBone.LeftFoot);
        var rFoot = _world.Position(HumanoidBone.RightFoot);
        var lKnee = _world.Position(HumanoidBone.LeftLeg);
        var rKnee = _world.Position(HumanoidBone.RightLeg);

        Vector3 SoftUnderHip(Vector3 foot, Vector3 hipSocket)
        {
            var hipLat = Vector3.Dot(hipSocket - hips, lateral);
            var footFwd = Vector3.Dot(foot - hips, forward);
            var ideal = hips + lateral * (hipLat * StanceFootUnderHip) + forward * footFwd;
            ideal = new Vector3(ideal.X, foot.Y, ideal.Z);
            return Vector3.Lerp(foot, ideal, StanceWidenBlend);
        }

        // Preserve mocap knee bend side (pole = current mid) to avoid flip/pop.
        var targets = HumanoidFullBodyIkTargets.WithDefaults();
        targets.LeftFoot = SoftUnderHip(lFoot, lHip);
        targets.RightFoot = SoftUnderHip(rFoot, rHip);
        targets.LeftFootPole = lKnee;
        targets.RightFootPole = rKnee;
        HumanoidFullBodyIk.Apply(_world, bind, targets);

        var dL = _world.Position(HumanoidBone.LeftFoot) - lFoot;
        var dR = _world.Position(HumanoidBone.RightFoot) - rFoot;
        _world.Set(
            HumanoidBone.LeftToeBase,
            _world.Position(HumanoidBone.LeftToeBase) + dL,
            _world.Rotation(HumanoidBone.LeftToeBase));
        _world.Set(
            HumanoidBone.RightToeBase,
            _world.Position(HumanoidBone.RightToeBase) + dR,
            _world.Rotation(HumanoidBone.RightToeBase));
    }

    static LocomotionRemap BuildRemap(HumanoidAnimationClip clip, HumanoidBindPose bind)
    {
        if (clip.Keys.Count == 0 || clip.Keys[0].RootTranslation is not { } first)
            return new LocomotionRemap(1f, Vector2.Zero, ForwardAlongZ: true, Enabled: false);

        var rawHipY = MathF.Abs(first.Y);
        if (rawHipY < 0.05f)
            return new LocomotionRemap(1f, Vector2.Zero, ForwardAlongZ: true, Enabled: false);

        var last = clip.Keys[^1].RootTranslation ?? first;
        var forwardAlongZ = MathF.Abs(last.Z - first.Z) >= MathF.Abs(last.X - first.X);
        var bindHipY = bind[HumanoidBone.Hips].Y;
        var xzScale = bindHipY / rawHipY;
        return new LocomotionRemap(xzScale, new Vector2(first.X, first.Z), forwardAlongZ, Enabled: true);
    }

    string PhaseName(float t)
    {
        if (string.Equals(_clipId, SyntheticDrillId, StringComparison.OrdinalIgnoreCase))
            return DrillClips.PhaseName(t);
        var frac = DurationSeconds <= 0f ? 0f : t / DurationSeconds;
        return frac switch
        {
            < 0.25f => "Mocap A",
            < 0.5f => "Mocap B",
            < 0.75f => "Mocap C",
            _ => "Mocap D",
        };
    }

    void PlaceWeapon()
    {
        var spine = _world.Position(HumanoidBone.Spine2);
        var center = spine + new Vector3(0.02f, 0.02f, 0.22f);
        var tip = center + new Vector3(0f, RifleLengthMeters * 0.45f, 0.05f);
        var butt = center - new Vector3(0f, RifleLengthMeters * 0.45f, 0.02f);
        _weaponWorld = RifleWorldMatrix(butt, tip);
        RefreshHolds();
    }

    void ClearWeapon()
    {
        _rifleButt = default;
        _rifleTip = default;
        _holdPrimaryWorld = default;
        _holdSecondaryWorld = default;
    }

    void RefreshHolds()
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
        RefreshHolds();
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
        return RifleBasis(Vector3.Normalize(dir)) * Matrix4x4.CreateTranslation(mid);
    }

    static string ResolveMocapRoot(string? assetsRoot)
    {
        var candidates = new List<string>();
        if (!string.IsNullOrWhiteSpace(assetsRoot))
            candidates.Add(Path.Combine(assetsRoot, "mocap"));
        candidates.Add(Path.Combine(AppContext.BaseDirectory, "assets", "mocap"));
        candidates.Add(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "assets", "mocap")));
        foreach (var c in candidates)
        {
            if (Directory.Exists(c))
                return Path.GetFullPath(c);
        }

        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "assets", "mocap"));
    }
}
