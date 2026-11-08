using SkiaSharp;

namespace ArtillerySimulator;

internal static class ShellLegalPanel
{
    public static void Draw(
        SKCanvas canvas,
        int width,
        int height,
        IGameLevel? level,
        ref float scrollLines,
        out float maxScrollLines)
    {
        ShellOverlayDimmer.Draw(canvas, width, height);
        ShellOverlayModalPanel.Draw(
            canvas,
            width,
            height,
            "LICENSES & DISCLAIMERS",
            ProductLegal.BuildLicensePanel(level),
            scrollable: true,
            ref scrollLines,
            out maxScrollLines);
    }
}
