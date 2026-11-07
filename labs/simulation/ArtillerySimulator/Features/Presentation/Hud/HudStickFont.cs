using System.Drawing;
using System.Numerics;
using Novolis.Math.Geometry;

namespace ArtillerySimulator;

/// <summary>Minimal 5×7 stroke font for Silk wire HUDs (no glyph atlas in Silk yet).</summary>
internal static class HudStickFont
{
    // Each glyph: pairs of cell coords in a 5×7 grid (0..4, 0..6), y down.
    private static readonly Dictionary<char, byte[]> Glyphs = BuildGlyphs();

    public static float MeasureWidth(string text, float scale) =>
        text.Length * (6f * scale);

    /// <summary>HUD line depth — nearer than solid panels so text is not depth-culled.</summary>
    private const float HudZ = -0.4f;

    public static void Draw(
        SilkWireBatch wires,
        string text,
        float x,
        float y,
        float scale,
        Color color)
    {
        var cursor = x;
        foreach (var ch in text.ToUpperInvariant())
        {
            if (Glyphs.TryGetValue(ch, out var strokes))
            {
                for (var i = 0; i + 3 < strokes.Length; i += 4)
                {
                    var ax = cursor + strokes[i] * scale;
                    var ay = y + strokes[i + 1] * scale;
                    var bx = cursor + strokes[i + 2] * scale;
                    var by = y + strokes[i + 3] * scale;
                    wires.Line(new Vector3(ax, ay, HudZ), new Vector3(bx, by, HudZ), color);
                }
            }

            cursor += 6f * scale;
        }
    }

    public static void Rect(SilkWireBatch wires, float x, float y, float w, float h, Color color)
    {
        var a = new Vector3(x, y, HudZ);
        var b = new Vector3(x + w, y, HudZ);
        var c = new Vector3(x + w, y + h, HudZ);
        var d = new Vector3(x, y + h, HudZ);
        wires.Line(a, b, color);
        wires.Line(b, c, color);
        wires.Line(c, d, color);
        wires.Line(d, a, color);
    }

    public static void FillHatch(SilkWireBatch wires, float x, float y, float w, float h, Color color, float step = 8f)
    {
        for (var yy = y; yy <= y + h; yy += step)
            wires.Line(new Vector3(x, yy, HudZ), new Vector3(x + w, yy, HudZ), color);
    }

    /// <summary>Dense scanlines that read as a dark translucent panel over wire terrain.</summary>
    public static void FillPanel(SilkWireBatch wires, float x, float y, float w, float h, Color color)
    {
        for (var yy = y; yy <= y + h; yy += 1f)
            wires.Line(new Vector3(x, yy, HudZ), new Vector3(x + w, yy, HudZ), color);
    }

