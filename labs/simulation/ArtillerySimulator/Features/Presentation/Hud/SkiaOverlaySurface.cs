using System.Runtime.InteropServices;
using Novolis.Silk;
using SkiaSharp;

namespace ArtillerySimulator;

internal sealed class SkiaOverlaySurface : IDisposable
{
    private SKBitmap? _bitmap;
    private SKCanvas? _canvas;
    private Rgba32[] _pixels = [];

    public bool OverlayUploaded { get; set; }

    public SKCanvas Canvas => _canvas!;

    public ReadOnlySpan<Rgba32> Pixels => _pixels;

    public void Ensure(int width, int height)
    {
        if (_bitmap is { } existing && existing.Width == width && existing.Height == height)
            return;

        _canvas?.Dispose();
        _bitmap?.Dispose();
        _bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        _canvas = new SKCanvas(_bitmap);
        _pixels = new Rgba32[width * height];
        OverlayUploaded = false;
    }

    public void CopyPixels()
    {
        if (_bitmap is null)
            return;

        using var pixmap = _bitmap.PeekPixels();
        var bytes = pixmap.GetPixelSpan<byte>();
        var dst = MemoryMarshal.AsBytes(_pixels.AsSpan());
        var n = Math.Min(dst.Length, bytes.Length);
        bytes[..n].CopyTo(dst[..n]);
    }

    public void Upload(SilkFrame frame, int width, int height)
    {
        frame.ClearDepth();
        frame.BlitOverlay(_pixels, width, height);
        OverlayUploaded = true;
    }

    public void Dispose()
    {
        _canvas?.Dispose();
        _bitmap?.Dispose();
        _canvas = null;
        _bitmap = null;
    }
}
