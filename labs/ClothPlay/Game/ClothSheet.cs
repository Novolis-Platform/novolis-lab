using System.Numerics;
using System.Runtime.InteropServices;
using Novolis.Physics.Cloth;
using Novolis.Physics.Collision.Simple;
using Novolis.Physics.Joints;
using Novolis.Simulation.World.Builders;

namespace ClothPlay.Game;

internal enum ClothScenario
{
    Flag,
    DropDrape,
    DropCut,
}

/// <summary>Flag / falling cloth built from DistanceJoint grid primitives.</summary>
internal sealed class ClothSheet
{
    public const float ParticleRadius = 0.035f;
    public const int Columns = 18;
    public const int Rows = 12;
    public const float Spacing = 0.12f;

    /// <summary>If any free particle drops below this Y, the scenario setup is wrong.</summary>
    public const float GroundFailY = 0.25f;

    private readonly ClothSheetSimulator _simulator = new()
    {
        Options =
        {
            Radius = ParticleRadius,
            LinearDragPerSecond = 1.8,
            SphereRestitution = 0.02f,
            StaticRestitution = 0.08f,
            GroundFrictionPerSecond = 8.0,
            SleepSpeedThreshold = 0f,
            MaxSpeedMps = 8f,
            FloorHeight = 0f,
        },
            JointIterations = 28,
            JointRelaxIterations = 10,
            ConstraintPasses = 5,
            MaxStrainFraction = 3f,
            MaxStretchRatio = 1.06f,
            StretchLimitIterations = 16,
        };

    private readonly List<SphereState> _spheres = [];
    private readonly List<DistanceJoint> _joints = [];
    private readonly List<int> _pins = [];
    private readonly List<Vector3> _anchors = [];
    private InteriorClampVolume _clamp;
    private bool _windOn = true;
    private ClothBlade? _activeBlade;
    private int _lastSevered;

    public IReadOnlyList<SphereState> Spheres => _spheres;
    public IReadOnlyList<DistanceJoint> Joints => _joints;
    public IReadOnlyList<int> Pins => _pins;
    public int ColumnsCount => Columns;
    public int RowsCount => Rows;
    public int LastJointCorrections => _simulator.LastJointCorrections;
    public int LastSeveredJoints => _lastSevered;
    public bool WindEnabled => _windOn;
    public ClothScenario Scenario { get; private set; } = ClothScenario.Flag;
    public bool CuttingEnabled { get; private set; }
    public bool HitGround { get; private set; }
    public Vector3 WindAcceleration { get; private set; } = new(3.2f, 0f, 0.4f);

    /// <summary>Pinned flag on a pole — must not touch the floor.</summary>
    public void SpawnFlag(PlayRoom room)
    {
        Scenario = ClothScenario.Flag;
        CuttingEnabled = false;
        HitGround = false;
        _activeBlade = null;
        _windOn = true;
        room.ClearSword();

        // Top of flag near ceiling height; bottom stays well clear of the floor.
        var height = (Rows - 1) * Spacing;
        var originY = 3.35f;
        var bottomY = originY - height;
        if (bottomY < 0.8f)
            throw new InvalidOperationException($"Flag would reach Y={bottomY:F2}; raise origin or shorten sheet.");

        var origin = room.FloorCenter + new Vector3(
            -(Columns - 1) * Spacing * 0.5f,
            originY,
            -0.15f);

        Spawn(room, ClothPinMode.TopRow, origin, Vector3.UnitX, -Vector3.UnitY);
        ApplyWindSetting();
    }

    /// <summary>Drop onto horizontal katana edge (collision only).</summary>
    public void SpawnDropDrape(PlayRoom room, KatanaEdge edge = KatanaEdge.Up)
    {
        Scenario = ClothScenario.DropDrape;
        CuttingEnabled = false;
        HitGround = false;
        _windOn = false;
        var blade = room.InstallKatana(edge);
        _activeBlade = blade;
        SpawnDropAboveKatana(room, blade);
        ApplyWindSetting();
    }

    /// <summary>Drop onto katana with continuous edge cutting.</summary>
    public void SpawnDropCut(PlayRoom room, KatanaEdge edge = KatanaEdge.Up)
    {
        Scenario = ClothScenario.DropCut;
        CuttingEnabled = true;
        HitGround = false;
        _windOn = false;
        var blade = room.InstallKatana(edge);
        _activeBlade = blade;
        SpawnDropAboveKatana(room, blade);
        ApplyWindSetting();
    }

