using SkiaSharp;

namespace ArtillerySimulator;

internal static class ShellLoadingPanel
{
    public static void Draw(
        SKCanvas canvas,
        int width,
        int height,
        SKBitmap? splash,
        SKBitmap? icon,
        IGameLevel? level,
        string status) =>
        ShellSplashPanel.Draw(canvas, width, height, splash, icon, level, status);
}
