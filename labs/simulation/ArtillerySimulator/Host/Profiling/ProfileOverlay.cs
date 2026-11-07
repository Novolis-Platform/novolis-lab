using Novolis.Math.Geometry;
using Novolis.Silk;
using SkiaSharp;

namespace ArtillerySimulator;

/// <summary>F1 profile panel — Skia text over the 3D frame.</summary>
internal sealed class ProfileOverlay : IDisposable
{
    private readonly SKTypeface _typeface;
    private SKBitmap? _bitmap;
    private SKCanvas? _canvas;
    private Rgba32[] _pixels = [];
    private int _width;
    private int _height;
    private string _lastText = "";

    public ProfileOverlay()
    {
        _typeface =
            SKTypeface.FromFamilyName("Cascadia Mono", SKFontStyle.Normal)
            ?? SKTypeface.FromFamilyName("Consolas", SKFontStyle.Normal)
            ?? SKTypeface.Default;
    }

    public void Draw(SilkFrame frame, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        EnsureSurface(frame.Width, frame.Height);
        if (text != _lastText || _pixels.Length == 0)
        {
            _lastText = text;
            Rasterize(text);
        }

        frame.BlitOverlay(_pixels, _width, _height);
    }

    public void Dispose()
    {
        _canvas?.Dispose();
        _bitmap?.Dispose();
        _typeface.Dispose();
        _canvas = null;
        _bitmap = null;
    }

    private void EnsureSurface(int width, int height)
    {
        if (_bitmap is { } existing && existing.Width == width && existing.Height == height)
            return;

        _canvas?.Dispose();
        _bitmap?.Dispose();
        _bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        _canvas = new SKCanvas(_bitmap);
        _pixels = new Rgba32[width * height];
        _width = width;
        _height = height;
        _lastText = "";
    }

    private void Rasterize(string text)
    {
        var canvas = _canvas!;
        canvas.Clear(SKColors.Transparent);

        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var fontSize = Math.Clamp(Math.Min(_width, _height) / 70f, 11f, 15f);
        using var font = new SKFont(_typeface, fontSize);
        var lineH = fontSize * 1.35f;
        var pad = 10f;
        var maxW = 0f;
        foreach (var line in lines)
            maxW = Math.Max(maxW, font.MeasureText(line));

        var panelW = Math.Min(_width - 16f, maxW + pad * 2f);
        var panelH = Math.Min(_height - 16f, lines.Length * lineH + pad * 2f);
        var x0 = 8f;
        var y0 = 8f;

        using (var fill = new SKPaint
        {
            Color = new SKColor(12, 16, 22, 210),
            Style = SKPaintStyle.Fill,
            IsAntialias = true,
        })
        {
            canvas.DrawRoundRect(x0, y0, panelW, panelH, 4, 4, fill);
        }

        using (var stroke = new SKPaint
        {
            Color = new SKColor(120, 200, 255, 220),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1.25f,
            IsAntialias = true,
        })
        {
            canvas.DrawRoundRect(x0, y0, panelW, panelH, 4, 4, stroke);
        }

        using var paint = new SKPaint
        {
            Color = new SKColor(220, 240, 255, 255),
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
        };
        var y = y0 + pad + fontSize;
        foreach (var line in lines)
        {
            canvas.DrawText(line, x0 + pad, y, SKTextAlign.Left, font, paint);
            y += lineH;
            if (y > y0 + panelH - 4)
                break;
        }

        using var pixmap = _bitmap!.PeekPixels();
        var bytes = pixmap.GetPixelSpan<byte>();
        var n = Math.Min(_pixels.Length, bytes.Length / 4);
        for (var i = 0; i < n; i++)
        {
            var o = i * 4;
            _pixels[i] = new Rgba32(bytes[o], bytes[o + 1], bytes[o + 2], bytes[o + 3]);
        }
    }
}
