using System.Drawing;
using System.Numerics;
using Novolis.Raylib.Game;
using Novolis.Simulation.Humanoid;

namespace RandoriFight.Game.Skeleton;

/// <summary>Solid body mesh from rig joints — torso box + limb capsules (no spine sphere stack).</summary>
internal static class BodyMeshRenderer
{
    public static void Draw(RayGameContext ctx, SkeletonFrame s, bool isPlayer, bool hitFlash)
    {
        var torso = isPlayer
            ? Color.FromArgb(255, 210, 198, 178)
            : Color.FromArgb(255, 188, 198, 218);
        var limb = isPlayer
            ? Color.FromArgb(255, 198, 158, 112)
            : Color.FromArgb(255, 178, 188, 208);
        var head = hitFlash
            ? Color.FromArgb(255, 255, 160, 120)
            : Color.FromArgb(255, 215, 175, 135);
        var foot = Color.FromArgb(255, 38, 36, 44);
        var joint = Color.FromArgb(255, 52, 48, 56);

        DrawTorso(ctx, s, torso);
        DrawHead(ctx, s[HumanoidBone.Head], head);

        DrawLimb(ctx, s[HumanoidBone.LeftArm], s[HumanoidBone.LeftForeArm], 0.068f, limb);
        DrawLimb(ctx, s[HumanoidBone.LeftForeArm], s[HumanoidBone.LeftHand], 0.058f, limb);
        DrawLimb(ctx, s[HumanoidBone.RightArm], s[HumanoidBone.RightForeArm], 0.068f, limb);
        DrawLimb(ctx, s[HumanoidBone.RightForeArm], s[HumanoidBone.RightHand], 0.058f, limb);

        DrawLimb(ctx, s[HumanoidBone.LeftUpLeg], s[HumanoidBone.LeftLeg], 0.082f, limb);
        DrawLimb(ctx, s[HumanoidBone.LeftLeg], s[HumanoidBone.LeftFoot], 0.072f, limb);
        DrawLimb(ctx, s[HumanoidBone.RightUpLeg], s[HumanoidBone.RightLeg], 0.082f, limb);
        DrawLimb(ctx, s[HumanoidBone.RightLeg], s[HumanoidBone.RightFoot], 0.072f, limb);

        DrawJoint(ctx, s[HumanoidBone.LeftArm], 0.05f, joint);
        DrawJoint(ctx, s[HumanoidBone.RightArm], 0.05f, joint);
        DrawJoint(ctx, s[HumanoidBone.LeftUpLeg], 0.055f, joint);
        DrawJoint(ctx, s[HumanoidBone.RightUpLeg], 0.055f, joint);
        DrawJoint(ctx, s[HumanoidBone.LeftLeg], 0.045f, joint);
        DrawJoint(ctx, s[HumanoidBone.RightLeg], 0.045f, joint);
        DrawJoint(ctx, s[HumanoidBone.LeftForeArm], 0.04f, joint);
        DrawJoint(ctx, s[HumanoidBone.RightForeArm], 0.04f, joint);

        DrawFoot(ctx, s[HumanoidBone.LeftFoot], s[HumanoidBone.LeftToeBase], foot);
        DrawFoot(ctx, s[HumanoidBone.RightFoot], s[HumanoidBone.RightToeBase], foot);
        DrawHand(ctx, s[HumanoidBone.LeftHand], limb);
        DrawHand(ctx, s[HumanoidBone.RightHand], limb);
    }

    private static void DrawTorso(RayGameContext ctx, SkeletonFrame s, Color color)
    {
        var pelvis = s[HumanoidBone.Hips];
        var neck = s[HumanoidBone.Neck];
        var top = Vector3.Lerp(neck, s[HumanoidBone.Head], 0.15f);
        var center = (pelvis + top) * 0.5f;
        var height = Vector3.Distance(pelvis, top) + 0.08f;
        ctx.DrawShipBox(center, new Vector3(0.22f, height, 0.14f), color);
    }

    private static void DrawHead(RayGameContext ctx, Vector3 head, Color color)
    {
        ctx.DrawGlowSphere(head + new Vector3(0f, 0.04f, 0f), 0.11f, color);
    }

    private static void DrawLimb(RayGameContext ctx, Vector3 a, Vector3 b, float maxRadius, Color color)
    {
        var len = Vector3.Distance(a, b);
        if (len < 1e-4f)
            return;

        var radius = Math.Min(maxRadius, len * 0.46f);
        ctx.DrawGlowSphere(a, radius, color);
        ctx.DrawGlowSphere(b, radius, color);
    }

    private static void DrawJoint(RayGameContext ctx, Vector3 p, float radius, Color color)
    {
        ctx.DrawGlowSphereWires(p, radius, Color.FromArgb(180, 40, 38, 46));
        ctx.DrawGlowSphere(p, radius * 0.85f, color);
    }

    private static void DrawHand(RayGameContext ctx, Vector3 hand, Color color) =>
        ctx.DrawGlowSphere(hand, 0.048f, color);

    private static void DrawFoot(RayGameContext ctx, Vector3 ankle, Vector3 toe, Color color)
    {
        var mid = Vector3.Lerp(ankle, toe, 0.45f);
        ctx.DrawShipBox(mid, new Vector3(0.11f, 0.05f, 0.2f), color);
    }
}
