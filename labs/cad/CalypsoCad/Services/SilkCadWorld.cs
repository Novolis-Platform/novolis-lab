using System.Drawing;
using System.Numerics;
using Novolis.Math.Geometry;
using Novolis.Silk;

namespace CalypsoCad.Services;

/// <summary>Raylib-shaped CAD draw facade over <see cref="WorldSolidBatch"/> / Silk.</summary>
internal sealed class SilkCadWorld
{
    private readonly WorldSolidBatch _batch = new();

    public void Begin() => _batch.Clear();

    public void Flush(SilkFrame frame, in Matrix4x4 viewProjection) =>
        frame.SubmitWorld(_batch, viewProjection);

    public void DrawLine(Vector3 a, Vector3 b, Color color) =>
        _batch.Line(a, b, ToRgba(color));

    public void DrawCube(Vector3 center, float width, float height, float length, Color color) =>
        _batch.Cube(center, width, height, length, ToRgba(color));

    public void DrawCubeV(Vector3 center, Vector3 size, Color color) =>
        _batch.Cube(center, size.X, size.Y, size.Z, ToRgba(color));

    public void DrawSphere(Vector3 center, float radius, Color color) =>
        _batch.Sphere(center, radius, ToRgba(color));

    public void DrawTriangle(Vector3 a, Vector3 b, Vector3 c, Color color) =>
        _batch.Triangle(a, b, c, ToRgba(color));

    public void DrawCubeWiresV(Vector3 center, Vector3 size, Color color)
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
        _batch.Line(p000, p001, c); _batch.Line(p001, p011, c); _batch.Line(p011, p010, c); _batch.Line(p010, p000, c);
        _batch.Line(p100, p101, c); _batch.Line(p101, p111, c); _batch.Line(p111, p110, c); _batch.Line(p110, p100, c);
        _batch.Line(p000, p100, c); _batch.Line(p001, p101, c); _batch.Line(p010, p110, c); _batch.Line(p011, p111, c);
    }

    /// <summary>Vertical cylinder approximated as a prism (Raylib DrawCylinder compat).</summary>
    public void DrawCylinder(Vector3 startPos, float radiusTop, float radiusBottom, float height, int slices, Color color)
    {
        slices = System.Math.Clamp(slices, 6, 32);
        var rgba = ToRgba(color);
        var y0 = startPos.Y;
        var y1 = startPos.Y + height;
        for (var i = 0; i < slices; i++)
        {
            var a0 = i / (float)slices * MathF.Tau;
            var a1 = (i + 1) / (float)slices * MathF.Tau;
            var b0 = startPos + new Vector3(MathF.Cos(a0) * radiusBottom, 0f, MathF.Sin(a0) * radiusBottom);
            var b1 = startPos + new Vector3(MathF.Cos(a1) * radiusBottom, 0f, MathF.Sin(a1) * radiusBottom);
            var t0 = new Vector3(startPos.X + MathF.Cos(a0) * radiusTop, y1, startPos.Z + MathF.Sin(a0) * radiusTop);
            var t1 = new Vector3(startPos.X + MathF.Cos(a1) * radiusTop, y1, startPos.Z + MathF.Sin(a1) * radiusTop);
            b0 = new Vector3(b0.X, y0, b0.Z);
            b1 = new Vector3(b1.X, y0, b1.Z);
            _batch.Triangle(b0, b1, t1, rgba);
            _batch.Triangle(b0, t1, t0, rgba);
        }
    }

    public static Rgba32 ToRgba(Color c) => new((byte)c.R, (byte)c.G, (byte)c.B, (byte)c.A);

    public static Rgba32 ToRgba(Color c, byte alpha) => new((byte)c.R, (byte)c.G, (byte)c.B, alpha);
}
