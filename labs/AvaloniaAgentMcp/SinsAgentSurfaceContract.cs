using Novolis.Agent.Core;
using Novolis.Agent.Surface;

namespace AvaloniaAgentMcp;

/// <summary>
/// Minimal attributed mirror of Sins' <c>ICaptainAgentSurface</c>, kept local so this generic MCP
/// sidecar can resolve marker/env conventions (ports, marker prefix) without a project reference
/// into SinsOfACapitalismTycoon. Must stay in sync with that app's own surface attribute.
/// </summary>
[AgentSurface("sins", HttpPort = 18765, TcpPort = 18766, EnableEnv = "NOVOLIS_GAME_SESSION", MarkerPrefix = "novolis-game-session")]
internal interface ISinsAgentSurface : IAgentHost;

internal static class SinsAgentSurfaceContract
{
    public static AgentSurfaceDefinition Definition { get; } = AgentSurfaceDefinition.From<ISinsAgentSurface>();
}
