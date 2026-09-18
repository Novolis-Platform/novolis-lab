using System.Drawing;
using System.Numerics;
using Novolis.Raylib.Game;

namespace XFighter.Game;

internal static class TargetingComputer
{
    private static readonly Color LockGreen = Color.FromArgb(220, 60, 255, 100);
    private static readonly Color LockDim = Color.FromArgb(120, 40, 180, 70);

    public static HFighter? FindLockTarget(ReadOnlySpan<HFighter> enemies, Vector3 playerPos, Vector3 forward)
    {
        HFighter? best = null;
        var bestScore = float.MaxValue;
        foreach (var enemy in enemies)
        {
            if (!enemy.Active)
                continue;

            var to = enemy.Position - playerPos;
            var dist = to.Length();
            if (dist > 90f || dist < 4f)
                continue;

            var dir = Vector3.Normalize(to);
            var dot = Vector3.Dot(forward, dir);
            if (dot < 0.55f)
                continue;

            var score = dist - dot * 20f;
            if (score >= bestScore)
                continue;

            bestScore = score;
            best = enemy;
        }

        return best;
    }

    public static void DrawLockBrackets(RayGameContext ctx, HFighter target)
    {
        var c = target.Position;
        var s = 3.2f + MathF.Sin(target.WeavePhase * 2f) * 0.15f;
        var corner = s * 0.55f;
        var color = LockGreen;

        DrawBracket(ctx, c + new Vector3(-s, s, 0), corner, 1, 1, color);
        DrawBracket(ctx, c + new Vector3(s, s, 0), corner, -1, 1, color);
        DrawBracket(ctx, c + new Vector3(-s, -s, 0), corner, 1, -1, color);
        DrawBracket(ctx, c + new Vector3(s, -s, 0), corner, -1, -1, color);

        ctx.DrawShipWires(c, new Vector3(s * 2.1f, s * 2.1f, 0.05f), LockDim);
        ctx.DrawBolt(c, c + new Vector3(0, -s * 1.4f, 0), LockDim);
    }

    private static void DrawBracket(
        RayGameContext ctx,
        Vector3 origin,
        float len,
        int sx,
        int sy,
        Color color)
    {
        var a = origin;
        var b = origin + new Vector3(sx * len, 0, 0);
        var c = origin + new Vector3(0, sy * len, 0);
        ctx.DrawBolt(a, b, color);
        ctx.DrawBolt(a, c, color);
    }
}
