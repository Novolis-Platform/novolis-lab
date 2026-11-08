using SkiaSharp;

namespace ArtillerySimulator;

internal sealed class FireControlChromePrimitives(SKTypeface typeface, float scale)
{
    public float Scale { get; } = scale;

    public void PanelBox(SKCanvas canvas, float x, float y, float w, float h, bool hot = false)
    {
        using var fill = new SKPaint
        {
            Color = hot ? FireControlTheme.PanelHotFill : FireControlTheme.PanelFill,
            Style = SKPaintStyle.Fill,
            IsAntialias = true,
        };
        canvas.DrawRoundRect(x, y, w, h, 6 * Scale, 6 * Scale, fill);

        using var stroke = new SKPaint
        {
            Color = FireControlTheme.Border,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1.5f * Scale,
            IsAntialias = true,
        };
        canvas.DrawRoundRect(x, y, w, h, 6 * Scale, 6 * Scale, stroke);
    }

    public void DrawButtonVisual(SKCanvas canvas, float x, float y, float w, float h, string label, bool selected)
    {
        PanelBox(canvas, x, y, w, h, hot: selected);
        if (selected)
        {
            using var stroke = new SKPaint
            {
                Color = FireControlTheme.Selected,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 2f * Scale,
                IsAntialias = true,
            };
            canvas.DrawRoundRect(x, y, w, h, 6 * Scale, 6 * Scale, stroke);
        }

        DrawCenteredText(canvas, label, x, y, w, h, 14f * Scale, selected ? FireControlTheme.Selected : FireControlTheme.Text);
    }

    public void DrawText(SKCanvas canvas, string text, float x, float baselineY, float size, SKColor color)
    {
        using var font = new SKFont(typeface, size);
        using var paint = new SKPaint
        {
            Color = color,
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
        };
        canvas.DrawText(text, x, baselineY, SKTextAlign.Left, font, paint);
    }

    public void DrawCenteredText(SKCanvas canvas, string text, float x, float y, float w, float h, float size, SKColor color)
    {
        using var font = new SKFont(typeface, size);
        using var paint = new SKPaint
        {
            Color = color,
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
        };
        var metrics = font.Metrics;
        var textWidth = font.MeasureText(text);
        var baseline = y + (h + metrics.CapHeight) * 0.5f;
        canvas.DrawText(text, x + (w - textWidth) * 0.5f, baseline, SKTextAlign.Left, font, paint);
    }

    public float MeasureText(string text, float size)
    {
        using var font = new SKFont(typeface, size);
        return font.MeasureText(text);
    }

    public static string Truncate(string text, int max)
        => text.Length <= max ? text : text[..Math.Max(0, max - 3)] + "...";

    public static SKColor ToSk(System.Drawing.Color c) => new(c.R, c.G, c.B, c.A);
}
