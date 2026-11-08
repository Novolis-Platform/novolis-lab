using Microsoft.Extensions.Logging;
using Novolis.Silk;

namespace ArtillerySimulator;

/// <summary>Starts the Silk frame loop for the configured run mode.</summary>
internal sealed class ArtilleryGameRunner
{
    private readonly FrameDirector _frame;
    private readonly ArtilleryRunOptions _options;
    private readonly ILogger<ArtilleryGameRunner> _log;

    public ArtilleryGameRunner(
        FrameDirector frame,
        ArtilleryRunOptions options,
        ILogger<ArtilleryGameRunner> log)
    {
        _frame = frame;
        _options = options;
        _log = log;
    }

    public int Run()
    {
        if (_options.ProfileFrames is int frames)
        {
            _log.LogInformation(
                "Profile run: {Frames} frames, level={LevelId}, seed={Seed}",
                frames,
                _options.Level.Id,
                _frame.Session.SeedHash);
            _frame.EnableProfile(frames, _options.ProfileJsonPath);
            SilkGame.Run(
                $"{ProductIdentity.Title} profile",
                1280,
                720,
                _frame.Initialize,
                _frame.Update,
                renderOverlay: null,
                captureSession: null,
                visible: false);
            return 0;
        }

        if (_options.Headless)
        {
            _log.LogInformation(
                "Headless agent: level={LevelId} seed={Seed} http=http://127.0.0.1:18770/",
                _options.Level.Id,
                _frame.Session.SeedHash);
            Console.WriteLine($"{ProductIdentity.Title} headless — level={_options.Level.Id} seed={_frame.Session.SeedHash} — agent HTTP http://127.0.0.1:18770/");
            Console.WriteLine("  GET  /agent/hello  /agent/snapshot  /agent/actions  /agent/mcp/tools");
            Console.WriteLine("  POST /agent/command");
            Console.WriteLine(ProductLegal.Tagline);
            SilkGame.Run(
                $"{ProductIdentity.Title} headless",
                1280,
                720,
                _frame.Initialize,
                _frame.Update,
                renderOverlay: null,
                captureSession: null,
                visible: false);
            return 0;
        }

        _log.LogInformation(
            "Desktop play: level={LevelId} seed={Seed}",
            _options.Level.Id,
            _frame.Session.SeedHash);
        SilkGame.Run(ProductIdentity.WindowTitle, 1920, 1080, _frame.Initialize, _frame.Update);
        return 0;
    }
}
