namespace PulseStrip.Game;

using System.Drawing;
using System.Numerics;
using System.Runtime.InteropServices;

/// <summary>Direct raylib draw calls not yet exposed on <c>Novolis.Raylib</c> façades.</summary>
internal static class PulseStripNativeDraw
{
    private const string RaylibDll = "raylib";

    [StructLayout(LayoutKind.Sequential)]
    private struct RlColor
    {
        public byte R, G, B, A;

        public static RlColor From(Color c) => new()
        {
            R = c.R,
            G = c.G,
            B = c.B,
            A = c.A,
        };
    }

    [DllImport(RaylibDll, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern void DrawTriangle3D(Vector3 v1, Vector3 v2, Vector3 v3, RlColor color);

    public static void Triangle(Vector3 a, Vector3 b, Vector3 c, Color color)
    {
        var rl = RlColor.From(color);
        DrawTriangle3D(a, b, c, rl);
        DrawTriangle3D(a, c, b, rl);
    }

    public static void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color)
    {
        Triangle(a, b, c, color);
        Triangle(a, c, d, color);
    }
}
