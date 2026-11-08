using SkiaSharp;

namespace ArtillerySimulator;

internal static class FireControlFireButtonPanel
{
    public static void Draw(
        SKCanvas canvas,
        FireControlChromePrimitives chrome,
        in FireControlHudMetrics m,
        bool canFire)
    {
        var scale = m.Scale;
        var width = m.Width;
        var height = m.Height;
        var bw = 180f * scale;
        var bh = 48f * scale;
        var bx = (width - bw) * 0.5f;
        var by = height - 64f * scale;
        chrome.PanelBox(canvas, bx, by, bw, bh, hot: canFire);
        using (var stroke = new SKPaint
        {
            Color = canFire ? FireControlTheme.Alert : FireControlTheme.Dim,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2f * scale,
            IsAntialias = true,
        })
        {
            canvas.DrawRoundRect(bx, by, bw, bh, 4 * scale, 4 * scale, stroke);
        }

        chrome.DrawCenteredText(canvas, "FIRE", bx, by, bw, bh, 22f * scale, canFire ? FireControlTheme.Alert : FireControlTheme.Dim);
    }
}
