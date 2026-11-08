using Microsoft.Extensions.Logging;
using Novolis.Agent.Surface;

namespace ArtillerySimulator;

internal sealed class ArtilleryAgentSurfaceBootstrap
{
    private readonly ArtilleryLiveHost _agent;
    private readonly ArtilleryRunOptions _options;
    private readonly BattleWorld _world;
    private readonly ILogger _log;
    private bool _attachAttempted;
    private AgentSurface? _surface;

    public ArtilleryAgentSurfaceBootstrap(
        ArtilleryLiveHost agent,
        ArtilleryRunOptions options,
        BattleWorld world,
        ILogger<FrameDirector> log)
    {
        _agent = agent;
        _options = options;
        _world = world;
        _log = log;
    }

    public void TryAttach()
    {
        if (_attachAttempted)
            return;
        _attachAttempted = true;

        if (!ShouldAttach())
        {
            _log.LogInformation(
                "Agent surface skipped (set ARTILLERY_ATTACH_AGENT=1 or use --headless / --attach-agent)");
            return;
        }

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

    public void DisposeSurface()
    {
        if (_surface is not null)
            _surface.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    private bool ShouldAttach()
    {
        if (_options.AttachAgent || _options.Headless)
            return true;

        var env = Environment.GetEnvironmentVariable("ARTILLERY_ATTACH_AGENT");
        return string.Equals(env, "1", StringComparison.Ordinal)
            || string.Equals(env, "true", StringComparison.OrdinalIgnoreCase);
    }
}
