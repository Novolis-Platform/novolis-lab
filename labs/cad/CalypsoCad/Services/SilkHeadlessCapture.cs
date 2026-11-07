using Novolis.Silk;
using Novolis.Silk.Capture;

namespace CalypsoCad.Services;

/// <summary>Hidden Silk/GLFW loop → PNG via framebuffer readback.</summary>
internal static class SilkHeadlessCapture
{
    /// <summary>One hidden window; each step prepares pose then draws; writes PNG paths.</summary>
    public static IReadOnlyList<string> CaptureSequence(
        int width,
        int height,
        IReadOnlyList<(string Path, Action Prepare)> steps,
        Action<SilkFrame> draw,
        int warmFramesPerStep = 3)
    {
        ArgumentNullException.ThrowIfNull(steps);
        ArgumentNullException.ThrowIfNull(draw);
        if (steps.Count == 0)
            return Array.Empty<string>();

        var saved = new List<string>(steps.Count);
        using var capture = new FrameCaptureSession(new CaptureStreamOptions
        {
            CaptureEveryNFrames = 1,
            MaxBufferedFrames = 8,
        });

        var index = 0;
        var warmLeft = warmFramesPerStep;
        var retries = 0;
        const int maxRetries = 24;

        SilkGame.Run(
            "CalypsoCad capture",
            width,
            height,
            initialize: null,
            update: frame =>
            {
                if (index >= steps.Count)
                {
                    frame.Close();
                    return;
                }

                if (warmLeft == warmFramesPerStep)
                    steps[index].Prepare();

                draw(frame);

                if (warmLeft > 0)
                {
                    warmLeft--;
                    return;
                }

                capture.CaptureAfterDraw(frame.Host);
                if (!capture.TryRead(out var shot) || shot.Pixels is not { Length: > 0 })
                {
                    retries++;
                    if (retries >= maxRetries)
                        throw new InvalidOperationException(
                            $"Silk framebuffer capture failed for '{steps[index].Path}'.");
                    return;
                }

                retries = 0;
                var png = RgbaPng.Encode(shot.Pixels, shot.Width, shot.Height);
                var path = steps[index].Path;
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllBytes(path, png);
                saved.Add(path);
                index++;
                warmLeft = warmFramesPerStep;
            },
            renderOverlay: null,
            captureSession: null,
            visible: false);

        return saved;
    }

    public static byte[]? CapturePng(int width, int height, Action prepare, Action<SilkFrame> draw)
    {
        var path = Path.Combine(Path.GetTempPath(), "calypso-silk-" + Guid.NewGuid().ToString("N") + ".png");
        try
        {
            var saved = CaptureSequence(width, height, [(path, prepare)], draw);
            if (saved.Count == 0 || !File.Exists(path))
                return null;
            return File.ReadAllBytes(path);
        }
        finally
        {
            try { File.Delete(path); } catch { /* best-effort */ }
        }
    }
}
