using System.Globalization;
using Novolis.Math.Geometry;
using Novolis.Silk;
using SkiaSharp;

namespace ArtillerySimulator;

/// <summary>Splash, loading, pause, and license overlays (Skia blit on top of the frame).</summary>
internal sealed class ShellOverlay : IDisposable
{
    private static readonly SKTypeface Typeface =
        SKTypeface.FromFamilyName("Cascadia Mono", SKFontStyle.Bold)
        ?? SKTypeface.FromFamilyName("Consolas", SKFontStyle.Bold)
        ?? SKTypeface.FromFamilyName("Courier New", SKFontStyle.Bold)
        ?? SKTypeface.Default;

    private SKBitmap? _splash;
    private SKBitmap? _icon;
    private SKBitmap? _surface;
    private SKCanvas? _canvas;
    private Rgba32[] _pixels = [];
    private int _width;
    private int _height;
    private string _lastSignature = "";
    private float _legalScrollLines;
    private float _legalMaxScrollLines;

    public float LegalScrollLines
    {
        get => _legalScrollLines;
        set
        {
            var next = Math.Clamp(value, 0f, Math.Max(0f, _legalMaxScrollLines));
            if (MathF.Abs(next - _legalScrollLines) < 0.01f)
                return;
            _legalScrollLines = next;
            _lastSignature = "";
        }
    }

    public void ResetLegalScroll()
    {
        _legalScrollLines = 0f;
        _lastSignature = "";
    }

    public void NudgeLegalScroll(float deltaLines) =>
        LegalScrollLines = _legalScrollLines + deltaLines;

    public void EnsureAssetsLoaded()
    {
        _splash ??= TryLoadBitmap("assets/k9-dog-splash.png") ?? TryLoadBitmap("assets/k9-dog-splash.jpg");
        _icon ??= TryLoadBitmap("assets/k9-dog-icon.png") ?? TryLoadBitmap("assets/k9-dog-icon.jpg");
    }

    public void Draw(SilkFrame frame, ShellPhase phase, string statusLine, IGameLevel? level = null)
    {
        EnsureAssetsLoaded();
        EnsureSurface(frame.Width, frame.Height);
        var levelKey = level is null ? "" : $"{level.Id}|{level.DisplayName}";
        var signature = string.Create(
            CultureInfo.InvariantCulture,
            $"{phase}|{statusLine}|{levelKey}|{_width}x{_height}|{GameLevelCatalog.PlayableCount}|{_legalScrollLines:F1}");
        if (signature != _lastSignature)
        {
            _lastSignature = signature;
            var canvas = _canvas!;
            canvas.Clear(new SKColor(12, 14, 16));
            switch (phase)
            {
                case ShellPhase.Splash:
                case ShellPhase.Loading:
                    DrawSplash(canvas, level, statusLine);
                    break;
                case ShellPhase.Paused:
                    DrawDim(canvas);
                    DrawPanel(canvas, "PAUSED", ProductLegal.PauseHint + "\n\n" + ProductLegal.Tagline, scrollable: false);
                    break;
                case ShellPhase.Legal:
                    DrawDim(canvas);
                    DrawPanel(canvas, "LICENSES & DISCLAIMERS", ProductLegal.BuildLicensePanel(level), scrollable: true);
                    break;
            }

            CopyPixels();
        }

        frame.ClearDepth();
        frame.BlitOverlay(_pixels, _width, _height);
        frame.SetTitle(TitleFor(phase, level));
    }

    public void Dispose()
    {
        _canvas?.Dispose();
        _surface?.Dispose();
        _splash?.Dispose();
        _icon?.Dispose();
    }

    private static string TitleFor(ShellPhase phase, IGameLevel? level)
    {
        var levelTitle = level is null
            ? ProductIdentity.WindowTitle
            : $"{ProductIdentity.Title} — {level.DisplayName}";

        return phase switch
        {
            ShellPhase.Playing => levelTitle,
            ShellPhase.Paused => $"{levelTitle} (paused)",
            ShellPhase.Legal => $"{ProductIdentity.Title} — licenses",
            ShellPhase.Loading => $"{ProductIdentity.Title} — loading",
            _ => ProductIdentity.WindowTitle,
        };
    }

