using System.Numerics;
using Novolis.Simulation.Humanoid;

namespace RandoriFight.Game.Skeleton;

/// <summary>World-space joints for one solved humanoid pose (platform <see cref="HumanoidBone"/> + katana tips).</summary>
internal sealed class SkeletonFrame
{
    private readonly HumanoidWorldPose _world = new();

    /// <summary>Platform world pose.</summary>
    public HumanoidWorldPose World => _world;

    /// <summary>Katana blade root (app prop, not a Mixamo bone).</summary>
    public Vector3 BladeRoot { get; set; }

    /// <summary>Katana blade tip.</summary>
    public Vector3 BladeTip { get; set; }

    public Vector3 this[HumanoidBone bone] => _world.Position(bone);

    public void Set(HumanoidBone bone, Vector3 world) =>
        _world.Set(bone, world, Quaternion.Identity);
}
