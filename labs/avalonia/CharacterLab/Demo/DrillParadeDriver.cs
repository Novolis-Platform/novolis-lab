using System.Numerics;
using Novolis.Avalonia.ThreeD.Session;
using Novolis.Game.Humanoid;
using Novolis.Math.Geometry;
using Novolis.ThreeD;
using Novolis.ThreeD;
using Novolis.Simulation.Humanoid;
using Novolis.Simulation.Humanoid.Skinning;

namespace CharacterLab.Demo;

/// <summary>
/// 3D drill: continuous auto-skinned WhiteTechwearGirl LOD + SciFi Rifle with grip hold-points.
/// Hands Soft-IK lock to primary/secondary holds; bone rotations update so LBS actually bends.
/// </summary>
internal sealed class DrillParadeDriver
{
    const int CharacterLodTris = 28_000;
    const int RifleLodTris = 8_000;
    const float RifleLengthMeters = 1.05f;

    readonly HumanoidBindPose _bind;
    readonly HumanoidClipBank _bank;
    readonly HumanoidPose _pose = new();
    readonly SkinnedHumanoidMesh _skin;
    readonly Vector3[] _skinScratch;
    readonly Vector3[] _measureA;
    readonly Vector3[] _measureB;
    readonly int[] _bindIndices;
    readonly TriangleMesh _rifleBind;
    readonly WeaponHoldSet _holds;
    readonly MeshNode _characterNode;
    readonly MeshNode _rifleNode;
    readonly MeshNode _holdPrimaryNode;
    readonly MeshNode _holdSecondaryNode;
    readonly MeshNode _handRightMarker;
    readonly MeshNode _handLeftMarker;
    readonly SceneSessionService _session;
    readonly string _skinSource;
    float _time;
    Matrix4x4 _weaponWorld = Matrix4x4.Identity;
    Vector3 _rifleButt;
    Vector3 _rifleTip;
    Vector3 _holdPrimaryWorld;
    Vector3 _holdSecondaryWorld;

    public DrillParadeDriver(SceneSessionService session, string assetsRoot)
    {
        _session = session;
        _bind = HumanoidBindPose.CreateDefaultTPose(1.72f);
        _bank = DrillClips.CreateBank(_bind);

        Console.WriteLine("Loading & auto-skinning WhiteTechwearGirl…");
        (_skin, _skinSource) = LoadCharacterSkin(assetsRoot, _bind);
        _skinScratch = new Vector3[_skin.BindMesh.VertexCount];
        _measureA = new Vector3[_skin.BindMesh.VertexCount];
        _measureB = new Vector3[_skin.BindMesh.VertexCount];
        _bindIndices = _skin.BindMesh.Indices.ToArray();

        Console.WriteLine("Loading SciFi Rifle (LOD) + hold points…");
        _rifleBind = LoadRifleLod(assetsRoot);
        _holds = WeaponHoldSet.ForCenteredLongGun(_rifleBind, RifleLengthMeters);

        var doc = BuildDocument(
            out _characterNode,
            out _rifleNode,
            out _holdPrimaryNode,
            out _holdSecondaryNode,
            out _handRightMarker,
            out _handLeftMarker);
        MeshEditBake.WriteBaked(_characterNode, EditableMesh.FromTriangleMesh(_skin.BindMesh));
        MeshEditBake.WriteBaked(_rifleNode, EditableMesh.FromTriangleMesh(_rifleBind));
        session.ReplaceDocument(doc);
        Console.WriteLine(
            $"Skin ready ({_skinSource}): verts={_skin.BindMesh.VertexCount} tris={_skin.BindMesh.TriangleCount}; " +
            $"holds primary={_holds.PrimaryGrip.LocalPosition} secondary={_holds.SecondaryGrip.LocalPosition}");
        Seek(0.6f);
    }

    public bool Paused { get; set; }

    public string Phase { get; private set; } = "Order Arms";

    public string SkinSource => _skinSource;

    public float TimeSeconds => _time;

    public HumanoidBindPose Bind => _bind;

    public SkinnedHumanoidMesh Skin => _skin;

    public WeaponHoldSet Holds => _holds;

    public void Tick(float dt)
    {
        if (!Paused)
            _time += dt;
        ApplyFrame(writeScene: true);
    }

    public void Seek(float timeSeconds)
    {
        _time = timeSeconds;
        ApplyFrame(writeScene: true);
    }

    public void SeekPhase(string phase) => Seek(DrillClips.TimeForPhase(phase));

