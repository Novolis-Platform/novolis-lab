using System.Numerics;
using Novolis.Simulation.Humanoid;
using RandoriFight.Game;

namespace RandoriFight.Game.Skeleton;

/// <summary>Builds a posable skeleton from katana pose landmarks using platform two-bone IK.</summary>
internal static class HumanoidSkeleton
{
    private const float UpperLeg = 0.44f;
    private const float LowerLeg = 0.44f;
    private const float UpperArm = 0.3f;
    private const float LowerArm = 0.28f;

    public static SkeletonFrame SolveFromLandmarks(KatanaPose pose, Vector3 worldRoot, int facing)
    {
        var frame = new SkeletonFrame();
        var pelvis = World(worldRoot, facing, pose.Hips);
        var chest = World(worldRoot, facing, pose.Chest);
        var head = World(worldRoot, facing, pose.Head);
        var lFoot = World(worldRoot, facing, pose.LeftFoot);
        var rFoot = World(worldRoot, facing, pose.RightFoot);
        var lHand = World(worldRoot, facing, pose.LeftHand);
        var rHand = World(worldRoot, facing, pose.RightHand);

        frame.Set(HumanoidBone.Hips, pelvis);
        frame.Set(HumanoidBone.Spine, Vector3.Lerp(pelvis, chest, 0.33f));
        frame.Set(HumanoidBone.Spine1, Vector3.Lerp(pelvis, chest, 0.66f));
        frame.Set(HumanoidBone.Spine2, chest);
        frame.Set(HumanoidBone.Neck, Vector3.Lerp(chest, head, 0.38f));
        frame.Set(HumanoidBone.Head, head);

        SolveLeg(frame, HumanoidBone.LeftUpLeg, HumanoidBone.LeftLeg, HumanoidBone.LeftFoot, HumanoidBone.LeftToeBase,
            pelvis, lFoot, hipSocket: new(-0.13f, 0.02f, 0.06f), facing, bendSign: 1f);
        SolveLeg(frame, HumanoidBone.RightUpLeg, HumanoidBone.RightLeg, HumanoidBone.RightFoot, HumanoidBone.RightToeBase,
            pelvis, rFoot, hipSocket: new(0.1f, 0.01f, -0.05f), facing, bendSign: -1f);

        var lClav = chest + Local(facing, new(-0.15f, 0.12f, 0.02f));
        var rClav = chest + Local(facing, new(0.15f, 0.12f, 0.02f));
        frame.Set(HumanoidBone.LeftShoulder, lClav);
        frame.Set(HumanoidBone.RightShoulder, rClav);

        SolveArm(frame, HumanoidBone.LeftArm, HumanoidBone.LeftForeArm, HumanoidBone.LeftHand,
            lClav, lHand, facing, bendSign: -1f);
        SolveArm(frame, HumanoidBone.RightArm, HumanoidBone.RightForeArm, HumanoidBone.RightHand,
            rClav, rHand, facing, bendSign: 1f);

        frame.BladeRoot = World(worldRoot, facing, pose.BladeRoot);
        frame.BladeTip = World(worldRoot, facing, pose.BladeTip);
        return frame;
    }

    private static void SolveLeg(
        SkeletonFrame frame,
        HumanoidBone hipId,
        HumanoidBone kneeId,
        HumanoidBone ankleId,
        HumanoidBone toeId,
        Vector3 pelvis,
        Vector3 foot,
        Vector3 hipSocket,
        int facing,
        float bendSign)
    {
        var hip = pelvis + Local(facing, hipSocket);
        var knee = TwoBoneIk.SolveMid(hip, foot, UpperLeg, LowerLeg, new Vector3(0f, 0f, bendSign * 0.85f));
        var ankle = foot + new Vector3(0f, 0.06f, 0f);
        var toe = foot + Local(facing, new(0.06f, 0f, 0.1f));

        frame.Set(hipId, hip);
        frame.Set(kneeId, knee);
        frame.Set(ankleId, ankle);
        frame.Set(toeId, toe);
    }

    private static void SolveArm(
        SkeletonFrame frame,
        HumanoidBone shoulderId,
        HumanoidBone elbowId,
        HumanoidBone handId,
        Vector3 shoulderRoot,
        Vector3 handTarget,
        int facing,
        float bendSign)
    {
        var shoulder = shoulderRoot + Local(facing, new(0f, -0.02f, 0f));
        var elbow = TwoBoneIk.SolveMid(shoulder, handTarget, UpperArm, LowerArm, new Vector3(0f, 0f, bendSign * 0.7f));

        frame.Set(shoulderId, shoulder);
        frame.Set(elbowId, elbow);
        frame.Set(handId, handTarget);
    }

    private static Vector3 World(Vector3 root, int facing, Vector3 local) =>
        root + Local(facing, local);

    private static Vector3 Local(int facing, Vector3 local) =>
        new(local.X * facing, local.Y, local.Z);
}
