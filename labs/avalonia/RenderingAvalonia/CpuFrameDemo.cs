using Novolis.Avalonia.Rendering;
using Novolis.Math.Geometry;

namespace RenderingAvalonia;

internal sealed class CpuFrameDemo
{
    private readonly Rgba32FrameControl _view;
    private readonly Rgba32[] _buffer;
    private readonly int _width;
    private readonly int _height;
    private float _phase;

    public CpuFrameDemo(Rgba32FrameControl view, int width, int height)
    {
        _view = view;
        _width = width;
        _height = height;
        _buffer = new Rgba32[width * height];
    }

    public void Tick(float dt)
    {
        _phase += dt;
        FillGradient(_buffer, _width, _height, _phase);
        _view.PresentCpuFrame(_buffer, _width, _height);
    }

    private static void FillGradient(Span<Rgba32> pixels, int width, int height, float phase)
    {
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var u = x / (float)width;
                var v = y / (float)height;
                var r = (byte)(127 + 127 * MathF.Sin((u + phase) * 6f));
                var g = (byte)(127 + 127 * MathF.Sin((v + phase * 0.7f) * 5f));
                var b = (byte)(127 + 127 * MathF.Cos((u + v + phase) * 4f));
                pixels[y * width + x] = new Rgba32(r, g, b);
            }
        }
    }
}