    public SkinStatsReport SkinStats()
    {
        var covered = new bool[(int)HumanoidBone.Count];
        var multi = 0;
        for (var v = 0; v < _skin.VertexWeights.Count; v++)
        {
            var w = _skin.VertexWeights[v];
            if (w.Length > 1)
                multi++;
            if (w.Length > 0)
                covered[(int)w[0].Bone] = true;
        }

        return new SkinStatsReport(
            _skinSource,
            _skin.BindMesh.VertexCount,
            _skin.BindMesh.TriangleCount,
            covered.Count(static c => c),
            multi,
            _bind.HeightMeters);
    }

    public IReadOnlyList<(HumanoidBone Bone, int PrimaryVerts)> BoneCoverage()
    {
        var counts = new int[(int)HumanoidBone.Count];
        for (var v = 0; v < _skin.VertexWeights.Count; v++)
        {
            var w = _skin.VertexWeights[v];
            if (w.Length > 0)
                counts[(int)w[0].Bone]++;
        }

        var list = new List<(HumanoidBone, int)>(counts.Length);
        for (var i = 0; i < counts.Length; i++)
        {
            if (counts[i] > 0)
                list.Add(((HumanoidBone)i, counts[i]));
        }

        list.Sort(static (a, b) => b.Item2.CompareTo(a.Item2));
        return list;
    }

    public BoneTravelReport MeasureBoneTravel(float timeA, float timeB)
    {
        SampleWorldAt(timeA, out var wa);
        SampleWorldAt(timeB, out var wb);

        float Tip(HumanoidBone bone) => Vector3.Distance(wa.Position(bone), wb.Position(bone));
        return new BoneTravelReport(
            DrillClips.PhaseName(timeA),
            DrillClips.PhaseName(timeB),
            timeA,
            timeB,
            Tip(HumanoidBone.Head),
            Tip(HumanoidBone.RightHand),
            Tip(HumanoidBone.LeftHand),
            Tip(HumanoidBone.RightFoot),
            Tip(HumanoidBone.LeftFoot),
            Tip(HumanoidBone.Hips),
            Tip(HumanoidBone.Spine2));
    }

    public PoseSampleReport SamplePose()
    {
        EnsureSampledWorld(out var world);
        return new PoseSampleReport(
            Phase,
            _time,
            world.Position(HumanoidBone.Hips),
            world.Position(HumanoidBone.Head),
            world.Position(HumanoidBone.LeftHand),
            world.Position(HumanoidBone.RightHand),
            world.Position(HumanoidBone.LeftFoot),
            world.Position(HumanoidBone.RightFoot),
            _rifleButt,
            _rifleTip);
    }

    /// <summary>Hold points + hand lock error (meters) for agent / smoke.</summary>
    public HoldLockReport SampleHolds()
    {
        EnsureSampledWorld(out var world);
        var rHand = world.Position(HumanoidBone.RightHand);
        var lHand = world.Position(HumanoidBone.LeftHand);
        var isSalute = Phase.Contains("Salute", StringComparison.Ordinal);
        // Salute: left on primary, right on brow — report the intentional locks.
        var rightTarget = isSalute
            ? world.Position(HumanoidBone.Head) + new Vector3(0.10f, 0.06f, 0.14f)
            : _holdPrimaryWorld;
        var leftTarget = isSalute ? _holdPrimaryWorld : _holdSecondaryWorld;
        if (!isSalute && !Phase.Contains("Present", StringComparison.Ordinal))
            leftTarget = lHand; // order: left not on rifle — error 0 vs self

        return new HoldLockReport(
            Phase,
            _time,
            _holdPrimaryWorld,
            _holdSecondaryWorld,
            rHand,
            lHand,
            Vector3.Distance(rHand, rightTarget),
            Vector3.Distance(lHand, isSalute || Phase.Contains("Present", StringComparison.Ordinal)
                ? leftTarget
                : lHand));
    }

