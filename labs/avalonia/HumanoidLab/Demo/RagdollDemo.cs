using System.Numerics;
using System.Runtime.InteropServices;
using Novolis.Math.Arrays;
using Novolis.Math.Geometry;
using Novolis.Physics.Collision.Simple;
using Novolis.Physics.Joints;
using Novolis.Simulation.Humanoid;
using Novolis.Simulation.Humanoid.Skinning;
using Novolis.Simulation.World;
using Novolis.Simulation.World.Builders;
using HumanoidLab.Ui;

namespace HumanoidLab.Demo;

/// <summary>
/// Standing humanoid ragdoll with AdaptiveMesh hull. Runtime uses distance joints only
/// (angular limits inject energy when frames tumble) plus explicit entropy damping.
/// </summary>
internal sealed class RagdollDemo
{
    private const float SphereRadius = 0.2f;
    private const float FixedDt = 1f / 60f;
    private const uint GridSize = 12;
    private const float CellSize = 1f;
    private const float WallHeight = 4f;

    private readonly ConstrainedSphereSimulator _simulator = new()
    {
        Options =
        {
            Radius = SphereRadius,
            LinearDragPerSecond = 1.35,
            SphereRestitution = 0.0f,
            StaticRestitution = 0.0f,
            GroundFrictionPerSecond = 22.0,
            SleepSpeedThreshold = 0.12f,
            MaxSpeedMps = 6f,
            FloorHeight = 0f,
            GroundContactSlack = 0.08f,
        },
        JointIterations = 20,
        JointRelaxIterations = 6,
        AngularIterations = 0,
        InternalCollisionIterations = 0,
        ConstraintPasses = 2,
    };

    private readonly List<SphereState> _spheres = [];
    private readonly List<DistanceJoint> _joints = [];
    private readonly List<SwingLimit> _swingLimits = [];
    private readonly List<HingeLimit> _hingeLimits = [];
    private readonly float[] _restLengths;
    private readonly BvhStaticWorld _world;
    private readonly InteriorClampVolume _clamp;
    private readonly Vector3 _floorCenter;
    private AdaptiveMesh _adaptiveBody;
    private Vector3[] _meshScratch;
    private int[] _meshIndices;
    private readonly Vector3[] _handlePositions = new Vector3[HumanoidAdaptiveBody.SphereCount];
    private readonly Vector3[] _prevPositions;
    private (float MinU, float MaxU, float MinV, float MaxV) _viewBounds;
    private float _time;
    private float _accum;
    private bool _tipped;
    private float _entropyPerSecond = 3.2f;
    private float _autoTipAt = 1.0f;
    private bool _autoTipEnabled = true;

    public RagdollDemo(HumanoidBindPose bind)
    {
        _ = bind;
        _world = BuildRoom(out _floorCenter, out var interior);
        _clamp = interior.ToInteriorClamp();
        _prevPositions = new Vector3[RagdollHumanoidPreset.SphereCount];
        _restLengths = new float[10];
        _adaptiveBody = null!;
        _meshScratch = [];
        _meshIndices = [];
        Reset();
    }

    public float EntropyPerSecond
    {
        get => _entropyPerSecond;
        set => _entropyPerSecond = System.Math.Clamp(value, 0f, 20f);
    }

    public bool AutoTipEnabled
    {
        get => _autoTipEnabled;
        set => _autoTipEnabled = value;
    }

    public float TimeSeconds => _time;
    public bool Tipped => _tipped;

