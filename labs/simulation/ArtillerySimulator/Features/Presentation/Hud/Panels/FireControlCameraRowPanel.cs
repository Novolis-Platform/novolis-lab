using SkiaSharp;

namespace ArtillerySimulator;

internal static class FireControlCameraRowPanel
{
    public static void Draw(
        SKCanvas canvas,
        FireControlChromePrimitives chrome,
        in FireControlHudMetrics m,
        bool followProjectile)
    {
        var scale = m.Scale;
        var width = m.Width;
        var height = m.Height;
        var bw = 120f * scale;
        var bh = 36f * scale;
        var gap = 8f * scale;
        var x = width - 8 - 4 * bw - 3 * gap;
        var y = height - 120f * scale;

        void Cam(string label, bool on)
        {
            chrome.DrawButtonVisual(canvas, x, y, bw, bh, label, on);
            x += bw + gap;
        }

        Cam(followProjectile ? "SHELL" : "GUN", true);
        Cam("GUN VIEW", false);
        Cam("BATTLE", false);
        Cam("MAP", false);
    }
}
