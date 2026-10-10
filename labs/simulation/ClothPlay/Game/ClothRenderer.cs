using System.Drawing;
using System.Numerics;
using Novolis.Physics.Collision.Simple;
using Novolis.Physics.Joints;
using Novolis.Raylib.Game;

namespace ClothPlay.Game;

internal static class ClothRenderer
{
    private static readonly Color ClothEdge = Color.FromArgb(255, 210, 230, 235);
    private static readonly Color SoftEdge = Color.FromArgb(110, 120, 160, 170);
    private static readonly Color PinKnob = Color.FromArgb(255, 220, 160, 70);
    private static readonly Color Particle = Color.FromArgb(255, 180, 210, 215);

    public static void Draw(RayGameContext ctx, ClothSheet cloth)
    {
        var spheres = cloth.Spheres;
        if (spheres.Count == 0)
            return;

        DrawLiveJoints(ctx, cloth);
        DrawParticles(ctx, cloth);
        DrawPins(ctx, cloth);
    }

    public static bool TryPickParticle(
        Vector3 rayOrigin,
        Vector3 rayDir,
        IReadOnlyList<SphereState> spheres,
        float pickRadius,
        out int index,
        out float hitDistance)
    {
        index = -1;
        hitDistance = float.MaxValue;
        var dirLenSq = rayDir.LengthSquared();
        if (dirLenSq < 1e-12f)
            return false;

        var dir = rayDir / MathF.Sqrt(dirLenSq);
        for (var i = 0; i < spheres.Count; i++)
        {
            var to = spheres[i].Position - rayOrigin;
            var t = Vector3.Dot(to, dir);
            if (t < 0f)
                continue;

            var closest = rayOrigin + dir * t;
            var dist = Vector3.Distance(closest, spheres[i].Position);
            if (dist > pickRadius || t >= hitDistance)
                continue;

            hitDistance = t;
            index = i;
        }

        return index >= 0;
    }

    private static void DrawLiveJoints(RayGameContext ctx, ClothSheet cloth)
    {
        var spheres = cloth.Spheres;
        foreach (var joint in cloth.Joints)
        {
            if ((uint)joint.SphereA >= (uint)spheres.Count || (uint)joint.SphereB >= (uint)spheres.Count)
                continue;

            var color = joint.Stiffness >= 0.85f ? ClothEdge : SoftEdge;
            ctx.DrawBolt(spheres[joint.SphereA].Position, spheres[joint.SphereB].Position, color);
        }
    }

    private static void DrawParticles(RayGameContext ctx, ClothSheet cloth)
    {
        foreach (var sphere in cloth.Spheres)
            ctx.DrawGlowSphere(sphere.Position, ClothSheet.ParticleRadius * 0.85f, Particle);
    }

    private static void DrawPins(RayGameContext ctx, ClothSheet cloth)
    {
        foreach (var pin in cloth.Pins)
        {
            if ((uint)pin >= (uint)cloth.Spheres.Count)
                continue;
            ctx.DrawGlowSphere(cloth.Spheres[pin].Position, ClothSheet.ParticleRadius * 1.35f, PinKnob);
        }
    }
}
