using System.Drawing;
using System.Numerics;

namespace ArtillerySimulator;

internal static class NatoSymbolDraw
{
    public static void HostileMechanized(SilkWireBatch wires, Vector3 at, float yawDegrees, float size, Color stroke, bool wreck)
    {
        var scale = wreck ? 0.55f : 1f;
        var s = size * scale;
        // Tall mast so hostiles read against dense terrain wires from orbit distance.
        var mast = wreck ? s * 0.9f : s * 2.4f;
        wires.Line(at, at + Vector3.UnitY * mast, stroke);
        var head = at + Vector3.UnitY * (mast * 0.72f);
        DrawDiamond(wires, head, yawDegrees, s * 1.15f, stroke);
        DrawDiamond(wires, at + Vector3.UnitY * (s * 0.08f), yawDegrees, s * 0.85f, stroke);
        DrawTracks(wires, at + Vector3.UnitY * (s * 0.08f), yawDegrees, s * 0.42f, stroke);
        if (!wreck)
        {
            DrawRing(wires, at + Vector3.UnitY * 1.5f, s * 0.95f, stroke, segments: 16);
            DrawRing(wires, at + Vector3.UnitY * 1.5f, s * 1.35f, stroke, segments: 16);
        }
    }

    private static void DrawRing(SilkWireBatch wires, Vector3 center, float radius, Color color, int segments)
    {
        var previous = center + new Vector3(radius, 0f, 0f);
        for (var i = 1; i <= segments; i++)
        {
            var angle = i / (float)segments * MathF.Tau;
            var next = center + new Vector3(MathF.Cos(angle) * radius, 0f, MathF.Sin(angle) * radius);
            wires.Line(previous, next, color);
            previous = next;
        }
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
