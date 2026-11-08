using Novolis.Silk;

namespace ArtillerySimulator;

internal sealed class ShellOverlaySurface : IDisposable
{
    private readonly SkiaOverlaySurface _inner = new();

    public int Width { get; private set; }
    public int Height { get; private set; }

    public SkiaOverlaySurface Inner => _inner;

    public bool Ensure(int width, int height)
    {
        var w = Math.Max(1, width);
        var h = Math.Max(1, height);
        if (Width == w && Height == h && _inner.Canvas is not null)
            return false;

        Width = w;
        Height = h;
        _inner.Ensure(Width, Height);
        return true;
    }

    public void Blit(SilkFrame frame)
    {
        _inner.CopyPixels();
        _inner.Upload(frame, Width, Height);
    }

    public void Dispose() => _inner.Dispose();
}
