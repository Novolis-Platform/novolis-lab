using SkiaSharp;

namespace ArtillerySimulator;

internal static class ShellOverlayDimmer
{
    public static void Draw(SKCanvas canvas, int width, int height)
    {
        using var shade = new SKPaint { Color = ShellOverlayTheme.DimShade };
        canvas.DrawRect(0, 0, width, height, shade);
    }
}
