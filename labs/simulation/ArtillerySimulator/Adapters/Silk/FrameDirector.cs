using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Novolis.Silk;

namespace ArtillerySimulator;

/// <summary>
/// Silk frame adapter. Physics and rendering are sequenced but budget-isolated:
/// wall-clock fixed steps (capped) never grow because HUD/GPU was slow.
/// </summary>
internal sealed class FrameDirector : IDisposable
{
    private readonly BattleWorld _world;
    private readonly BattleCommandPump _commands;
    private readonly ArtilleryLiveHost _agent;
    private readonly ArtilleryRunOptions _options;
    private readonly Stopwatch _wall = Stopwatch.StartNew();
    private readonly FrameWallClockBudget _wallBudget;
    private readonly ArtilleryAgentSurfaceBootstrap _agentBootstrap;
    private readonly ArtillerySceneRenderer _renderer;
    private readonly ArtilleryFrameProfileController _profiles;
    private readonly PlayingFrameRenderer _playingRenderer;
    private readonly ArtillerySmokeDriver? _smoke;

    public FrameDirector(
        BattleWorld world,
        BattleCommandPump commands,
        ArtilleryLiveHost agent,
        ArtillerySceneRenderer renderer,
        ProfileOverlay profileOverlay,
        ArtilleryRunOptions options,
        ILoggerFactory logs,
        ILogger<FrameDirector> log)
    {
        _world = world;
        _commands = commands;
        _agent = agent;
        _renderer = renderer;
        _options = options;
        _wallBudget = new FrameWallClockBudget(_wall);
        _agentBootstrap = new ArtilleryAgentSurfaceBootstrap(agent, options, world, log);
        _profiles = new ArtilleryFrameProfileController(world, renderer, profileOverlay, log);
        _playingRenderer = new PlayingFrameRenderer(world, renderer, options, _profiles);
        if (options.Smoke)
            _smoke = new ArtillerySmokeDriver(world, logs.CreateLogger<ArtillerySmokeDriver>());
    }

    public BattleSession Session => _world.Session;

    public int SmokeExitCode => _smoke?.ExitCode ?? 1;

    public void EnableProfile(int frames, string? jsonPath = null) =>
        _profiles.EnableProfile(frames, jsonPath);

    public void Initialize(SilkFrame frame)
    {
        _world.Shell.EnsureAssets();
        frame.SetTitle(ProductIdentity.WindowTitle);
        WindowIconLoader.TryApply(frame);
        if (_world.Options.SkipShell)
            LoadWorldAndMaybeAttachAgent();
        _wallBudget.Stamp();
    }

    public void Update(SilkFrame frame)
    {
        var wallSeconds = _wallBudget.AdvanceSeconds();

        _world.Shell.HandleKeys(frame, _world.WorldReady, _world.SelectLevel);
        _profiles.HandleHotkeys(frame);

        if (_world.Shell.TryHandlePreWorldFrame(frame, _world.WorldReady, LoadWorldAndMaybeAttachAgent))
            return;

        void Simulate() => _agent.RunFrame(() => TickAndInput(frame, wallSeconds));
        void Present() => _world.PresentFrame(frame, wallSeconds);
        void Render() => _playingRenderer.Render(frame);

        if (!_profiles.StepTimedCapture(frame, Simulate, Present, Render))
        {
            Simulate();
            Present();
            Render();
        }

        _smoke?.Tick(frame);
        _world.Shell.DrawOverlayIfNeeded(frame, _world.ScoreSnapshot.PauseBody);
        _profiles.TickFlash(frame.DeltaSeconds);
    }

    private void TickAndInput(SilkFrame frame, float wallSeconds)
    {
        _ = _commands.Drain();
        if (_smoke is null)
            _world.HandleInput(frame, _world);
        _ = _commands.Drain();
        _ = _world.TickPhysics(wallSeconds);
    }

    private void LoadWorldAndMaybeAttachAgent()
    {
        _world.LoadWorld();
        _agentBootstrap.TryAttach();
        _wallBudget.Stamp();
    }

    public void Dispose()
    {
        _agentBootstrap.DisposeSurface();
        _renderer.Dispose();
    }
}