    public void Reset()
    {
        RagdollHumanoidPreset.BuildStanding(
            _floorCenter,
            _spheres,
            _joints,
            _swingLimits,
            _hingeLimits,
            runtimeStiffness: 0.65f);

        for (var i = 0; i < _joints.Count; i++)
        {
            var j = _joints[i];
            _joints[i] = new DistanceJoint(j.SphereA, j.SphereB, j.RestLength, stiffness: 1f);
            _restLengths[i] = j.RestLength;
        }

        _simulator.SetJoints(CollectionsMarshal.AsSpan(_joints));
        _simulator.DepenetrateSpawnedRange(_spheres, 0, _spheres.Count, _clamp);
        RagdollHumanoidPreset.StabilizeSpawn(
            _spheres,
            CollectionsMarshal.AsSpan(_joints),
            _clamp,
            _simulator,
            spawnStiffness: 0.85f);

        foreach (var s in _spheres)
        {
            s.Velocity = Vector3.Zero;
            s.IsSleeping = false;
            s.IsGrounded = false;
        }

        _simulator.ResetPileState();

        var bindCenters = new Vector3[_spheres.Count];
        for (var i = 0; i < _spheres.Count; i++)
        {
            bindCenters[i] = _spheres[i].Position;
            _prevPositions[i] = _spheres[i].Position;
        }

        _adaptiveBody = HumanoidAdaptiveBody.CreateFromRagdollBind(bindCenters);
        _meshScratch = new Vector3[_adaptiveBody.VertexCount];
        _meshIndices = _adaptiveBody.Indices.ToArray();

        _viewBounds = (
            _floorCenter.X - 1.8f,
            _floorCenter.X + 2.6f,
            -0.05f,
            2.15f);

        _time = 0f;
        _accum = 0f;
        _tipped = false;
    }

    public void Tip(Vector3? impulse = null)
    {
        var push = impulse ?? new Vector3(1.6f, 0.35f, 0.1f);
        var chest = _spheres[HumanoidRagdollMap.RagdollChest];
        chest.Velocity += push;
        chest.IsSleeping = false;
        foreach (var s in _spheres)
            s.IsSleeping = false;
        _simulator.MarkPileUnsettled();
        _tipped = true;
    }

    public RagdollStatus Snapshot()
    {
        float maxSp = 0f, ke = 0f, boneErr = 0f, minY = float.MaxValue, maxY = float.MinValue;
        var sleeping = 0;
        for (var i = 0; i < _spheres.Count; i++)
        {
            var s = _spheres[i];
            var sp = s.Velocity.Length();
            maxSp = System.Math.Max(maxSp, sp);
            ke += 0.5f * sp * sp;
            minY = System.Math.Min(minY, s.Position.Y);
            maxY = System.Math.Max(maxY, s.Position.Y);
            if (s.IsSleeping)
                sleeping++;
        }

        for (var i = 0; i < _joints.Count; i++)
        {
            var j = _joints[i];
            var d = Vector3.Distance(_spheres[j.SphereA].Position, _spheres[j.SphereB].Position);
            boneErr = System.Math.Max(boneErr, System.Math.Abs(d - _restLengths[i]));
        }

        var hip = _spheres[HumanoidRagdollMap.RagdollHip].Position;
        return new RagdollStatus(
            _time,
            _tipped,
            maxSp,
            ke,
            boneErr,
            sleeping,
            _spheres.Count,
            minY,
            maxY,
            hip,
            _entropyPerSecond,
            _autoTipEnabled);
    }

    public void Tick(float dt, StickFigurePane pane)
    {
        _time += dt;
        _accum += System.Math.Clamp(dt, 0f, 0.05f);

        if (_autoTipEnabled && !_tipped && _time >= _autoTipAt)
            Tip();

        var steps = 0;
        var statusBefore = Snapshot();
        var frozen = statusBefore.Sleeping == statusBefore.SphereCount && statusBefore.MaxSpeed < 0.15f;
        if (!frozen)
        {
            while (_accum >= FixedDt && steps < 3)
            {
                _accum -= FixedDt;
                steps++;
                StepFixed(FixedDt);
            }
        }
        else
        {
            _accum = 0f;
        }

        if (steps >= 3)
            _accum = 0f;

        var centers = new Vector3[_spheres.Count];
        for (var i = 0; i < _spheres.Count; i++)
            centers[i] = _spheres[i].Position;

        HumanoidAdaptiveBody.CopySphereCenters(centers, _handlePositions);
        _adaptiveBody.Adapt(_handlePositions, _meshScratch);

        var status = Snapshot();
        pane.ViewMode = StickViewMode.FrontXy;
        pane.FixedViewBounds = _viewBounds;
        pane.ClipToBounds = true;
        pane.Caption = !_tipped
            ? "Ragdoll — standing (tips in 1s)"
            : status.Sleeping == status.SphereCount
                ? $"Ragdoll — at rest (KE={status.KineticEnergy:F3})"
                : $"Ragdoll — settling (vMax={status.MaxSpeed:F2} KE={status.KineticEnergy:F2})";
        pane.ClearExtras();
        pane.SetJointDots(centers);
        pane.SetBoneGuides(MannequinBuilder.SkeletonFromRagdollSpheres(_spheres));
        pane.SetAdaptiveMesh(_meshScratch, _meshIndices);
    }

