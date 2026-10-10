using FrankMoat.Game;
using Novolis.Rendering.Appearance;
using Novolis.Rendering.Planar;
using Novolis.Silk;
using Novolis.Silk.Capture;

var captureDir = CaptureDir(args);
var auto = captureDir is not null;
var game = new FrankMoatGame { SkipMenu = auto };
var scene = new PlanarScene();
using var capture = new FrameCaptureSession(new CaptureStreamOptions
{
    CaptureEveryNFrames = auto ? 12 : 1,
    MaxBufferedFrames = 8,
});
var dumps = 0;
const int autoLimit = 6;
SilkGame.Run(
    "Frank Moat vs the Evil Undead — Minigun Range",
    1280,
    720,
    frame => game.Initialize(frame, scene),
    frame =>
    {
        if (auto)
        {
            dumps += WritePending(capture, captureDir!, dumps);
            if (dumps >= autoLimit)
            {
                frame.Close();
                return;
            }
        }

        scene.Menus.HandleInput(frame.IsMenuUpPressed(), frame.IsMenuDownPressed(), frame.IsMenuConfirmPressed(), frame.IsMenuCancelPressed());
        game.Update(frame, scene);
        frame.Submit(scene.Tessellate(frame.Width, frame.Height));
        if (frame.IsKeyPressed(Key.F10))
        {
            capture.CaptureAfterDraw(frame.Host);
            dumps += WritePending(capture, captureDir ?? DefaultCaptureDir(), dumps);
        }
    },
    renderOverlay: null,
    auto ? capture : null);

static int WritePending(FrameCaptureSession session, string dir, int startIndex)
{
    var n = 0;
    while (session.TryRead(out var frame))
    {
        AppearancePost.Apply(AppearanceRecipes.RadioactiveBloom(), frame.Pixels, frame.Width, frame.Height);
        CapturedFramePpm.Write(frame, Path.Combine(dir, $"frank-moat-{startIndex + n:D3}.ppm"));
        n++;
    }

    return n;
}

static string? CaptureDir(string[] args)
{
    for (var i = 0; i < args.Length; i++)
    {
        if (!string.Equals(args[i], "--capture", StringComparison.OrdinalIgnoreCase))
        {
            continue;
        }

        if (i + 1 < args.Length && !args[i + 1].StartsWith('-'))
        {
            return args[i + 1];
        }

        return DefaultCaptureDir();
    }

    return null;
}

static string DefaultCaptureDir() =>
    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Novolis", "FrankMoat", "capture");
