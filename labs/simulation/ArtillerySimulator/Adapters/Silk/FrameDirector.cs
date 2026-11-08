using Microsoft.Extensions.Logging;
using Novolis.Agent.Surface;
using Novolis.Silk;

namespace ArtillerySimulator;

/// <summary>
/// Silk frame adapter: shell → drain battle commands → simulate → render.
/// Owns agent surface attach; does not own domain state (<see cref="BattleWorld"/>).
/// </summary>
internal sealed class FrameDirector : IDisposable
{
    private readonly BattleWorld _world;
    private readonly BattleCommandPump _commands;
    private readonly ArtilleryLiveHost _agent;
    private readonly ArtillerySceneRenderer _renderer;
    private readonly ProfileOverlay _profileOverlay;
    private readonly FrameProfiler _liveProfiler = new(warmupFrames: 5);
    private readonly ILogger<FrameDirector> _log;

    private AgentSurface? _surface;
    private FrameProfiler? _profiler;
    private int _profileFramesRemaining = -1;
    private string? _profileJsonPath;
    private bool _showProfile;
    private string _profileFlash = "";
    private float _profileFlashAge = -1f;
    private bool _agentAttached;

    public FrameDirector(
        BattleWorld world,
        BattleCommandPump commands,
        ArtilleryLiveHost agent,
        ArtillerySceneRenderer renderer,
        ProfileOverlay profileOverlay,
        ILogger<FrameDirector> log)
    {
        _world = world;
        _commands = commands;
        _agent = agent;
        _renderer = renderer;
        _profileOverlay = profileOverlay;
        _log = log;
    }

    public BattleSession Session => _world.Session;

    public void EnableProfile(int frames, string? jsonPath = null)
    {
        _profiler = new FrameProfiler(warmupFrames: Math.Min(20, Math.Max(0, frames / 6)));
        _profileFramesRemaining = Math.Max(1, frames);
        _profileJsonPath = jsonPath;
        _log.LogInformation("Profiling enabled for {Frames} frames", frames);
    }

    public void Initialize(SilkFrame frame)
    {
        _world.Shell.EnsureAssets();
        frame.SetTitle(ProductIdentity.WindowTitle);
        WindowIconLoader.TryApply(frame);
        if (_world.Options.SkipShell)
            LoadWorldAndAttachAgent();
    }

    public void Update(SilkFrame frame)
    {
        _world.Shell.HandleKeys(frame, _world.WorldReady, _world.SelectLevel);
        HandleProfileHotkeys(frame);

        if (_world.Shell.TryHandlePreWorldFrame(frame, _world.WorldReady, LoadWorldAndAttachAgent))
            return;

        var active = ActiveProfiler();
        active?.BeginFrame();
        if (active is not null)
        {
            active.Time("simulate", () => _agent.RunFrame(Simulate));
            active.Time("render", () => Render(frame));
            active.SetGeometry(
                _world.Terrain.TerrainTriangleCount,
                _world.Terrain.RoadLineCount,
                _renderer.Wires.AsSpan().Length / 2,
                FireControlUi.LastOverlayLineCount);
            active.EndFrame();
            if (_profiler is not null && _profileFramesRemaining > 0)
            {
                _profileFramesRemaining--;
                if (_profileFramesRemaining == 0)
                {
                    WriteProfileReport();
                    frame.Close();
                }
            }
        }
        else
        {
            _agent.RunFrame(Simulate);
            Render(frame);
        }

        _world.Shell.DrawOverlayIfNeeded(frame);

        if (_profileFlashAge >= 0f)
            _profileFlashAge += frame.DeltaSeconds;

        void Simulate()
        {
            // Agent (and any prior writers) first; player discrete intents are TryWrite'd during
            // SimulateFrame, then drained so fire/charge land this frame — no BackgroundService.
            _ = _commands.Drain();
            _world.SimulateFrame(frame, _world);
            _ = _commands.Drain();
        }
    }