    private void DrawSplash(SKCanvas canvas, IGameLevel? level, string status)
    {
        var hasArt = _splash is not null;
        if (hasArt)
        {
            var dest = FitContain(_splash!.Width, _splash.Height, _width, _height);
            canvas.DrawBitmap(_splash, dest, SKSamplingOptions.Default);
            using var shade = new SKPaint { Color = new SKColor(0, 0, 0, 70) };
            canvas.DrawRect(0, 0, _width, _height, shade);
        }

        if (_icon is not null)
        {
            var s = Math.Min(_width, _height) * 0.12f;
            canvas.DrawBitmap(_icon, new SKRect(28, 28, 28 + s, 28 + s), SKSamplingOptions.Default);
        }

        if (!hasArt)
        {
            DrawCentered(canvas, ProductIdentity.Title, _height * 0.30f, Math.Min(68f, _width / 14f), new SKColor(232, 214, 180));
            DrawCentered(canvas, ProductIdentity.Subtitle, _height * 0.30f + 48f, Math.Min(24f, _width / 42f), new SKColor(180, 196, 170));
        }

        if (level is not null)
        {
            var index = GameLevelCatalog.PlayableIndex(level);
            var count = Math.Max(1, GameLevelCatalog.PlayableCount);
            var cardTop = hasArt ? _height * 0.58f : _height * 0.46f;
            DrawLevelCard(
                canvas,
                level,
                string.Create(CultureInfo.InvariantCulture, $"LEVEL {index + 1} / {count}"),
                cardTop);
        }

        DrawCentered(canvas, status, _height * 0.86f, 20f, new SKColor(210, 132, 72));
        DrawCentered(canvas, ProductLegal.SplashFooter, _height - 32f, 13f, new SKColor(140, 148, 140));
    }

    private void DrawLevelCard(SKCanvas canvas, IGameLevel level, string indexLabel, float top)
    {
        var margin = Math.Max(48f, _width * 0.18f);
        var rect = new SKRect(margin, top, _width - margin, top + Math.Min(160f, _height * 0.22f));
        using (var fill = new SKPaint { Color = new SKColor(18, 24, 20, 210) })
            canvas.DrawRoundRect(rect, 10, 10, fill);
        using (var stroke = new SKPaint
        {
            Color = new SKColor(210, 132, 72),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2,
            IsAntialias = true,
        })
            canvas.DrawRoundRect(rect, 10, 10, stroke);

        var x = rect.Left + 24;
        var y = rect.Top + 36;
        DrawText(canvas, indexLabel, x, y, 14f, new SKColor(210, 132, 72));
        y += 28;
        DrawText(canvas, level.DisplayName, x, y, 26f, new SKColor(232, 214, 180));
        y += 28;
        DrawText(canvas, level.SiteName, x, y, 16f, new SKColor(180, 196, 170));
        y += 24;
        DrawText(canvas, Truncate(level.Blurb, 72), x, y, 14f, new SKColor(190, 198, 186));
    }

    private void DrawDim(SKCanvas canvas)
    {
        using var shade = new SKPaint { Color = new SKColor(0, 0, 0, 160) };
        canvas.DrawRect(0, 0, _width, _height, shade);
    }

    private void DrawPanel(SKCanvas canvas, string title, string body, bool scrollable)
    {
        var margin = Math.Max(28f, _width * 0.06f);
        var rect = new SKRect(margin, margin, _width - margin, _height - margin);
        using (var fill = new SKPaint { Color = new SKColor(22, 28, 24, 235) })
            canvas.DrawRoundRect(rect, 8, 8, fill);
        using (var stroke = new SKPaint
        {
            Color = new SKColor(210, 132, 72),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2,
            IsAntialias = true,
        })
            canvas.DrawRoundRect(rect, 8, 8, stroke);

        var x = rect.Left + 24;
        var y = rect.Top + 40;
        var titleSize = Math.Min(26f, _width / 48f);
        DrawText(canvas, title, x, y, titleSize, new SKColor(232, 214, 180));
        y += titleSize + 14f;

        var bodySize = scrollable ? Math.Min(13f, _width / 95f) : 14f;
        var lineHeight = bodySize + 4f;
        var maxWidth = rect.Width - 48f;
        var lines = WrapLines(body, bodySize, maxWidth);
        var visible = Math.Max(1, (int)((rect.Bottom - 36f - y) / lineHeight));
        _legalMaxScrollLines = scrollable ? Math.Max(0, lines.Count - visible) : 0;
        _legalScrollLines = Math.Clamp(_legalScrollLines, 0f, _legalMaxScrollLines);
        var start = scrollable ? (int)_legalScrollLines : 0;
        var end = scrollable ? Math.Min(lines.Count, start + visible) : lines.Count;

        for (var i = start; i < end; i++)
        {
            DrawText(canvas, lines[i], x, y, bodySize, new SKColor(200, 208, 196));
            y += lineHeight;
            if (y > rect.Bottom - 28)
                break;
        }

        if (scrollable && _legalMaxScrollLines > 0.5f)
        {
            var hint = string.Create(
                CultureInfo.InvariantCulture,
                $"[{start + 1}-{end}/{lines.Count}]  {ProductLegal.LegalCloseHint}");
            DrawText(canvas, hint, x, rect.Bottom - 14f, 12f, new SKColor(210, 132, 72));
        }
    }

