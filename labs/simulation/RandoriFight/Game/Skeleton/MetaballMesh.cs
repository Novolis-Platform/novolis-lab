using System.Drawing;
using System.Numerics;
using Novolis.Raylib.Game;

namespace RandoriFight.Game.Skeleton;

/// <summary>Metaball-style skin: one envelope per joint + capsule bridges (no stacked blob chains).</summary>
internal static class MetaballMesh
{
    /// <summary>Unity/Blender-style octahedral bones with wire joint spheres.</summary>
    public static void DrawSkeletonOverlay(RayGameContext ctx, IReadOnlyList<RigSegment> bones)
    {
        var boneColor = Color.FromArgb(200, 180, 188, 198);
        var wire = Color.FromArgb(255, 90, 98, 110);
        var joint = Color.FromArgb(255, 230, 235, 245);

        foreach (var bone in bones)
        {
            DrawOctahedralBone(ctx, bone.Start, bone.End, bone.Radius * 1.15f, boneColor, wire);
            DrawJointSphere(ctx, bone.Start, bone.Radius * 0.55f, joint, wire);
        }

        if (bones.Count > 0)
        {
            var last = bones[^1];
            DrawJointSphere(ctx, last.End, last.Radius * 0.55f, joint, wire);
        }
    }

    private static void DrawCapsuleEnvelope(RayGameContext ctx, Vector3 a, Vector3 b, float radius, Color color)
    {
        var delta = b - a;
        var len = delta.Length();
        if (len < 1e-4f)
        {
            ctx.DrawGlowSphere(a, radius, color);
            return;
        }

        ctx.DrawGlowSphere(a, radius, color);
        ctx.DrawGlowSphere(b, radius, color);

        var overlap = radius * 2.05f;
        if (len > overlap * 0.55f)
        {
            var midCount = len > overlap * 1.35f ? 2 : 1;
            for (var i = 1; i <= midCount; i++)
            {
                var t = i / (float)(midCount + 1);
                var pinch = 1f - MathF.Abs(t - 0.5f) * 0.12f;
                ctx.DrawGlowSphere(Vector3.Lerp(a, b, t), radius * pinch, color);
            }
        }
    }

    private static void DrawOctahedralBone(
        RayGameContext ctx,
        Vector3 start,
        Vector3 end,
        float radius,
        Color fill,
        Color wire)
    {
        var delta = end - start;
        var len = delta.Length();
        if (len < 1e-4f)
            return;

        var dir = delta / len;
        var mid = (start + end) * 0.5f;
        var right = Vector3.Normalize(Vector3.Cross(dir, MathF.Abs(dir.Y) < 0.95f ? Vector3.UnitY : Vector3.UnitX));
        var up = Vector3.Normalize(Vector3.Cross(right, dir));

        var waist = right * radius + up * radius;
        var w1 = mid + waist;
        var w2 = mid - waist;
        var w3 = mid + right * radius - up * radius;
        var w4 = mid - right * radius + up * radius;

        FillTriangle(ctx, start, w1, w3, fill);
        FillTriangle(ctx, start, w3, w2, fill);
        FillTriangle(ctx, start, w2, w4, fill);
        FillTriangle(ctx, start, w4, w1, fill);
        FillTriangle(ctx, end, w3, w1, fill);
        FillTriangle(ctx, end, w2, w3, fill);
        FillTriangle(ctx, end, w4, w2, fill);
        FillTriangle(ctx, end, w1, w4, fill);

        ctx.DrawBolt(start, end, wire);
        ctx.DrawBolt(start, w1, wire);
        ctx.DrawBolt(start, w2, wire);
        ctx.DrawBolt(start, w3, wire);
        ctx.DrawBolt(start, w4, wire);
        ctx.DrawBolt(end, w1, wire);
        ctx.DrawBolt(end, w2, wire);
        ctx.DrawBolt(end, w3, wire);
        ctx.DrawBolt(end, w4, wire);
        ctx.DrawBolt(w1, w3, wire);
        ctx.DrawBolt(w3, w2, wire);
        ctx.DrawBolt(w2, w4, wire);
        ctx.DrawBolt(w4, w1, wire);
    }

    private static void FillTriangle(RayGameContext ctx, Vector3 a, Vector3 b, Vector3 c, Color color)
    {
        var center = (a + b + c) / 3f;
        var edge = MathF.Max(Vector3.Distance(a, b), MathF.Max(Vector3.Distance(b, c), Vector3.Distance(c, a)));
        ctx.DrawGlowSphere(center, edge * 0.22f, Color.FromArgb(90, color.R, color.G, color.B));
    }

    private static void DrawJointSphere(RayGameContext ctx, Vector3 center, float radius, Color fill, Color wire)
    {
        ctx.DrawGlowSphere(center, radius, fill);
        ctx.DrawGlowSphereWires(center, radius, wire);
    }
}
