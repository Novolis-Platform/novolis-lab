using System.Numerics;
using Novolis.Simulation.Humanoid;

namespace CharacterLab.Ui;

internal readonly record struct MannequinCapsule(Vector3 A, Vector3 B, float RadiusMeters);

/// <summary>Painter-style capsules from a solved world pose (readable human shape over sticks).</summary>
internal static class MannequinBuilder
{
    public static MannequinCapsule[] FromWorldPose(HumanoidWorldPose world)
    {
        var hips = world.Position(HumanoidBone.Hips);
        var lHip = world.Position(HumanoidBone.LeftUpLeg);
        var rHip = world.Position(HumanoidBone.RightUpLeg);
        // Pelvis bar — without this both thighs appear to fan from a single point.
        var pelvisL = Vector3.Lerp(hips, lHip, 1f);
        var pelvisR = Vector3.Lerp(hips, rHip, 1f);

        return
        [
            new MannequinCapsule(pelvisL, pelvisR, 0.11f),
            Cap(world, HumanoidBone.Hips, HumanoidBone.Spine2, 0.12f),
            Cap(world, HumanoidBone.Spine2, HumanoidBone.Neck, 0.07f),
            Cap(world, HumanoidBone.Neck, HumanoidBone.Head, 0.08f),
            // Full leg chains (hip socket → knee → ankle → toe)
            Cap(world, HumanoidBone.Hips, HumanoidBone.LeftUpLeg, 0.09f),
            Cap(world, HumanoidBone.LeftUpLeg, HumanoidBone.LeftLeg, 0.095f),
            Cap(world, HumanoidBone.LeftLeg, HumanoidBone.LeftFoot, 0.075f),
            Cap(world, HumanoidBone.LeftFoot, HumanoidBone.LeftToeBase, 0.05f),
            Cap(world, HumanoidBone.Hips, HumanoidBone.RightUpLeg, 0.09f),
            Cap(world, HumanoidBone.RightUpLeg, HumanoidBone.RightLeg, 0.095f),
            Cap(world, HumanoidBone.RightLeg, HumanoidBone.RightFoot, 0.075f),
            Cap(world, HumanoidBone.RightFoot, HumanoidBone.RightToeBase, 0.05f),
            Cap(world, HumanoidBone.Spine2, HumanoidBone.LeftShoulder, 0.06f),
            Cap(world, HumanoidBone.LeftShoulder, HumanoidBone.LeftArm, 0.07f),
            Cap(world, HumanoidBone.LeftArm, HumanoidBone.LeftForeArm, 0.06f),
            Cap(world, HumanoidBone.LeftForeArm, HumanoidBone.LeftHand, 0.05f),
            Cap(world, HumanoidBone.Spine2, HumanoidBone.RightShoulder, 0.06f),
            Cap(world, HumanoidBone.RightShoulder, HumanoidBone.RightArm, 0.07f),
            Cap(world, HumanoidBone.RightArm, HumanoidBone.RightForeArm, 0.06f),
            Cap(world, HumanoidBone.RightForeArm, HumanoidBone.RightHand, 0.05f),
        ];
    }

    public static Vector3 HeadCenter(HumanoidWorldPose world) => world.Position(HumanoidBone.Head);

    static MannequinCapsule Cap(HumanoidWorldPose world, HumanoidBone a, HumanoidBone b, float r) =>
        new(world.Position(a), world.Position(b), r);
}