    private void StepFixed(float dt)
    {
        for (var i = 0; i < _spheres.Count; i++)
            _prevPositions[i] = _spheres[i].Position;

        // Distance joints only — angular limits fight tumbling frames and pump energy.
        _simulator.SetJoints(CollectionsMarshal.AsSpan(_joints));
        _simulator.Step(_world, _spheres, _clamp, dt);

        EnforceBoneLengths();
        SyncVelocitiesFromPositions(dt);
        ApplyEntropy(dt);
        ForceSleepWhenQuiet();
    }

    private void EnforceBoneLengths()
    {
        for (var pass = 0; pass < 4; pass++)
        {
            for (var i = 0; i < _joints.Count; i++)
            {
                var j = _joints[i];
                var a = _spheres[j.SphereA];
                var b = _spheres[j.SphereB];
                var delta = b.Position - a.Position;
                var distSq = delta.LengthSquared();
                if (distSq < 1e-10f)
                    continue;

                var dist = MathF.Sqrt(distSq);
                var error = dist - _restLengths[i];
                if (MathF.Abs(error) < 5e-4f)
                    continue;

                var n = delta / dist;
                var corr = n * (error * 0.5f);
                a.Position += corr;
                b.Position -= corr;
            }
        }
    }

    /// <summary>PBD-style velocity update so position projections do not leave phantom kinetic energy.</summary>
    private void SyncVelocitiesFromPositions(float dt)
    {
        var invDt = 1f / dt;
        for (var i = 0; i < _spheres.Count; i++)
        {
            var s = _spheres[i];
            var fromPos = (_spheres[i].Position - _prevPositions[i]) * invDt;
            // Blend measured velocity toward positional change (kills constraint-injected energy).
            s.Velocity = Vector3.Lerp(s.Velocity, fromPos, 0.65f);
        }
    }

    private void ApplyEntropy(float dt)
    {
        var damp = MathF.Exp(-_entropyPerSecond * dt);
        foreach (var s in _spheres)
        {
            s.Velocity *= damp;
            if (s.Position.Y <= SphereRadius + 0.12f)
                s.Velocity *= MathF.Exp(-4.5f * dt);
        }
    }

    private void ForceSleepWhenQuiet()
    {
        var maxSp = 0f;
        foreach (var s in _spheres)
            maxSp = System.Math.Max(maxSp, s.Velocity.Length());

        if (maxSp >= 0.15f)
            return;

        foreach (var s in _spheres)
        {
            s.Velocity = Vector3.Zero;
            s.IsSleeping = true;
            s.IsGrounded = true;
        }
    }

    private static BvhStaticWorld BuildRoom(out Vector3 floorCenter, out RoomInteriorBounds interior)
    {
        var walls = new DenseGrid<byte>(GridSize, GridSize);
        for (var y = 0u; y < GridSize; y++)
        for (var x = 0u; x < GridSize; x++)
        {
            var border = x == 0 || y == 0 || x == GridSize - 1 || y == GridSize - 1;
            walls[x, y, 0] = border ? (byte)1 : (byte)0;
        }

        var cells = new byte[GridSize * GridSize];
        for (var y = 0u; y < GridSize; y++)
        for (var x = 0u; x < GridSize; x++)
            cells[y * GridSize + x] = walls[x, y, 0];

        var collision = OccupancyEnclosedRoomMeshBuilder.FromWallGrid(
            GridSize,
            GridSize,
            cells,
            CellSize,
            WallHeight);
        var xz = GridSize * CellSize * 0.5f;
        floorCenter = new Vector3(xz, 0f, xz);
        interior = RoomInteriorBounds.ForOccupancyGrid(GridSize, GridSize, CellSize, WallHeight, SphereRadius);
        return collision;
    }
}

internal readonly record struct RagdollStatus(
    float TimeSeconds,
    bool Tipped,
    float MaxSpeed,
    float KineticEnergy,
    float BoneError,
    int Sleeping,
    int SphereCount,
    float MinY,
    float MaxY,
    Vector3 Hip,
    float EntropyPerSecond,
    bool AutoTipEnabled);
