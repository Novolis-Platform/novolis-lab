using System.Numerics;
using System.Runtime.InteropServices;
using Novolis.Physics.Collision.Simple;
using Novolis.Physics.Joints;
using Novolis.Simulation.World.Builders;

namespace RagdollPlay.Game;

internal sealed class RagdollBody
{
    public const float SphereRadius = 0.2f;

    private readonly ConstrainedSphereSimulator _simulator = new()
    {
        Options =
        {
            Radius = SphereRadius,
            LinearDragPerSecond = 0.18,
            SphereRestitution = 0.25f,
            StaticRestitution = 0.35f,
            MaxSpeedMps = 12f,
        },
        JointIterations = 16,
        JointRelaxIterations = 4,
        AngularIterations = 2,
        InternalCollisionIterations = 4,
        ConstraintPasses = 2,
    };

    private readonly List<SphereState> _spheres = [];
    private readonly List<DistanceJoint> _joints = [];
    private readonly List<SwingLimit> _swingLimits = [];
    private readonly List<HingeLimit> _hingeLimits = [];
    private InteriorClampVolume _clamp;

    public IReadOnlyList<SphereState> Spheres => _spheres;
    public IReadOnlyList<DistanceJoint> Joints => _joints;
    public int LastJointCorrections => _simulator.LastJointCorrections;
    public int LastAngularCorrections => _simulator.LastAngularCorrections;
    public int LastInternalFixes => _simulator.LastInternalCollisionFixes;

    public void SpawnStanding(Vector3 groundPoint, PlayRoom room)
    {
        _clamp = room.InteriorBounds.ToInteriorClamp();

        RagdollHumanoidPreset.BuildStanding(
            groundPoint,
            _spheres,
            _joints,
            _swingLimits,
            _hingeLimits,
            runtimeStiffness: 0.65f);

        _simulator.SetJoints(CollectionsMarshal.AsSpan(_joints));
        _simulator.DepenetrateSpawnedRange(_spheres, 0, _spheres.Count, _clamp);
        RagdollHumanoidPreset.StabilizeSpawn(
            _spheres,
            CollectionsMarshal.AsSpan(_joints),
            _clamp,
            _simulator,
            spawnStiffness: 0.85f);
    }

    public void ApplyImpulse(int sphereIndex, Vector3 impulse)
    {
        if ((uint)sphereIndex >= (uint)_spheres.Count)
            return;

        _spheres[sphereIndex].Velocity += impulse;
        _spheres[sphereIndex].IsSleeping = false;
        WakeAll();
    }

    public void Step(BvhStaticWorld world, float deltaSeconds)
    {
        _simulator.SetJoints(CollectionsMarshal.AsSpan(_joints));
        _simulator.Step(
            world,
            _spheres,
            _clamp,
            deltaSeconds,
            CollectionsMarshal.AsSpan(_swingLimits),
            CollectionsMarshal.AsSpan(_hingeLimits));
    }

    private void WakeAll()
    {
        foreach (var s in _spheres)
            s.IsSleeping = false;
        _simulator.MarkPileUnsettled();
    }
}