    private static List<string> WrapLines(string body, float fontSize, float maxWidth)
    {
        var result = new List<string>(128);
        using var font = new SKFont(Typeface, fontSize);
        foreach (var raw in body.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            if (raw.Length == 0)
            {
                result.Add(string.Empty);
                continue;
            }

            if (font.MeasureText(raw) <= maxWidth)
            {
                result.Add(raw);
                continue;
            }

            var words = raw.Split(' ');
            var current = string.Empty;
            foreach (var word in words)
            {
                var candidate = current.Length == 0 ? word : current + " " + word;
                if (font.MeasureText(candidate) <= maxWidth)
                {
                    current = candidate;
                    continue;
                }

                if (current.Length > 0)
                    result.Add(current);

                if (font.MeasureText(word) <= maxWidth)
                {
                    current = word;
                    continue;
                }

                // Hard-break overlong tokens (paths / URLs).
                var slice = word;
                while (slice.Length > 0)
                {
                    var fit = 1;
                    while (fit < slice.Length && font.MeasureText(slice[..(fit + 1)]) <= maxWidth)
                        fit++;
                    result.Add(slice[..fit]);
                    slice = slice[fit..];
                }

                current = string.Empty;
            }

            if (current.Length > 0)
                result.Add(current);
        }

        return result;
    }

    private void EnsureSurface(int width, int height)
    {
        if (_surface is not null && _width == width && _height == height)
            return;
        _canvas?.Dispose();
        _surface?.Dispose();
        _width = Math.Max(1, width);
        _height = Math.Max(1, height);
        _surface = new SKBitmap(_width, _height, SKColorType.Rgba8888, SKAlphaType.Premul);
        _canvas = new SKCanvas(_surface);
        _pixels = new Rgba32[_width * _height];
        _lastSignature = "";
    }

    private void CopyPixels()
    {
        if (_surface is null)
            return;
        using var pixmap = _surface.PeekPixels();
        var bytes = pixmap.GetPixelSpan<byte>();
        for (var i = 0; i < _pixels.Length; i++)
        {
            var o = i * 4;
            _pixels[i] = new Rgba32(bytes[o], bytes[o + 1], bytes[o + 2], bytes[o + 3]);
        }
    }

    private static SKBitmap? TryLoadBitmap(string relative)
    {
        var path = Path.Combine(AppContext.BaseDirectory, relative);
        if (!File.Exists(path))
            return null;
        try
        {
            return SKBitmap.Decode(path);
        }
        catch
        {
            return null;
        }
    }

    private static SKRect FitContain(int srcW, int srcH, int dstW, int dstH)
    {
        var scale = Math.Min(dstW / (float)srcW, dstH / (float)srcH);
        var w = srcW * scale;
        var h = srcH * scale;
        var x = (dstW - w) * 0.5f;
        var y = (dstH - h) * 0.5f;
        return new SKRect(x, y, x + w, y + h);
    }

    private void DrawCentered(SKCanvas canvas, string text, float y, float size, SKColor color)
    {
        using var font = new SKFont(Typeface, size);
        using var paint = new SKPaint { Color = color, IsAntialias = true, Style = SKPaintStyle.Fill };
        var width = font.MeasureText(text);
        canvas.DrawText(text, (_width - width) * 0.5f, y, SKTextAlign.Left, font, paint);
    }

    private static void DrawText(SKCanvas canvas, string text, float x, float baselineY, float size, SKColor color)
    {
        using var font = new SKFont(Typeface, size);
        using var paint = new SKPaint { Color = color, IsAntialias = true, Style = SKPaintStyle.Fill };
        canvas.DrawText(text, x, baselineY, SKTextAlign.Left, font, paint);
    }

    private static string Truncate(string text, int max) =>
        text.Length <= max ? text : text[..Math.Max(0, max - 1)] + "…";
}
