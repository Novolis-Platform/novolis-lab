using System.Drawing;
using Novolis.Avalonia.Raylib;
using Novolis.Raylib.Rendering;

namespace RenderingAvalonia;

internal sealed class RaylibViewportDemo
{
    private float _time;

    public RaylibViewportDemo(RaylibHostControl host)
    {
        host.FrameRendering += OnFrame;
    }

    private void OnFrame(object? sender, RaylibFrameEventArgs e)
    {
        _time += e.DeltaSeconds;
        Graphics.ClearBackground(Color.CornflowerBlue);
        Graphics.DrawRectangle(24, 24, 120, 80, Color.Goldenrod);
        Graphics.DrawText($"Raylib host {_time:F1}s", 24, 120, 22, Color.White);
    }
}
