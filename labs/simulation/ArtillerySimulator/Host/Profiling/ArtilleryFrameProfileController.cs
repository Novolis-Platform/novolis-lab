using Microsoft.Extensions.Logging;
using Novolis.Silk;

namespace ArtillerySimulator;

internal sealed class ArtilleryFrameProfileController
{
    private readonly FrameProfiler _liveProfiler = new(warmupFrames: 5);
    private readonly ArtillerySceneRenderer _renderer;
    private readonly BattleWorld _world;
    private readonly ProfileOverlay _profileOverlay;
    private readonly ILogger _log;

    private FrameProfiler? _profiler;
    private int _profileFramesRemaining = -1;
    private string? _profileJsonPath;
    private bool _showProfile;
    private string _profileFlash = "";
    private float _profileFlashAge = -1f;

    public ArtilleryFrameProfileController(
        BattleWorld world,
        ArtillerySceneRenderer renderer,
        ProfileOverlay profileOverlay,
        ILogger<FrameDirector> log)
    {
        _world = world;
        _renderer = renderer;
        _profileOverlay = profileOverlay;
        _log = log;
    }

    public bool ShowProfile => _showProfile;

    public string ProfileFlash => _profileFlash;

    public float ProfileFlashAge => _profileFlashAge;

    public void TickFlash(float deltaSeconds)
    {
        if (_profileFlashAge >= 0f)
            _profileFlashAge += deltaSeconds;
    }

    public void EnableProfile(int frames, string? jsonPath = null)
    {
        _profiler = new FrameProfiler(warmupFrames: Math.Min(20, Math.Max(0, frames / 6)));
        _profileFramesRemaining = Math.Max(1, frames);
        _profileJsonPath = jsonPath;
        _log.LogInformation("Profiling enabled for {Frames} frames", frames);
    }

    public FrameProfiler? ActiveProfiler()
    {
        if (_profiler is not null)
            return _profiler;
        return _showProfile ? _liveProfiler : null;
    }

    public bool HandleHotkeys(SilkFrame frame)
    {
        var result = ProfileHotkeys.TryHandle(
            frame,
            ActiveProfiler() ?? _liveProfiler,
            _renderer,
            _world);
        if (result is null)
            return false;

        if (result.ToggledShowProfile)
            _showProfile = !_showProfile;
        if (result.ForceShowProfile)
            _showProfile = true;
        if (result.ResetLiveProfiler)
            _liveProfiler.Reset();
        if (result.ProfileFlash is { } flash)
        {
            _profileFlash = flash;
            _profileFlashAge = 0f;
        }

        return false;
    }

    public void ProfileOverlayIfNeeded(SilkFrame frame, FrameViewModel vm)
    {
        if (!_showProfile || _world.Options.Smoke)
            return;

        _profileOverlay.Draw(
            frame,
            ArtilleryDebugDump.BuildProfilePanel(
                frame,
                ActiveProfiler() ?? _liveProfiler,
                _renderer,
                vm.Terrain,
                vm.Atmosphere,
                vm.Gun,
                vm.Rounds,
                vm.PredictedRangeMeters,
                vm.Camera,
                vm.Mission,
                _profileFlash,
                _profileFlashAge));
    }

    public bool StepTimedCapture(SilkFrame frame, Action simulate, Action present, Action render)
    {
        var active = ActiveProfiler();
        if (active is null)
            return false;

        active.BeginFrame();
        active.Time("simulate", simulate);
        active.Time("present", present);
        active.Time("render", render);
        active.SetGeometry(
            _world.Terrain.TerrainTriangleCount,
            _world.Terrain.RoadLineCount,
            _renderer.Wires.AsSpan().Length / 2,
            FireControlUi.LastOverlayLineCount);
        active.EndFrame();

        if (_profiler is null || _profileFramesRemaining <= 0)
            return false;

        _profileFramesRemaining--;
        if (_profileFramesRemaining != 0)
            return false;

        ProfileReportWriter.Write(_profiler, _profileJsonPath, _log);
        frame.Close();
        return true;
    }
}
