using System.Drawing;
using Novolis.Raylib.Game;

namespace XFighter.Game;

/// <summary>Lower-viewport S-foil hints and canopy frame (X-wing cockpit feel).</summary>
internal static class CockpitNoseArt
{
    private static readonly Color FrameGray = Color.FromArgb(255, 55, 58, 68);
    private static readonly Color FrameHighlight = Color.FromArgb(255, 90, 95, 108);
    private static readonly Color WingOffWhite = Color.FromArgb(200, 180, 185, 195);

    public static void Draw(RayGameContext ctx, float roll)
    {
        var w = ctx.Width;
        var h = ctx.Height;
        var rollPx = (int)(roll * 90f);

        ctx.HudRect(0, h - 28, w, 28, Color.FromArgb(255, 12, 14, 20));
        ctx.HudLine(0, h - 28, w, h - 28, FrameHighlight);

        var wingY = h - 120 + Math.Abs(rollPx) / 3;
        var wingSpan = (int)(w * 0.22f);
        DrawWing(ctx, 24, wingY, wingSpan, 36, rollPx, 1);
        DrawWing(ctx, w - 24 - wingSpan, wingY, wingSpan, 36, rollPx, -1);

        var strutX = w / 2 + rollPx / 2;
        ctx.HudLine(strutX - 40, h - 95, strutX, h - 35, FrameGray);
        ctx.HudLine(strutX + 40, h - 95, strutX, h - 35, FrameGray);
        ctx.HudRect(strutX - 55, h - 100, 110, 14, FrameHighlight);
    }

    private static void DrawWing(RayGameContext ctx, int x, int y, int span, int depth, int rollOffset, int side)
    {
        var tilt = side * rollOffset / 4;
        ctx.HudRect(x, y + tilt, span, depth, WingOffWhite);
        ctx.HudLine(x, y + tilt, x + span, y + depth + tilt, FrameHighlight);
        ctx.HudLine(x + span, y + tilt, x, y + depth + tilt, FrameGray);
    }
}
