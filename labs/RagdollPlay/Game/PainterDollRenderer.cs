using System.Drawing;
using System.Numerics;
using Novolis.Physics.Collision.Simple;
using Novolis.Raylib.Game;

namespace RagdollPlay.Game;

/// <summary>3D capsule mannequin — limbs are sphere chains aligned to bones, not axis-aligned boxes.</summary>
internal static class PainterDollRenderer
{
    private static readonly Color Wood = Color.FromArgb(255, 196, 158, 108);
    private static readonly Color WoodHi = Color.FromArgb(255, 220, 188, 140);
    private static readonly Color WoodWire = Color.FromArgb(255, 72, 48, 28);
    private static readonly Color JointKnob = Color.FromArgb(255, 108, 72, 38);
    private static readonly Color HeadTone = Color.FromArgb(255, 228, 200, 155);

    public static void Draw(RayGameContext ctx, IReadOnlyList<SphereState> bones)
    {
        if (bones.Count < RagdollIndices.Count)
            return;

        Vector3 P(int i) => bones[i].Position;

        DrawCapsule(ctx, P(RagdollIndices.Hip), P(RagdollIndices.LeftKnee), 0.11f);
        DrawCapsule(ctx, P(RagdollIndices.Hip), P(RagdollIndices.RightKnee), 0.11f);
        DrawCapsule(ctx, P(RagdollIndices.LeftKnee), P(RagdollIndices.LeftFoot), 0.09f);
        DrawCapsule(ctx, P(RagdollIndices.RightKnee), P(RagdollIndices.RightFoot), 0.09f);
        DrawCapsule(ctx, P(RagdollIndices.Hip), P(RagdollIndices.Chest), 0.14f);
        DrawCapsule(ctx, P(RagdollIndices.Chest), P(RagdollIndices.Head), 0.09f);
        DrawCapsule(ctx, P(RagdollIndices.Chest), P(RagdollIndices.LeftShoulder), 0.08f);
        DrawCapsule(ctx, P(RagdollIndices.Chest), P(RagdollIndices.RightShoulder), 0.08f);
        DrawCapsule(ctx, P(RagdollIndices.LeftShoulder), P(RagdollIndices.LeftHand), 0.07f);
        DrawCapsule(ctx, P(RagdollIndices.RightShoulder), P(RagdollIndices.RightHand), 0.07f);

        DrawJoint(ctx, P(RagdollIndices.Hip), 0.13f);
        DrawJoint(ctx, P(RagdollIndices.LeftKnee), 0.1f);
        DrawJoint(ctx, P(RagdollIndices.RightKnee), 0.1f);
        DrawJoint(ctx, P(RagdollIndices.LeftFoot), 0.09f);
        DrawJoint(ctx, P(RagdollIndices.RightFoot), 0.09f);
        DrawJoint(ctx, P(RagdollIndices.Chest), 0.11f);
        DrawJoint(ctx, P(RagdollIndices.LeftShoulder), 0.085f);
        DrawJoint(ctx, P(RagdollIndices.RightShoulder), 0.085f);

        DrawHead(ctx, P(RagdollIndices.Head));
        DrawHand(ctx, P(RagdollIndices.LeftHand));
        DrawHand(ctx, P(RagdollIndices.RightHand));
    }

    public static bool TryPickBone(
        Vector3 rayOrigin,
        Vector3 rayDir,
        IReadOnlyList<SphereState> bones,
        float pickRadius,
        out int sphereIndex,
        out float hitDistance)
    {
        sphereIndex = -1;
        hitDistance = float.MaxValue;

        if (bones.Count < RagdollIndices.Count)
            return false;

        Vector3 P(int i) => bones[i].Position;

        TryCapsule(rayOrigin, rayDir, P(RagdollIndices.Head), P(RagdollIndices.Head), 0.2f, RagdollIndices.Head, ref sphereIndex, ref hitDistance);
        TryCapsule(rayOrigin, rayDir, P(RagdollIndices.Chest), P(RagdollIndices.Head), 0.1f, RagdollIndices.Chest, ref sphereIndex, ref hitDistance);
        TryCapsule(rayOrigin, rayDir, P(RagdollIndices.Hip), P(RagdollIndices.Chest), 0.12f, RagdollIndices.Hip, ref sphereIndex, ref hitDistance);
        TryCapsule(rayOrigin, rayDir, P(RagdollIndices.Hip), P(RagdollIndices.LeftKnee), 0.1f, RagdollIndices.LeftKnee, ref sphereIndex, ref hitDistance);
        TryCapsule(rayOrigin, rayDir, P(RagdollIndices.Hip), P(RagdollIndices.RightKnee), 0.1f, RagdollIndices.RightKnee, ref sphereIndex, ref hitDistance);
        TryCapsule(rayOrigin, rayDir, P(RagdollIndices.LeftKnee), P(RagdollIndices.LeftFoot), 0.09f, RagdollIndices.LeftFoot, ref sphereIndex, ref hitDistance);
        TryCapsule(rayOrigin, rayDir, P(RagdollIndices.RightKnee), P(RagdollIndices.RightFoot), 0.09f, RagdollIndices.RightFoot, ref sphereIndex, ref hitDistance);
        TryCapsule(rayOrigin, rayDir, P(RagdollIndices.Chest), P(RagdollIndices.LeftShoulder), 0.08f, RagdollIndices.LeftShoulder, ref sphereIndex, ref hitDistance);
        TryCapsule(rayOrigin, rayDir, P(RagdollIndices.Chest), P(RagdollIndices.RightShoulder), 0.08f, RagdollIndices.RightShoulder, ref sphereIndex, ref hitDistance);
        TryCapsule(rayOrigin, rayDir, P(RagdollIndices.LeftShoulder), P(RagdollIndices.LeftHand), 0.07f, RagdollIndices.LeftHand, ref sphereIndex, ref hitDistance);
        TryCapsule(rayOrigin, rayDir, P(RagdollIndices.RightShoulder), P(RagdollIndices.RightHand), 0.07f, RagdollIndices.RightHand, ref sphereIndex, ref hitDistance);

        return sphereIndex >= 0;
    }