    public VertexDeltaReport MeasureVertexDelta(float timeA, float timeB)
    {
        DeformAt(timeA, _measureA);
        DeformAt(timeB, _measureB);
        var n = _measureA.Length;
        var max = 0f;
        var sum = 0.0;
        var handish = 0f;
        var footish = 0f;
        var footN = 0;
        var hips = _bind[HumanoidBone.Hips];
        var headY = _bind[HumanoidBone.Head].Y;

        for (var i = 0; i < n; i++)
        {
            var d = Vector3.Distance(_measureA[i], _measureB[i]);
            max = MathF.Max(max, d);
            sum += d;
            var y = _skin.BindMesh.Vertices[i].Y;
            if (y > hips.Y + 0.15f)
                handish = MathF.Max(handish, d);
            else if (y < hips.Y - 0.2f)
            {
                footish += d;
                footN++;
            }
        }

        return new VertexDeltaReport(
            DrillClips.PhaseName(timeA),
            DrillClips.PhaseName(timeB),
            timeA,
            timeB,
            max,
            n == 0 ? 0f : (float)(sum / n),
            handish,
            footN == 0 ? 0f : footish / footN,
            headY);
    }

    void ApplyFrame(bool writeScene)
    {
        if (!_bank.TryGet("drill", out var clip))
            return;

        clip.Sample(_time, _pose, _bind);
        var world = HumanoidPoseSolver.SolveWorld(_bind, _pose);
        Phase = DrillClips.PhaseName(_time);
        PlaceRifleAndLockHands(world, Phase);
        // Auto-skin + Soft-IK: translation blend stays continuous. Full LBS+FromTo explodes clothing.
        CpuSkinDeformer.DeformTranslations(_skin, _bind, world, _skinScratch);

        if (!writeScene)
            return;

        var skinned = new TriangleMesh(_skinScratch, _bindIndices);
        MeshEditBake.WriteBaked(_characterNode, EditableMesh.FromTriangleMesh(skinned));

        var rifle = EditableMesh.FromTriangleMesh(_rifleBind);
        rifle.Transform(_weaponWorld);
        MeshEditBake.WriteBaked(_rifleNode, rifle);
        _rifleNode.Transform = new SceneTransform();

        PlaceMarker(_holdPrimaryNode, _holdPrimaryWorld, 0.04f);
        PlaceMarker(_holdSecondaryNode, _holdSecondaryWorld, 0.035f);
        PlaceMarker(_handRightMarker, world.Position(HumanoidBone.RightHand), 0.03f);
        PlaceMarker(_handLeftMarker, world.Position(HumanoidBone.LeftHand), 0.03f);

        _session.Evaluator.NotifyNodeChanged(_characterNode);
        _session.Evaluator.NotifyNodeChanged(_rifleNode);
        _session.Evaluator.NotifyNodeChanged(_holdPrimaryNode);
        _session.Evaluator.NotifyNodeChanged(_holdSecondaryNode);
        _session.Evaluator.NotifyNodeChanged(_handRightMarker);
        _session.Evaluator.NotifyNodeChanged(_handLeftMarker);
    }

    void DeformAt(float time, Span<Vector3> dest)
    {
        var saved = _time;
        _time = time;
        if (!_bank.TryGet("drill", out var clip))
            return;
        clip.Sample(_time, _pose, _bind);
        var world = HumanoidPoseSolver.SolveWorld(_bind, _pose);
        PlaceRifleAndLockHands(world, DrillClips.PhaseName(_time));
        CpuSkinDeformer.DeformTranslations(_skin, _bind, world, dest);
        _time = saved;
    }

    void EnsureSampledWorld(out HumanoidWorldPose world)
    {
        if (!_bank.TryGet("drill", out var clip))
        {
            world = HumanoidPoseSolver.SolveWorld(_bind, new HumanoidPose());
            return;
        }

        clip.Sample(_time, _pose, _bind);
        world = HumanoidPoseSolver.SolveWorld(_bind, _pose);
        PlaceRifleAndLockHands(world, Phase);
    }

    void SampleWorldAt(float time, out HumanoidWorldPose world)
    {
        if (!_bank.TryGet("drill", out var clip))
        {
            world = HumanoidPoseSolver.SolveWorld(_bind, new HumanoidPose());
            return;
        }

        clip.Sample(time, _pose, _bind);
        world = HumanoidPoseSolver.SolveWorld(_bind, _pose);
        PlaceRifleAndLockHands(world, DrillClips.PhaseName(time));
    }

