using System.Drawing;
using System.Numerics;
using Novolis.Math.Geometry;

namespace ArtillerySimulator.Game;

/// <summary>Accumulates world-space line pairs for <see cref="Novolis.Silk.SilkFrame.DrawWorldLines"/>.</summary>
internal sealed class SilkWireBatch
{
    private readonly List<WorldLineVertex> _vertices = new(262_144);

    public void Clear() => _vertices.Clear();

    public ReadOnlySpan<WorldLineVertex> AsSpan() =>
        System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_vertices);

    public void Line(Vector3 a, Vector3 b, Color color) => Line(a, b, ToRgba(color));

    public void Line(Vector3 a, Vector3 b, Rgba32 color)
    {
        _vertices.Add(new WorldLineVertex(a.X, a.Y, a.Z, color));
        _vertices.Add(new WorldLineVertex(b.X, b.Y, b.Z, color));
    }

    public void Ring(Vector3 center, float radius, Color color, int segments = 28, float yLift = 3f)
    {
        var c = ToRgba(color);
        var previous = center + new Vector3(radius, yLift, 0f);
        for (var i = 1; i <= segments; i++)
        {
            var angle = i / (float)segments * MathF.Tau;
            var next = center + new Vector3(MathF.Cos(angle) * radius, yLift, MathF.Sin(angle) * radius);
            Line(previous, next, c);
            previous = next;
        }
    }

    public void AxisRing(Vector3 center, Vector3 axis, float radius, Color color, int segments = 12)
    {
        var c = ToRgba(color);
        var side = Lateral(axis);
        var up = Vector3.Normalize(Vector3.Cross(axis, side));
        var previous = center + side * radius;
        for (var i = 1; i <= segments; i++)
        {
            var angle = i / (float)segments * MathF.Tau;
            var next = center + side * (MathF.Cos(angle) * radius) + up * (MathF.Sin(angle) * radius);
            Line(previous, next, c);
            previous = next;
        }
    }

    public void WireSphere(Vector3 center, float radius, Color color, int segments = 16)
    {
        var c = ToRgba(color);
        Ring(center, radius, color, segments, yLift: 0f);
        var previous = center + new Vector3(0f, radius, 0f);
        for (var i = 1; i <= segments; i++)
        {
            var angle = i / (float)segments * MathF.Tau;
            var next = center + new Vector3(0f, MathF.Cos(angle) * radius, MathF.Sin(angle) * radius);
            Line(previous, next, c);
            previous = next;
        }

        previous = center + new Vector3(radius, 0f, 0f);
        for (var i = 1; i <= segments; i++)
        {
            var angle = i / (float)segments * MathF.Tau;
            var next = center + new Vector3(MathF.Cos(angle) * radius, MathF.Sin(angle) * radius, 0f);
            Line(previous, next, c);
            previous = next;
        }
    }

    public void WireBox(Vector3 center, Vector3 size, Color color)
    {
        var c = ToRgba(color);
        var hx = size.X * 0.5f;
        var hy = size.Y * 0.5f;
        var hz = size.Z * 0.5f;
        var p000 = center + new Vector3(-hx, -hy, -hz);
        var p001 = center + new Vector3(-hx, -hy, hz);
        var p010 = center + new Vector3(-hx, hy, -hz);
        var p011 = center + new Vector3(-hx, hy, hz);
        var p100 = center + new Vector3(hx, -hy, -hz);
        var p101 = center + new Vector3(hx, -hy, hz);
        var p110 = center + new Vector3(hx, hy, -hz);
        var p111 = center + new Vector3(hx, hy, hz);
        Line(p000, p001, c); Line(p001, p011, c); Line(p011, p010, c); Line(p010, p000, c);
        Line(p100, p101, c); Line(p101, p111, c); Line(p111, p110, c); Line(p110, p100, c);
        Line(p000, p100, c); Line(p001, p101, c); Line(p010, p110, c); Line(p011, p111, c);
    }

    public static Rgba32 ToRgba(Color color) => new(color.R, color.G, color.B, color.A);

    private static Vector3 Lateral(Vector3 dir)
    {
        var right = Vector3.Cross(dir, Vector3.UnitY);
        return right.LengthSquared() < 1e-6f ? Vector3.UnitZ : Vector3.Normalize(right);
    }
}
