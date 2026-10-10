using System.Drawing;

namespace BouncingBall.Game;

internal static class BallColors
{
    private static readonly Color[] Palette =
    [
        Color.FromArgb(255, 90, 200, 255),
        Color.FromArgb(255, 255, 120, 90),
        Color.FromArgb(255, 140, 255, 120),
        Color.FromArgb(255, 255, 220, 80),
        Color.FromArgb(255, 200, 140, 255),
        Color.FromArgb(255, 100, 220, 200),
    ];

    public static (Color Fill, Color Wire) ForIndex(int index)
    {
        var fill = Palette[index % Palette.Length];
        var wire = Color.FromArgb(
            255,
            Math.Min(255, fill.R + 80),
            Math.Min(255, fill.G + 80),
            Math.Min(255, fill.B + 80));
        return (fill, wire);
    }
}
