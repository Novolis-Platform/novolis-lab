using Novolis.Raylib.Abstractions;
using Novolis.Raylib.Colors;
using Novolis.Raylib.Rendering;
using Novolis.Raylib.Shell;

RaylibRuntimeShell.RunShellFrame("Hello Runtime", 960, 540, new DemoFrame());

file sealed class DemoFrame : IRaylibFrameRenderer
{
    public void OnFrame(float dt, int w, int h)
    {
        Graphics.ClearBackground(RaylibColors.RayWhite);
        Graphics.DrawText($"Runtime shell ({w}x{h})", 16, 16, 24, RaylibColors.DarkGray);
    }
}