    private static void DrawCapsule(RayGameContext ctx, Vector3 a, Vector3 b, float radius)
    {
        var delta = b - a;
        var len = delta.Length();
        if (len < 1e-4f)
        {
            ctx.DrawGlowSphere(a, radius, Wood);
            return;
        }

        var steps = Math.Clamp((int)(len / (radius * 0.9f)), 2, 3);
        for (var i = 0; i <= steps; i++)
        {
            var t = i / (float)steps;
            var p = Vector3.Lerp(a, b, t);
            var shade = i % 2 == 0 ? Wood : WoodHi;
            ctx.DrawGlowSphere(p, radius, shade);
        }
    }

    private static void DrawJoint(RayGameContext ctx, Vector3 center, float radius)
    {
        ctx.DrawGlowSphere(center, radius, JointKnob);
        ctx.DrawGlowSphereWires(center, radius, WoodWire);
    }

    private static void DrawHead(RayGameContext ctx, Vector3 center)
    {
        ctx.DrawGlowSphere(center, 0.22f, HeadTone);
        ctx.DrawGlowSphereWires(center, 0.22f, WoodWire);
    }

    private static void DrawHand(RayGameContext ctx, Vector3 center)
    {
        ctx.DrawGlowSphere(center, 0.08f, WoodHi);
    }

    private static void TryCapsule(
        Vector3 origin,
        Vector3 dir,
        Vector3 a,
        Vector3 b,
        float radius,
        int index,
        ref int bestIndex,
        ref float bestT)
    {
        if (!RayCapsule.TryHit(origin, dir, a, b, radius, out var t))
            return;
        if (t >= 0f && t < bestT)
        {
            bestT = t;
            bestIndex = index;
        }
    }
}

internal static class RayCapsule
{
    public static bool TryHit(Vector3 origin, Vector3 dir, Vector3 a, Vector3 b, float radius, out float t)
    {
        var ab = b - a;
        var abLenSq = ab.LengthSquared();
        if (abLenSq < 1e-8f)
            return RaySphere.TryHit(origin, dir, a, radius, out t);

        var ao = origin - a;
        var dab = Vector3.Dot(dir, ab);
        var daa = Vector3.Dot(dir, ao);
        var aab = Vector3.Dot(ab, ao);

        var aCoeff = Vector3.Dot(dir, dir) - dab * dab / abLenSq;
        var bCoeff = 2f * (daa - dab * aab / abLenSq);
        var cCoeff = Vector3.Dot(ao, ao) - aab * aab / abLenSq - radius * radius;

        if (MathF.Abs(aCoeff) < 1e-8f)
        {
            t = -1f;
            return false;
        }

        var disc = bCoeff * bCoeff - 4f * aCoeff * cCoeff;
        if (disc < 0f)
        {
            t = -1f;
            return false;
        }

        t = (-bCoeff - MathF.Sqrt(disc)) / (2f * aCoeff);
        return t >= 0f;
    }
}

internal static class RaySphere
{
    public static bool TryHit(Vector3 origin, Vector3 direction, Vector3 center, float radius, out float t)
    {
        var oc = origin - center;
        var a = Vector3.Dot(direction, direction);
        var b = 2f * Vector3.Dot(oc, direction);
        var c = Vector3.Dot(oc, oc) - radius * radius;
        var disc = b * b - 4f * a * c;
        if (disc < 0f || MathF.Abs(a) < 1e-8f)
        {
            t = -1f;
            return false;
        }

        t = (-b - MathF.Sqrt(disc)) / (2f * a);
        return t >= 0f;
    }
}
