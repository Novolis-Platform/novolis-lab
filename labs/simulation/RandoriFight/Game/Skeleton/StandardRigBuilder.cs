using System.Drawing;
using System.Numerics;
using Novolis.Simulation.Humanoid;

namespace RandoriFight.Game.Skeleton;

/// <summary>Maps solved joints to drawable segments.</summary>
internal static class StandardRigBuilder
{
    public static IReadOnlyList<RigSegment> BuildSegments(SkeletonFrame s, bool isPlayer)
    {
        var torso = isPlayer
            ? Color.FromArgb(255, 210, 198, 178)
            : Color.FromArgb(255, 188, 198, 218);
        var limb = isPlayer
            ? Color.FromArgb(255, 198, 158, 112)
            : Color.FromArgb(255, 178, 188, 208);
        var head = Color.FromArgb(255, 215, 175, 135);
        var dark = Color.FromArgb(255, 42, 40, 48);

        return
        [
            Seg(s[HumanoidBone.Hips], s[HumanoidBone.Spine2], 0.13f, torso, RigSegmentKind.Torso),
            Seg(s[HumanoidBone.Spine2], s[HumanoidBone.Neck], 0.08f, torso, RigSegmentKind.Torso),
            Seg(s[HumanoidBone.Neck], s[HumanoidBone.Head], 0.1f, head, RigSegmentKind.Head),
            Seg(s[HumanoidBone.LeftUpLeg], s[HumanoidBone.LeftLeg], 0.095f, limb, RigSegmentKind.Limb),
            Seg(s[HumanoidBone.LeftLeg], s[HumanoidBone.LeftFoot], 0.08f, limb, RigSegmentKind.Limb),
            Seg(s[HumanoidBone.LeftFoot], s[HumanoidBone.LeftToeBase], 0.055f, dark, RigSegmentKind.Extremity),
            Seg(s[HumanoidBone.RightUpLeg], s[HumanoidBone.RightLeg], 0.095f, limb, RigSegmentKind.Limb),
            Seg(s[HumanoidBone.RightLeg], s[HumanoidBone.RightFoot], 0.08f, limb, RigSegmentKind.Limb),
            Seg(s[HumanoidBone.RightFoot], s[HumanoidBone.RightToeBase], 0.055f, dark, RigSegmentKind.Extremity),
            Seg(s[HumanoidBone.LeftArm], s[HumanoidBone.LeftForeArm], 0.072f, limb, RigSegmentKind.Limb),
            Seg(s[HumanoidBone.LeftForeArm], s[HumanoidBone.LeftHand], 0.062f, limb, RigSegmentKind.Limb),
            Seg(s[HumanoidBone.RightArm], s[HumanoidBone.RightForeArm], 0.072f, limb, RigSegmentKind.Limb),
            Seg(s[HumanoidBone.RightForeArm], s[HumanoidBone.RightHand], 0.062f, limb, RigSegmentKind.Limb),
        ];
    }

    public static IReadOnlyList<RigSegment> BuildSkeletonBones(SkeletonFrame s) =>
    [
        Seg(s[HumanoidBone.Hips], s[HumanoidBone.Spine2], 0.05f, default, RigSegmentKind.Torso),
        Seg(s[HumanoidBone.Spine2], s[HumanoidBone.Head], 0.04f, default, RigSegmentKind.Torso),
        Seg(s[HumanoidBone.LeftUpLeg], s[HumanoidBone.LeftLeg], 0.042f, default, RigSegmentKind.Limb),
        Seg(s[HumanoidBone.LeftLeg], s[HumanoidBone.LeftFoot], 0.038f, default, RigSegmentKind.Limb),
        Seg(s[HumanoidBone.RightUpLeg], s[HumanoidBone.RightLeg], 0.042f, default, RigSegmentKind.Limb),
        Seg(s[HumanoidBone.RightLeg], s[HumanoidBone.RightFoot], 0.038f, default, RigSegmentKind.Limb),
        Seg(s[HumanoidBone.LeftArm], s[HumanoidBone.LeftForeArm], 0.032f, default, RigSegmentKind.Limb),
        Seg(s[HumanoidBone.LeftForeArm], s[HumanoidBone.LeftHand], 0.028f, default, RigSegmentKind.Limb),
        Seg(s[HumanoidBone.RightArm], s[HumanoidBone.RightForeArm], 0.032f, default, RigSegmentKind.Limb),
        Seg(s[HumanoidBone.RightForeArm], s[HumanoidBone.RightHand], 0.028f, default, RigSegmentKind.Limb),
    ];

    private static RigSegment Seg(Vector3 a, Vector3 b, float radius, Color color, RigSegmentKind kind) =>
        new(a, b, radius, color, kind);
}