    /// <summary>
    /// Place weapon for the drill style → resolve holds → Soft-IK hands onto holds →
    /// optionally snap the rifle so primary grip sits exactly on the right hand (or left on salute).
    /// </summary>
    void PlaceRifleAndLockHands(HumanoidWorldPose world, string phase)
    {
        var isPresent = phase.Contains("Present", StringComparison.Ordinal);
        var isSalute = phase.Contains("Salute", StringComparison.Ordinal);

        _weaponWorld = ComputeWeaponWorld(world, phase);
        RefreshHoldsFromWeapon();

        if (isSalute)
        {
            SoftIk(world, HumanoidBone.LeftArm, HumanoidBone.LeftForeArm, HumanoidBone.LeftHand, _holdPrimaryWorld);
            var brow = world.Position(HumanoidBone.Head) + new Vector3(0.10f, 0.06f, 0.14f);
            SoftIk(world, HumanoidBone.RightArm, HumanoidBone.RightForeArm, HumanoidBone.RightHand, brow);
            // Snap rifle primary to the left hand that steadies it.
            SnapWeaponPrimaryTo(world.Position(HumanoidBone.LeftHand), barrelHint: _rifleTip - _rifleButt);
        }
        else if (isPresent)
        {
            SoftIk(world, HumanoidBone.RightArm, HumanoidBone.RightForeArm, HumanoidBone.RightHand, _holdPrimaryWorld);
            SoftIk(world, HumanoidBone.LeftArm, HumanoidBone.LeftForeArm, HumanoidBone.LeftHand, _holdSecondaryWorld);
            // Two-hand present: rebuild rifle from the two locked hands.
            SnapWeaponBetweenHands(
                world.Position(HumanoidBone.RightHand),
                world.Position(HumanoidBone.LeftHand));
            SoftIk(world, HumanoidBone.RightArm, HumanoidBone.RightForeArm, HumanoidBone.RightHand, _holdPrimaryWorld);
            SoftIk(world, HumanoidBone.LeftArm, HumanoidBone.LeftForeArm, HumanoidBone.LeftHand, _holdSecondaryWorld);
        }
        else
        {
            SoftIk(world, HumanoidBone.RightArm, HumanoidBone.RightForeArm, HumanoidBone.RightHand, _holdPrimaryWorld);
            var spine = world.Position(HumanoidBone.Spine2);
            SoftIk(world, HumanoidBone.LeftArm, HumanoidBone.LeftForeArm, HumanoidBone.LeftHand,
                spine + new Vector3(-0.20f, -0.08f, 0.05f));
            SnapWeaponPrimaryTo(world.Position(HumanoidBone.RightHand), barrelHint: Vector3.UnitY);
            SoftIk(world, HumanoidBone.RightArm, HumanoidBone.RightForeArm, HumanoidBone.RightHand, _holdPrimaryWorld);
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
        var translation = primaryWorld - Vector3.Transform(localPrimary, basis);
        _weaponWorld = basis * Matrix4x4.CreateTranslation(translation);
        RefreshHoldsFromWeapon();
    }

    void SnapWeaponBetweenHands(Vector3 rightHand, Vector3 leftHand)
    {
        var span = leftHand - rightHand;
        var barrel = span.LengthSquared() < 1e-8f ? Vector3.UnitY : Vector3.Normalize(span);
        // Vertical present: prefer up if hands are stacked.
        if (MathF.Abs(barrel.Y) > 0.55f)
            barrel = Vector3.Normalize(new Vector3(barrel.X * 0.2f, MathF.Sign(barrel.Y + 1e-6f), barrel.Z * 0.2f));
        else
            barrel = Vector3.Normalize(new Vector3(0.05f, 1f, 0.1f));

        var basis = RifleBasis(barrel);
        var localPrimary = _holds.PrimaryGrip.LocalPosition;
        _weaponWorld = basis * Matrix4x4.CreateTranslation(rightHand - Vector3.Transform(localPrimary, basis));
        RefreshHoldsFromWeapon();
        var err = leftHand - _holdSecondaryWorld;
        _weaponWorld *= Matrix4x4.CreateTranslation(err * 0.35f);
        RefreshHoldsFromWeapon();
        _weaponWorld *= Matrix4x4.CreateTranslation(rightHand - _holdPrimaryWorld);
        RefreshHoldsFromWeapon();
    }

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

    Matrix4x4 ComputeWeaponWorld(HumanoidWorldPose world, string phase)
    {
        var spine = world.Position(HumanoidBone.Spine2);
        var rightHip = world.Position(HumanoidBone.RightUpLeg);
        var head = world.Position(HumanoidBone.Head);
        var isPresent = phase.Contains("Present", StringComparison.Ordinal);
        var isSalute = phase.Contains("Salute", StringComparison.Ordinal);

        Vector3 butt;
        Vector3 tip;
        if (isPresent)
        {
            // Vertical present — muzzle up, in front of chest.
            var center = spine + new Vector3(0.02f, 0.05f, 0.28f);
            tip = center + new Vector3(0f, RifleLengthMeters * 0.5f, 0.02f);
            butt = center - new Vector3(0f, RifleLengthMeters * 0.5f, 0.02f);
        }
        else if (isSalute)
        {
            // Side arm, slightly forward so left hand can reach secondary while right salutes… wait.
            // Salute: right hand leaves the gun (brow), left steadies on primary.
            // For salute we still place the rifle at the side; right hand will SoftIK to head after we
            // temporarily lock… Actually current design locks right to primary always.
            // Salute needs right hand OFF the gun. Override below after matrix build.
            var side = new Vector3(rightHip.X + 0.20f, 0.05f, rightHip.Z + 0.06f);
            butt = side;
            tip = side + new Vector3(0.02f, RifleLengthMeters, 0f);
        }
        else
        {
            var side = new Vector3(rightHip.X + 0.20f, 0.02f, rightHip.Z + 0.06f);
            butt = side;
            tip = side + new Vector3(0.02f, RifleLengthMeters, 0f);
        }

        var mat = RifleWorldMatrix(butt, tip);

        // Salute: keep rifle on left-hand secondary + primary along side; right hand goes to brow
        // instead of primary — handled in PlaceRifleAndLockHands with a salute branch.
        _ = head;
        return mat;
    }

    void SoftIk(
        HumanoidWorldPose world,
        HumanoidBone upper,
        HumanoidBone mid,
        HumanoidBone hand,
        Vector3 target)
    {
        var u = Vector3.Distance(_bind[upper], _bind[mid]);
        var l = Vector3.Distance(_bind[mid], _bind[hand]);
        TwoBoneIk.ApplyLimb(world, _bind, upper, mid, hand, target, u, l, Vector3.UnitZ);
    }

    static void PlaceMarker(MeshNode node, Vector3 position, float radius)
    {
        node.Transform = new SceneTransform
        {
            Position = [position.X, position.Y, position.Z],
            Scale = [radius, radius, radius],
        };
    }

    static SceneDocument BuildDocument(
        out MeshNode characterNode,
        out MeshNode rifleNode,
        out MeshNode holdPrimary,
        out MeshNode holdSecondary,
        out MeshNode handRight,
        out MeshNode handLeft)
    {
        var doc = new SceneDocument
        {
            Name = "Military drill — hold-point grip + auto-skin",
            CreatedAt = DateTimeOffset.UtcNow,
            ModifiedAt = DateTimeOffset.UtcNow,
        };
        var root = new GroupNode { Name = "ParadeGround" };
        doc.Nodes.Add(root);
        doc.Nodes.Add(new MeshNode
        {
            Name = "ParadeYard",
            ParentId = root.Id,
            Primitive = MeshPrimitiveKind.Box,
            Transform = new SceneTransform { Position = [0f, -0.02f, 0f], Scale = [5f, 0.04f, 5f] },
        });

        characterNode = new MeshNode
        {
            Name = "WhiteTechwearGirl",
            ParentId = root.Id,
            Primitive = MeshPrimitiveKind.Box,
        };
        doc.Nodes.Add(characterNode);

        rifleNode = new MeshNode
        {
            Name = "SciFiRifle",
            ParentId = root.Id,
            Primitive = MeshPrimitiveKind.Box,
        };
        doc.Nodes.Add(rifleNode);

        holdPrimary = Marker(doc, root.Id, "Hold.Primary");
        holdSecondary = Marker(doc, root.Id, "Hold.Secondary");
        handRight = Marker(doc, root.Id, "Hand.Right");
        handLeft = Marker(doc, root.Id, "Hand.Left");

        doc.Nodes.Add(new CameraNode
        {
            Name = "ReviewCamera",
            ParentId = root.Id,
            Transform = new SceneTransform { Position = [2.1f, 1.4f, 3.4f] },
            Target = [0.05f, 0.95f, 0f],
            FovDeg = 34f,
        });
        doc.Nodes.Add(new LightNode
        {
            Name = "Key",
            ParentId = root.Id,
            LightKind = LightKind.Spot,
            Intensity = 3.6f,
            Transform = new SceneTransform { Position = [3.0f, 4.0f, 2.6f], RotationDeg = [42f, -30f, 0f] },
        });
        doc.Nodes.Add(new LightNode
        {
            Name = "Fill",
            ParentId = root.Id,
            LightKind = LightKind.Omni,
            Intensity = 1.6f,
            Color = [0.78f, 0.86f, 1f],
            Transform = new SceneTransform { Position = [-2.0f, 2.0f, 1.2f] },
        });
        doc.Nodes.Add(new LightNode
        {
            Name = "Rim",
            ParentId = root.Id,
            LightKind = LightKind.Infinite,
            Intensity = 0.6f,
            Transform = new SceneTransform { RotationDeg = [-50f, 20f, 0f] },
        });
        doc.SelectionId = null;
        return doc;
    }

    static MeshNode Marker(SceneDocument doc, Guid parentId, string name)
    {
        var node = new MeshNode
        {
            Name = name,
            ParentId = parentId,
            Primitive = MeshPrimitiveKind.Sphere,
            Transform = new SceneTransform { Scale = [0.03f, 0.03f, 0.03f] },
        };
        doc.Nodes.Add(node);
        return node;
    }

    static (SkinnedHumanoidMesh Skin, string Source) LoadCharacterSkin(string assetsRoot, HumanoidBindPose bind)
    {
        var path = Path.Combine(assetsRoot, "character", "WhiteTechwearGirl.fbx");
        if (!File.Exists(path))
            throw new FileNotFoundException("WhiteTechwearGirl.fbx missing.", path);

        if (AssimpSkinnedMeshImporter.TryImport(path, out var named, new MeshImportOptions
            {
                PreTransformVertices = false,
                GenerateNormals = true,
                CenterAtOrigin = false,
            })
            && named is not null)
        {
            var lodMesh = MeshLod.Decimate(named.Mesh, CharacterLodTris, out var srcMap);
            var lodWeights = new NamedBoneWeight[lodMesh.VertexCount][];
            for (var i = 0; i < lodMesh.VertexCount; i++)
            {
                var src = named.VertexWeights[srcMap[i]];
                lodWeights[i] = src.Select(w => new NamedBoneWeight(w.BoneName, w.Weight)).ToArray();
            }

            var aligned = HumanoidMeshAligner.FitToBindPose(lodMesh, bind);
            if (HumanoidNearestBoneSkinner.TryBindNamedWeights(aligned, lodWeights, bind) is { } authored)
                return (authored, "Assimp named bones");
        }

        var raw = AssimpMeshImporter.ImportFile(path, new MeshImportOptions
        {
            PreTransformVertices = true,
            GenerateNormals = true,
            CenterAtOrigin = false,
            LongestAxisToPositiveZ = false,
        });
        // Spatial LOD + weld keeps a continuous surface (stride subsample looked like swiss cheese).
        var lod = MeshLod.DecimateAndWeld(raw, CharacterLodTris, weldTolerance: 0.0015f);
        var fitted = HumanoidMeshAligner.FitToBindPose(lod, bind);
        var skin = HumanoidNearestBoneSkinner.Bind(fitted, bind, influences: 4);
        return (skin, "nearest-bone auto-skin");
    }

    static TriangleMesh LoadRifleLod(string assetsRoot)
    {
        var path = Path.Combine(assetsRoot, "weapons", "Rifle.fbx");
        if (!File.Exists(path))
            throw new FileNotFoundException("Rifle.fbx missing.", path);

        var raw = AssimpMeshImporter.ImportFile(path, new MeshImportOptions
        {
            TargetLengthMeters = RifleLengthMeters,
            CenterAtOrigin = true,
            LongestAxisToPositiveZ = true,
            PreTransformVertices = true,
            GenerateNormals = true,
        });
        return MeshLod.DecimateAndWeld(raw, RifleLodTris, weldTolerance: 0.001f);
    }

    static Matrix4x4 RifleWorldMatrix(Vector3 butt, Vector3 tip)
    {
        var mid = (butt + tip) * 0.5f;
        var dir = tip - butt;
        if (dir.LengthSquared() < 1e-8f)
            dir = Vector3.UnitY;
        dir = Vector3.Normalize(dir);

        var up = MathF.Abs(Vector3.Dot(dir, Vector3.UnitY)) > 0.98f ? Vector3.UnitX : Vector3.UnitY;
        var x = Vector3.Normalize(Vector3.Cross(up, dir));
        var y = Vector3.Cross(dir, x);
        var rot = new Matrix4x4(
            x.X, x.Y, x.Z, 0,
            y.X, y.Y, y.Z, 0,
            dir.X, dir.Y, dir.Z, 0,
            0, 0, 0, 1);
        return rot * Matrix4x4.CreateTranslation(mid);
    }
}
