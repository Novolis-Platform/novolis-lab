using SkiaSharp;

namespace ArtillerySimulator;

internal static class ShellOverlayTypography
{
    public static void DrawCentered(SKCanvas canvas, int frameWidth, string text, float y, float size, SKColor color)
    {
        using var font = new SKFont(ShellOverlayTheme.Typeface, size);
        using var paint = new SKPaint { Color = color, IsAntialias = true, Style = SKPaintStyle.Fill };
        var width = font.MeasureText(text);
        canvas.DrawText(text, (frameWidth - width) * 0.5f, y, SKTextAlign.Left, font, paint);
    }

    public static void DrawText(SKCanvas canvas, string text, float x, float baselineY, float size, SKColor color)
    {
        using var font = new SKFont(ShellOverlayTheme.Typeface, size);
        using var paint = new SKPaint { Color = color, IsAntialias = true, Style = SKPaintStyle.Fill };
        canvas.DrawText(text, x, baselineY, SKTextAlign.Left, font, paint);
    }

    public static string Truncate(string text, int max) =>
        text.Length <= max ? text : text[..Math.Max(0, max - 1)] + "…";
}
