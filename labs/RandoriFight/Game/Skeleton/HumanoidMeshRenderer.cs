using System.Drawing;
using System.Numerics;
using Novolis.Raylib.Game;
using Novolis.Simulation.Humanoid;

namespace RandoriFight.Game.Skeleton;

internal static class HumanoidMeshRenderer
{
    private static readonly Color Blade = Color.FromArgb(255, 198, 208, 222);
    private static readonly Color BladeEdge = Color.FromArgb(255, 240, 246, 255);
    private static readonly Color Tsuba = Color.FromArgb(255, 58, 48, 42);
    private static readonly Color Sageo = Color.FromArgb(255, 168, 42, 48);
    private static readonly Color CutTrail = Color.FromArgb(100, 255, 220, 160);

    public static void Draw(
        RayGameContext ctx,
        SkeletonFrame skeleton,
        bool isPlayer,
        bool showSkeleton,
        bool hitFlash,
        bool strikeWindow,
        bool showCutTrail)
    {
        BodyMeshRenderer.Draw(ctx, skeleton, isPlayer, hitFlash);

        if (showSkeleton)
        {
            var bones = StandardRigBuilder.BuildSkeletonBones(skeleton);
            MetaballMesh.DrawSkeletonOverlay(ctx, bones);
        }

        DrawKatana(ctx, skeleton, strikeWindow, showCutTrail);
    }

    private static void DrawKatana(RayGameContext ctx, SkeletonFrame s, bool strikeWindow, bool showCutTrail)
    {
        var root = s.BladeRoot;
        var tip = s.BladeTip;
        ctx.DrawBolt(root, tip, Blade);
        var tsuba = Vector3.Lerp(root, s[HumanoidBone.RightHand], 0.32f);
        ctx.DrawGlowSphere(tsuba, 0.048f, Tsuba);
        ctx.DrawGlowSphere(root, 0.035f, Sageo);
        ctx.DrawGlowSphere(tip, 0.032f, BladeEdge);

        if (strikeWindow)
            ctx.DrawGlowSphereWires(tip, 0.2f, CutTrail);

        if (showCutTrail)
            ctx.DrawBolt(Vector3.Lerp(root, tip, 0.55f), tip, CutTrail);
    }
}