    public void ToggleWind()
    {
        _windOn = !_windOn;
        ApplyWindSetting();
        _simulator.MarkPileUnsettled();
    }

    public void SetPinMode(ClothPinMode mode, PlayRoom room)
    {
        if (Scenario != ClothScenario.Flag || _spheres.Count == 0)
            return;

        var origin = _anchors.Count > 0 ? _anchors[0] : _spheres[0].Position;
        Spawn(room, mode, origin, Vector3.UnitX, -Vector3.UnitY);
        ApplyWindSetting();
    }

    public void ApplyImpulse(int particleIndex, Vector3 impulse)
    {
        if ((uint)particleIndex >= (uint)_spheres.Count)
            return;
        if (_pins.Contains(particleIndex))
            return;

        _spheres[particleIndex].Velocity += impulse;
        _spheres[particleIndex].IsSleeping = false;
        _simulator.MarkPileUnsettled();
    }

    public ClothCutResult DetonateBlast(Vector3 epicenter, float radius, float impulseSpeed)
    {
        var blast = new ClothBlast(epicenter, radius, impulseSpeed);
        var cut = ClothCutOps.CutWithBlast(_joints, _spheres, blast);
        ClothCutOps.ApplyBlastImpulse(_spheres, blast, CollectionsMarshal.AsSpan(_pins));
        _simulator.SetJoints(_joints);
        _simulator.MarkPileUnsettled();
        _lastSevered = cut.SeveredJointCount;
        return cut;
    }

    public void Step(BvhStaticWorld world, float deltaSeconds)
    {
        if (CuttingEnabled && _activeBlade is { } blade)
        {
            var cut = ClothCutOps.CutWithBlade(_joints, _spheres, blade);
            if (cut.SeveredJointCount > 0)
            {
                _simulator.SetJoints(_joints);
                _lastSevered += cut.SeveredJointCount;
            }
        }

        _simulator.Step(world, _spheres, _clamp, deltaSeconds);
        UpdateGroundWatch();
    }

    private void UpdateGroundWatch()
    {
        for (var i = 0; i < _spheres.Count; i++)
        {
            if (_pins.Contains(i))
                continue;
            if (_spheres[i].Position.Y < GroundFailY)
            {
                HitGround = true;
                return;
            }
        }
    }

    private void SpawnDropAboveKatana(PlayRoom room, ClothBlade contactBlade)
    {
        var width = (Columns - 1) * Spacing;
        var depth = (Rows - 1) * Spacing;
        // Sheet in XZ, just above the contact ridge — drapes onto steel, not the floor.
        var dropY = contactBlade.Heel.Y + 0.28f;
        var origin = room.FloorCenter + new Vector3(-width * 0.5f, dropY, -depth * 0.5f);
        Spawn(room, ClothPinMode.None, origin, Vector3.UnitX, Vector3.UnitZ);

        foreach (var s in _spheres)
            s.Velocity = new Vector3(0f, -0.6f, 0f);
    }

    private void Spawn(
        PlayRoom room,
        ClothPinMode pinMode,
        Vector3 origin,
        Vector3 right,
        Vector3 down)
    {
        _clamp = room.InteriorBounds.ToInteriorClamp();
        _lastSevered = 0;

        var options = new ClothSheetOptions
        {
            Columns = Columns,
            Rows = Rows,
            Spacing = Spacing,
            StructuralStiffness = 1f,
            IncludeShear = true,
            ShearStiffness = 1f,
            IncludeBend = true,
            BendStiffness = 0.85f,
            PinMode = pinMode,
        };

        ClothSheetPreset.BuildHanging(
            origin,
            right,
            down,
            options,
            _spheres,
            _joints,
            _pins,
            _anchors);

        _simulator.SetJoints(_joints);
        _simulator.SetPins(CollectionsMarshal.AsSpan(_pins), CollectionsMarshal.AsSpan(_anchors));
        _simulator.ResetPileState();
    }

    private void ApplyWindSetting() =>
        _simulator.WindAcceleration = _windOn ? WindAcceleration : Vector3.Zero;
}
