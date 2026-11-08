using SkiaSharp;

namespace ArtillerySimulator;

internal static class FireControlHelpPanel
{
    public static void Draw(SKCanvas canvas, FireControlChromePrimitives chrome, in FireControlHudMetrics m)
    {
        var scale = m.Scale;
        var width = m.Width;
        var height = m.Height;
        var help = "WASD LAY  SPACE FIRE  TAB GUN  ESC/P PAUSE  L LICENSE  F SHELL CAM  F1 PROFILE";
        var size = 13f * scale;
        var textWidth = chrome.MeasureText(help, size);
        chrome.DrawText(canvas, help, Math.Max(12f, (width - textWidth) * 0.5f), height - 12f * scale, size, FireControlTheme.Dim);
    }
}