    private static Dictionary<char, byte[]> BuildGlyphs()
    {
        var g = new Dictionary<char, byte[]>();
        void Add(char c, params byte[] s) => g[c] = s;

        Add('0', 1,0, 3,0, 3,0, 4,1, 4,1, 4,5, 4,5, 3,6, 3,6, 1,6, 1,6, 0,5, 0,5, 0,1, 0,1, 1,0);
        Add('1', 1,1, 2,0, 2,0, 2,6, 1,6, 3,6);
        Add('2', 0,1, 1,0, 1,0, 3,0, 3,0, 4,1, 4,1, 4,2, 4,2, 0,6, 0,6, 4,6);
        Add('3', 0,0, 4,0, 4,0, 2,3, 2,3, 4,4, 4,4, 4,5, 4,5, 3,6, 3,6, 0,6);
        Add('4', 3,0, 0,4, 0,4, 4,4, 3,0, 3,6);
        Add('5', 4,0, 0,0, 0,0, 0,3, 0,3, 3,3, 3,3, 4,4, 4,4, 4,5, 4,5, 3,6, 3,6, 0,6);
        Add('6', 3,0, 0,3, 0,3, 0,5, 0,5, 1,6, 1,6, 3,6, 3,6, 4,5, 4,5, 4,4, 4,4, 3,3, 3,3, 0,3);
        Add('7', 0,0, 4,0, 4,0, 1,6);
        Add('8', 1,0, 3,0, 3,0, 4,1, 4,1, 4,2, 4,2, 3,3, 3,3, 1,3, 1,3, 0,2, 0,2, 0,1, 0,1, 1,0, 1,3, 3,3, 3,3, 4,4, 4,4, 4,5, 4,5, 3,6, 3,6, 1,6, 1,6, 0,5, 0,5, 0,4, 0,4, 1,3);
        Add('9', 1,6, 4,3, 4,3, 4,1, 4,1, 3,0, 3,0, 1,0, 1,0, 0,1, 0,1, 0,2, 0,2, 1,3, 1,3, 4,3);
        Add('A', 0,6, 2,0, 2,0, 4,6, 1,4, 3,4);
        Add('B', 0,0, 0,6, 0,0, 3,0, 3,0, 4,1, 4,1, 4,2, 4,2, 3,3, 3,3, 0,3, 3,3, 4,4, 4,4, 4,5, 4,5, 3,6, 3,6, 0,6);
        Add('C', 4,1, 3,0, 3,0, 1,0, 1,0, 0,1, 0,1, 0,5, 0,5, 1,6, 1,6, 3,6, 3,6, 4,5);
        Add('D', 0,0, 0,6, 0,0, 2,0, 2,0, 4,2, 4,2, 4,4, 4,4, 2,6, 2,6, 0,6);
        Add('E', 4,0, 0,0, 0,0, 0,6, 0,6, 4,6, 0,3, 3,3);
        Add('F', 4,0, 0,0, 0,0, 0,6, 0,3, 3,3);
        Add('G', 4,1, 3,0, 3,0, 1,0, 1,0, 0,1, 0,1, 0,5, 0,5, 1,6, 1,6, 3,6, 3,6, 4,5, 4,5, 4,3, 4,3, 2,3);
        Add('H', 0,0, 0,6, 4,0, 4,6, 0,3, 4,3);
        Add('I', 1,0, 3,0, 2,0, 2,6, 1,6, 3,6);
        Add('J', 2,0, 4,0, 4,0, 4,5, 4,5, 3,6, 3,6, 1,6, 1,6, 0,5);
        Add('K', 0,0, 0,6, 4,0, 0,3, 0,3, 4,6);
        Add('L', 0,0, 0,6, 0,6, 4,6);
        Add('M', 0,6, 0,0, 0,0, 2,3, 2,3, 4,0, 4,0, 4,6);
        Add('N', 0,6, 0,0, 0,0, 4,6, 4,6, 4,0);
        Add('O', 1,0, 3,0, 3,0, 4,1, 4,1, 4,5, 4,5, 3,6, 3,6, 1,6, 1,6, 0,5, 0,5, 0,1, 0,1, 1,0);
        Add('P', 0,6, 0,0, 0,0, 3,0, 3,0, 4,1, 4,1, 4,2, 4,2, 3,3, 3,3, 0,3);
        Add('Q', 1,0, 3,0, 3,0, 4,1, 4,1, 4,4, 4,4, 2,6, 2,6, 1,6, 1,6, 0,5, 0,5, 0,1, 0,1, 1,0, 2,4, 4,6);
        Add('R', 0,6, 0,0, 0,0, 3,0, 3,0, 4,1, 4,1, 4,2, 4,2, 3,3, 3,3, 0,3, 2,3, 4,6);
        Add('S', 4,1, 3,0, 3,0, 1,0, 1,0, 0,1, 0,1, 0,2, 0,2, 1,3, 1,3, 3,3, 3,3, 4,4, 4,4, 4,5, 4,5, 3,6, 3,6, 1,6, 1,6, 0,5);
        Add('T', 0,0, 4,0, 2,0, 2,6);
        Add('U', 0,0, 0,5, 0,5, 1,6, 1,6, 3,6, 3,6, 4,5, 4,5, 4,0);
        Add('V', 0,0, 2,6, 2,6, 4,0);
        Add('W', 0,0, 0,6, 0,6, 2,3, 2,3, 4,6, 4,6, 4,0);
        Add('X', 0,0, 4,6, 4,0, 0,6);
        Add('Y', 0,0, 2,3, 4,0, 2,3, 2,3, 2,6);
        Add('Z', 0,0, 4,0, 4,0, 0,6, 0,6, 4,6);
        Add(' ', Array.Empty<byte>());
        Add('-', 1,3, 3,3);
        Add('.', 2,5, 2,6);
        Add(',', 2,5, 1,7);
        Add(':', 2,1, 2,2, 2,4, 2,5);
        Add('/', 4,0, 0,6);
        Add('\\', 0,0, 4,6);
        Add('|', 2,0, 2,6);
        Add('+', 2,1, 2,5, 0,3, 4,3);
        Add('=', 1,2, 3,2, 1,4, 3,4);
        Add('>', 1,1, 3,3, 3,3, 1,5);
        Add('<', 3,1, 1,3, 1,3, 3,5);
        Add('(', 3,0, 1,2, 1,2, 1,4, 1,4, 3,6);
        Add(')', 1,0, 3,2, 3,2, 3,4, 3,4, 1,6);
        Add('%', 0,0, 1,0, 1,0, 1,1, 1,1, 0,1, 0,1, 0,0, 4,5, 3,5, 3,5, 3,6, 3,6, 4,6, 4,6, 4,5, 4,0, 0,6);
        Add('\'', 2,0, 2,2);
        Add('°', 1,0, 2,0, 2,0, 3,1, 3,1, 2,2, 2,2, 1,2, 1,2, 1,0);

        return g;
    }
}
