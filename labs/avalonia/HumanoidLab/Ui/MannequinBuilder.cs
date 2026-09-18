using System.Numerics;
using Novolis.Physics.Collision.Simple;
using Novolis.Simulation.Humanoid;

namespace HumanoidLab.Ui;

/// <summary>One limb of a painter-style capsule mannequin (the readable human shape).</summary>
internal readonly record struct MannequinCapsule(Vector3 A, Vector3 B, float RadiusMeters);

/// <summary>Builds limb capsules from FK pose or ragdoll spheres (RagdollPlay-style).</summary>
internal static class MannequinBuilder
{
    private static readonly (int A, int B, float R, HumanoidBone From, HumanoidBone To)[] RagdollLimbs =
    [
        (HumanoidRagdollMap.RagdollHip, HumanoidRagdollMap.RagdollChest, 0.13f, HumanoidBone.Hips, HumanoidBone.Spine2),
        (HumanoidRagdollMap.RagdollChest, HumanoidRagdollMap.RagdollHead, 0.08f, HumanoidBone.Spine2, HumanoidBone.Head),
        (HumanoidRagdollMap.RagdollHip, HumanoidRagdollMap.RagdollLeftKnee, 0.10f, HumanoidBone.Hips, HumanoidBone.LeftLeg),
        (HumanoidRagdollMap.RagdollLeftKnee, HumanoidRagdollMap.RagdollLeftFoot, 0.08f, HumanoidBone.LeftLeg, HumanoidBone.LeftFoot),
        (HumanoidRagdollMap.RagdollHip, HumanoidRagdollMap.RagdollRightKnee, 0.10f, HumanoidBone.Hips, HumanoidBone.RightLeg),
        (HumanoidRagdollMap.RagdollRightKnee, HumanoidRagdollMap.RagdollRightFoot, 0.08f, HumanoidBone.RightLeg, HumanoidBone.RightFoot),
        (HumanoidRagdollMap.RagdollChest, HumanoidRagdollMap.RagdollLeftShoulder, 0.07f, HumanoidBone.Spine2, HumanoidBone.LeftArm),
        (HumanoidRagdollMap.RagdollLeftShoulder, HumanoidRagdollMap.RagdollLeftHand, 0.06f, HumanoidBone.LeftArm, HumanoidBone.LeftHand),
        (HumanoidRagdollMap.RagdollChest, HumanoidRagdollMap.RagdollRightShoulder, 0.07f, HumanoidBone.Spine2, HumanoidBone.RightArm),
        (HumanoidRagdollMap.RagdollRightShoulder, HumanoidRagdollMap.RagdollRightHand, 0.06f, HumanoidBone.RightArm, HumanoidBone.RightHand),
    ];

    public static MannequinCapsule[] FromWorldPose(HumanoidWorldPose world) =>
    [
        Cap(world, HumanoidBone.Hips, HumanoidBone.Spine2, 0.13f),
        Cap(world, HumanoidBone.Spine2, HumanoidBone.Head, 0.08f),
        Cap(world, HumanoidBone.Hips, HumanoidBone.LeftLeg, 0.10f),
        Cap(world, HumanoidBone.LeftLeg, HumanoidBone.LeftFoot, 0.08f),
        Cap(world, HumanoidBone.Hips, HumanoidBone.RightLeg, 0.10f),
        Cap(world, HumanoidBone.RightLeg, HumanoidBone.RightFoot, 0.08f),
        Cap(world, HumanoidBone.Spine2, HumanoidBone.LeftArm, 0.07f),
        Cap(world, HumanoidBone.LeftArm, HumanoidBone.LeftHand, 0.06f),
        Cap(world, HumanoidBone.Spine2, HumanoidBone.RightArm, 0.07f),
        Cap(world, HumanoidBone.RightArm, HumanoidBone.RightHand, 0.06f),
    ];

    public static MannequinCapsule[] FromRagdollSpheres(IReadOnlyList<SphereState> s)
    {
        if (s.Count < HumanoidRagdollMap.RagdollRightFoot + 1)
            return [];

        var limbs = new MannequinCapsule[RagdollLimbs.Length];
        for (var i = 0; i < RagdollLimbs.Length; i++)
        {
            var (a, b, r, _, _) = RagdollLimbs[i];
            limbs[i] = new MannequinCapsule(s[a].Position, s[b].Position, r);
        }

        return limbs;
    }

    /// <summary>
    /// Skeleton lines that match physics bones only (no invented Mixamo mid-joints from
    /// WorldPoseFromSpheres, which visually stretch relative to the 11 spheres).
    /// </summary>
    public static HumanoidBoneSegment[] SkeletonFromRagdollSpheres(IReadOnlyList<SphereState> s)
    {
        if (s.Count < HumanoidRagdollMap.RagdollRightFoot + 1)
            return [];

        var segs = new HumanoidBoneSegment[RagdollLimbs.Length];
        for (var i = 0; i < RagdollLimbs.Length; i++)
        {
            var (a, b, _, from, to) = RagdollLimbs[i];
            segs[i] = new HumanoidBoneSegment(from, to, s[a].Position, s[b].Position);
        }

        return segs;
    }

    public static Vector3 HeadCenter(HumanoidWorldPose world) => world.Position(HumanoidBone.Head);

    public static Vector3 HeadCenter(IReadOnlyList<SphereState> s) =>
        s.Count > HumanoidRagdollMap.RagdollHead ? s[HumanoidRagdollMap.RagdollHead].Position : default;

    private static MannequinCapsule Cap(HumanoidWorldPose world, HumanoidBone a, HumanoidBone b, float r) =>
        new(world.Position(a), world.Position(b), r);
}