    private void LoadWorldAndAttachAgent()
    {
        _world.LoadWorld();
        if (_agentAttached)
            return;

        _agentAttached = true;
        var def = AgentSurfaceDefinition.From<IArtillerySurface>();
        _surface = AgentSurface.AttachAll(
            _agent,
            def,
            new AgentAttachOptions { EnableStdio = false, EnableMcpStdio = false });
        if (_surface?.Http is { } http)
        {
            _log.LogInformation("Agent surface ready at {BaseUrl}", http.BaseUrl);
            Console.WriteLine($"Agent surface ready: {http.BaseUrl}  (MCP tools at /agent/mcp/tools)");
        }
        else
        {
            _log.LogWarning("Agent surface attach incomplete — expected HTTP :{Port}", def.DefaultHttpPort);
            Console.WriteLine($"Agent surface attach incomplete — expected HTTP :{def.DefaultHttpPort}");
        }

        Console.WriteLine(ProductLegal.Tagline);
        Console.WriteLine($"Session seed={_world.Session.SeedHash}  level={_world.Level.Id}  piece={_world.Session.Piece.Id}");
    }

    private void Render(SilkFrame frame)
    {
        _renderer.DrawPlaying(
            frame,
            _world.Camera,
            _world.Terrain,
            _world.Atmosphere,
            _world.Gun,
            _world.Crew,
            _world.Rounds,
            _world.Aim,
            _world.Battle,
            _world.Particles,
            _world.Mission,
            _world.Level,
            _world.Ui,
            _world.Audio,
            _world.FireBlockReason,
            () => _world.SolutionUnsafe,
            () => _world.LaunchDetonation,
            _world.FxTime,
            ActiveProfiler());

        if (_showProfile)
        {
            _profileOverlay.Draw(
                frame,
                ArtilleryDebugDump.BuildProfilePanel(
                    frame,
                    ActiveProfiler() ?? _liveProfiler,
                    _renderer,
                    _world.Terrain,
                    _world.Atmosphere,
                    _world.Gun,
                    _world.Rounds,
                    _world.PredictedRangeMeters,
                    _world.Camera,
                    _world.Mission,
                    _profileFlash,
                    _profileFlashAge));
        }
    }

    private FrameProfiler? ActiveProfiler()
    {
        if (_profiler is not null)
            return _profiler;
        return _showProfile ? _liveProfiler : null;
    }

    private void HandleProfileHotkeys(SilkFrame frame)
    {
        if (frame.IsKeyPressed(Key.F1))
        {
            _showProfile = !_showProfile;
            if (_showProfile)
                _liveProfiler.Reset();
        }

        var ctrl = frame.IsKeyDown(Key.ControlLeft) || frame.IsKeyDown(Key.ControlRight);
        if (ctrl && frame.IsKeyPressed(Key.C))
        {
            var dump = ArtilleryDebugDump.BuildClipboardDump(
                frame,
                ActiveProfiler() ?? _liveProfiler,
                _renderer,
                _world.Terrain,
                _world.Atmosphere,
                _world.Gun,
                _world.Crew,
                _world.Rounds,
                _world.Camera,
                _world.Mission);
            var ok = WindowsClipboardText.TrySet(dump);
            _profileFlash = ok ? "DEBUG DUMP COPIED" : "CLIPBOARD FAILED";
            _profileFlashAge = 0f;
            if (!_showProfile)
            {
                _showProfile = true;
                _liveProfiler.Reset();
            }
        }
    }

    private void WriteProfileReport()
    {
        if (_profiler is null)
            return;
        var text = _profiler.ToReport();
        Console.WriteLine();
        Console.WriteLine("=== ArtillerySimulator profile ===");
        Console.WriteLine(text);
        if (!string.IsNullOrWhiteSpace(_profileJsonPath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_profileJsonPath))!);
            File.WriteAllText(_profileJsonPath, _profiler.ToJson());
            _log.LogInformation("Wrote profile JSON to {Path}", _profileJsonPath);
            Console.WriteLine($"Wrote {_profileJsonPath}");
        }
    }

    public void Dispose()
    {
        if (_surface is not null)
            _surface.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
}
