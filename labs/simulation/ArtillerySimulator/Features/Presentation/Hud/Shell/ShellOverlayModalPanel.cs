using System.Globalization;
using SkiaSharp;

namespace ArtillerySimulator;

internal static class ShellOverlayModalPanel
{
    public static void Draw(
        SKCanvas canvas,
        int width,
        int height,
        string title,
        string body,
        bool scrollable,
        ref float scrollLines,
        out float maxScrollLines)
    {
        var margin = Math.Max(28f, width * 0.06f);
        var rect = new SKRect(margin, margin, width - margin, height - margin);
        using (var fill = new SKPaint { Color = ShellOverlayTheme.PanelFill })
            canvas.DrawRoundRect(rect, 8, 8, fill);
        using (var stroke = new SKPaint
        {
            Color = ShellOverlayTheme.Accent,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2,
            IsAntialias = true,
        })
            canvas.DrawRoundRect(rect, 8, 8, stroke);

        var x = rect.Left + 24;
        var y = rect.Top + 40;
        var titleSize = Math.Min(26f, width / 48f);
        ShellOverlayTypography.DrawText(canvas, title, x, y, titleSize, ShellOverlayTheme.Title);
        y += titleSize + 14f;

        var bodySize = scrollable ? Math.Min(13f, width / 95f) : 14f;
        var lineHeight = bodySize + 4f;
        var maxWidth = rect.Width - 48f;
        var lines = ShellOverlayTextWrap.WrapLines(body, bodySize, maxWidth);
        var visible = Math.Max(1, (int)((rect.Bottom - 36f - y) / lineHeight));
        maxScrollLines = scrollable ? Math.Max(0, lines.Count - visible) : 0;
        scrollLines = Math.Clamp(scrollLines, 0f, maxScrollLines);
        var start = scrollable ? (int)scrollLines : 0;
        var end = scrollable ? Math.Min(lines.Count, start + visible) : lines.Count;

        for (var i = start; i < end; i++)
        {
            ShellOverlayTypography.DrawText(canvas, lines[i], x, y, bodySize, ShellOverlayTheme.Body);
            y += lineHeight;
            if (y > rect.Bottom - 28)
                break;
        }

        if (scrollable && maxScrollLines > 0.5f)
        {
            var hint = string.Create(
                CultureInfo.InvariantCulture,
                $"[{start + 1}-{end}/{lines.Count}]  {ProductLegal.LegalCloseHint}");
            ShellOverlayTypography.DrawText(canvas, hint, x, rect.Bottom - 14f, 12f, ShellOverlayTheme.Accent);
        }
    }
}
