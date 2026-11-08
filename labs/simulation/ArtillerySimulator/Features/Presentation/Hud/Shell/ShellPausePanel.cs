using SkiaSharp;

namespace ArtillerySimulator;

internal static class ShellPausePanel
{
    public static void Draw(
        SKCanvas canvas,
        int width,
        int height,
        string statusLine,
        ref float scrollLines,
        out float maxScrollLines)
    {
        ShellOverlayDimmer.Draw(canvas, width, height);
        var body = string.IsNullOrWhiteSpace(statusLine)
            ? ProductLegal.PauseHint + "\n\n" + ProductLegal.Tagline
            : statusLine + "\n\n" + ProductLegal.PauseHint + "\n\n" + ProductLegal.Tagline;
        var title = statusLine.Contains("FINAL", StringComparison.OrdinalIgnoreCase) ? "MISSION RESULT" : "PAUSED";
        ShellOverlayModalPanel.Draw(
            canvas,
            width,
            height,
            title,
            body,
            scrollable: false,
            ref scrollLines,
            out maxScrollLines);
    }
}
