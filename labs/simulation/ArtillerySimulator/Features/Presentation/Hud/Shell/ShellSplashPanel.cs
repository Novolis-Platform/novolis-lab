using System.Globalization;
using SkiaSharp;

namespace ArtillerySimulator;

internal static class ShellSplashPanel
{
    public static void Draw(
        SKCanvas canvas,
        int width,
        int height,
        SKBitmap? splash,
        SKBitmap? icon,
        IGameLevel? level,
        string status)
    {
        var hasArt = splash is not null;
        if (hasArt)
        {
            var dest = ShellOverlayBitmaps.FitContain(splash!.Width, splash.Height, width, height);
            canvas.DrawBitmap(splash, dest, SKSamplingOptions.Default);
            using var shade = new SKPaint { Color = ShellOverlayTheme.SplashShade };
            canvas.DrawRect(0, 0, width, height, shade);
        }

        if (icon is not null)
        {
            var s = Math.Min(width, height) * 0.12f;
            canvas.DrawBitmap(icon, new SKRect(28, 28, 28 + s, 28 + s), SKSamplingOptions.Default);
        }

        if (!hasArt)
        {
            ShellOverlayTypography.DrawCentered(canvas, width, ProductIdentity.Title, height * 0.30f, Math.Min(68f, width / 14f), ShellOverlayTheme.Title);
            ShellOverlayTypography.DrawCentered(canvas, width, ProductIdentity.Subtitle, height * 0.30f + 48f, Math.Min(24f, width / 42f), ShellOverlayTheme.Subtitle);
        }

        if (level is not null)
        {
            var index = GameLevelCatalog.PlayableIndex(level);
            var count = Math.Max(1, GameLevelCatalog.PlayableCount);
            var cardTop = hasArt ? height * 0.58f : height * 0.46f;
            DrawLevelCard(
                canvas,
                width,
                height,
                level,
                string.Create(CultureInfo.InvariantCulture, $"LEVEL {index + 1} / {count}"),
                cardTop);
        }

        ShellOverlayTypography.DrawCentered(canvas, width, status, height * 0.86f, 20f, ShellOverlayTheme.Accent);
        ShellOverlayTypography.DrawCentered(canvas, width, ProductLegal.SplashFooter, height - 32f, 13f, ShellOverlayTheme.Muted);
    }

    private static void DrawLevelCard(SKCanvas canvas, int width, int height, IGameLevel level, string indexLabel, float top)
    {
        var margin = Math.Max(48f, width * 0.18f);
        var rect = new SKRect(margin, top, width - margin, top + Math.Min(160f, height * 0.22f));
        using (var fill = new SKPaint { Color = ShellOverlayTheme.CardFill })
            canvas.DrawRoundRect(rect, 10, 10, fill);
        using (var stroke = new SKPaint
        {
            Color = ShellOverlayTheme.Accent,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2,
            IsAntialias = true,
        })
            canvas.DrawRoundRect(rect, 10, 10, stroke);

        var x = rect.Left + 24;
        var y = rect.Top + 36;
        ShellOverlayTypography.DrawText(canvas, indexLabel, x, y, 14f, ShellOverlayTheme.Accent);
        y += 28;
        ShellOverlayTypography.DrawText(canvas, level.DisplayName, x, y, 26f, ShellOverlayTheme.Title);
        y += 28;
        ShellOverlayTypography.DrawText(canvas, level.SiteName, x, y, 16f, ShellOverlayTheme.Subtitle);
        y += 24;
        ShellOverlayTypography.DrawText(canvas, ShellOverlayTypography.Truncate(level.Blurb, 72), x, y, 14f, ShellOverlayTheme.BodyDim);
    }
}
