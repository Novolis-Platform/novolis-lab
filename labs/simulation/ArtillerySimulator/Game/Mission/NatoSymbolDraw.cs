using System.Drawing;
using System.Numerics;

namespace ArtillerySimulator.Game;

internal static class NatoSymbolDraw
{
    public static void HostileMechanized(SilkWireBatch wires, Vector3 at, float yawDegrees, float size, Color stroke, bool wreck)
    {
        var scale = wreck ? 0.55f : 1f;
        DrawDiamond(wires, at, yawDegrees, size * scale, stroke);
        DrawTracks(wires, at, yawDegrees, size * 0.35f * scale, stroke);
    }

    public static void FriendlyInfantry(SilkWireBatch wires, Vector3 at, float yawDegrees, float size, Color stroke)
    {
        DrawRectangle(wires, at, yawDegrees, size, stroke);
        var half = size * 0.32f;
        var a = Offset(at, yawDegrees, -half, -half);
        var b = Offset(at, yawDegrees, half, half);
        wires.Line(a, b, stroke);
    }

    public static void FriendlyArtillery(SilkWireBatch wires, Vector3 at, float yawDegrees, float size, Color stroke)
    {
        DrawRectangle(wires, at, yawDegrees, size, stroke);
        var center = at + Vector3.UnitY * (size * 0.05f);
        wires.Line(center, center + Vector3.UnitY * (size * 0.18f), stroke);
    }

    private static void DrawDiamond(SilkWireBatch wires, Vector3 at, float yaw, float size, Color stroke)
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
        wires.Line(top, right, stroke);
        wires.Line(right, bottom, stroke);
        wires.Line(bottom, left, stroke);
        wires.Line(left, top, stroke);
    }

    private static void DrawRectangle(SilkWireBatch wires, Vector3 at, float yaw, float size, Color stroke)
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
        wires.Line(nw, ne, stroke);
        wires.Line(ne, se, stroke);
        wires.Line(se, sw, stroke);
        wires.Line(sw, nw, stroke);
    }

    private static void DrawTracks(SilkWireBatch wires, Vector3 at, float yaw, float radius, Color stroke)
    {
        var left = Offset(at, yaw, -radius * 0.55f, 0);
        var right = Offset(at, yaw, radius * 0.55f, 0);
        wires.Line(left, left + Vector3.UnitY * (radius * 0.35f), stroke);
        wires.Line(right, right + Vector3.UnitY * (radius * 0.35f), stroke);
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
