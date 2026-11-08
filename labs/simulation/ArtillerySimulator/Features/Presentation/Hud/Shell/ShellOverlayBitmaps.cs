using SkiaSharp;

namespace ArtillerySimulator;

internal static class ShellOverlayBitmaps
{
    public static SKBitmap? TryLoad(string relative)
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

    public static SKRect FitContain(int srcW, int srcH, int dstW, int dstH)
    {
        var scale = Math.Min(dstW / (float)srcW, dstH / (float)srcH);
        var w = srcW * scale;
        var h = srcH * scale;
        var x = (dstW - w) * 0.5f;
        var y = (dstH - h) * 0.5f;
        return new SKRect(x, y, x + w, y + h);
    }
}
