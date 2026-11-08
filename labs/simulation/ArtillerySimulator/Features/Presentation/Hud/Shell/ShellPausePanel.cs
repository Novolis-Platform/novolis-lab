using SkiaSharp;

namespace ArtillerySimulator;

internal static class ShellPausePanel
{
    public static void Draw(SKCanvas canvas, int width, int height, ref float scrollLines, out float maxScrollLines)
    {
        ShellOverlayDimmer.Draw(canvas, width, height);
        ShellOverlayModalPanel.Draw(
            canvas,
            width,
            height,
            "PAUSED",
            ProductLegal.PauseHint + "\n\n" + ProductLegal.Tagline,
            scrollable: false,
            ref scrollLines,
            out maxScrollLines);
    }
}
