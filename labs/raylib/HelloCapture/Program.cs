using Novolis.Raylib.Abstractions;
using Novolis.Raylib.Capture;
using Novolis.Raylib.Colors;
using Novolis.Raylib.Rendering;
using Novolis.Raylib.Shell;
using Novolis.Raylib.Windowing;

var outputDirectory = Path.Combine("artifacts", "capture", DateTime.Now.ToString("yyyyMMdd-HHmmss"));
Directory.CreateDirectory(outputDirectory);

using var session = new FrameCaptureSession(new CaptureStreamOptions
{
    CaptureEveryNFrames = 2,
    MaxBufferedFrames = 64,
});

RaylibRuntimeShell.RunShellFrame("Hello Capture", 640, 480, new CaptureDemoFrame(), showFps: true);

var reader = session.Reader;
var written = 0;
if (reader is not null)
{
    while (reader.TryRead(out var frame))
    {
        var path = Path.Combine(outputDirectory, $"frame_{frame.FrameIndex:D4}.png");
        File.WriteAllBytes(path, frame.Png);
        written++;
    }
}

Console.WriteLine($"Wrote {written} frame(s) to {Path.GetFullPath(outputDirectory)}");

file sealed class CaptureDemoFrame : IRaylibFrameRenderer
{
    private int _frames;

    public void OnFrame(float deltaSeconds, int screenWidth, int screenHeight)
    {
        _ = deltaSeconds;
        Graphics.ClearBackground(RaylibColors.RayWhite);
        Graphics.DrawText($"Hello Capture — frame {_frames}", 16, 16, 20, RaylibColors.DarkGray);
        _frames++;
        if (_frames >= 90)
            Window.Close();
    }
}
