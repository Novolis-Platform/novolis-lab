using System.Drawing;
using System.Numerics;
using Novolis.Raylib.Game;

namespace ArtillerySimulator.Game;

internal static class NatoSymbolDraw
{
    public static void HostileMechanized(RayGameContext ctx, Vector3 at, float yawDegrees, float size, Color stroke, bool wreck)
    {
        var scale = wreck ? 0.55f : 1f;
        DrawDiamond(ctx, at, yawDegrees, size * scale, stroke, fill: false);
        DrawTracks(ctx, at, yawDegrees, size * 0.35f * scale, stroke);
    }

    public static void FriendlyInfantry(RayGameContext ctx, Vector3 at, float yawDegrees, float size, Color stroke)
    {
        DrawRectangle(ctx, at, yawDegrees, size, stroke, fill: false);
        var half = size * 0.32f;
        var a = Offset(at, yawDegrees, -half, -half);
        var b = Offset(at, yawDegrees, half, half);
        ctx.DrawBolt(a, b, stroke);
    }

    public static void FriendlyArtillery(RayGameContext ctx, Vector3 at, float yawDegrees, float size, Color stroke)
    {
        DrawRectangle(ctx, at, yawDegrees, size, stroke, fill: false);
        var center = at + Vector3.UnitY * (size * 0.05f);
        ctx.DrawBolt(center, center + Vector3.UnitY * (size * 0.18f), stroke);
    }

    private static void DrawDiamond(RayGameContext ctx, Vector3 at, float yaw, float size, Color stroke, bool fill)
    {
        var yawRad = yaw * MathF.PI / 180f;
        var c = MathF.Cos(yawRad);
        var s = MathF.Sin(yawRad);
        var h = size * 0.36f;
        var w = size * 0.5f;
        var top = Transform(at, c, s, 0, -h);
        var right = Transform(at, c, s, w, 0);
        var bottom = Transform(at, c, s, 0, h);
        var left = Transform(at, c, s, -w, 0);
        ctx.DrawBolt(top, right, stroke);
        ctx.DrawBolt(right, bottom, stroke);
        ctx.DrawBolt(bottom, left, stroke);
        ctx.DrawBolt(left, top, stroke);
        if (fill)
            ctx.DrawBolt(at, at + Vector3.UnitY * 0.01f, stroke);
    }

    private static void DrawRectangle(RayGameContext ctx, Vector3 at, float yaw, float size, Color stroke, bool fill)
    {
        var yawRad = yaw * MathF.PI / 180f;
        var c = MathF.Cos(yawRad);
        var s = MathF.Sin(yawRad);
        var h = size * 0.36f;
        var w = size * 0.5f;
        var nw = Transform(at, c, s, -w, -h);
        var ne = Transform(at, c, s, w, -h);
        var se = Transform(at, c, s, w, h);
        var sw = Transform(at, c, s, -w, h);
        ctx.DrawBolt(nw, ne, stroke);
        ctx.DrawBolt(ne, se, stroke);
        ctx.DrawBolt(se, sw, stroke);
        ctx.DrawBolt(sw, nw, stroke);
        if (fill)
            ctx.DrawBolt(at, at + Vector3.UnitY * 0.01f, stroke);
    }

    private static void DrawTracks(RayGameContext ctx, Vector3 at, float yaw, float radius, Color stroke)
    {
        var left = Offset(at, yaw, -radius * 0.55f, 0);
        var right = Offset(at, yaw, radius * 0.55f, 0);
        ctx.DrawBolt(left, left + Vector3.UnitY * (radius * 0.35f), stroke);
        ctx.DrawBolt(right, right + Vector3.UnitY * (radius * 0.35f), stroke);
    }

    private static Vector3 Transform(Vector3 at, float c, float s, float lx, float lz)
    {
        var x = lx * c - lz * s;
        var z = lx * s + lz * c;
        return at + new Vector3(x, 0f, z);
    }

    private static Vector3 Offset(Vector3 at, float yawDegrees, float lx, float lz)
    {
        var yawRad = yawDegrees * MathF.PI / 180f;
        return Transform(at, MathF.Cos(yawRad), MathF.Sin(yawRad), lx, lz);
    }
}
