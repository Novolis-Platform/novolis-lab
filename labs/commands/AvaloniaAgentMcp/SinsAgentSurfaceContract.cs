using Novolis.Agent.Core;
using Novolis.Agent.Surface;

namespace AvaloniaAgentMcp;

internal static class SinsAgentSurfaceContract
{
    public static AgentSurfaceDefinition Definition { get; } = AgentSurfaceDefinition.From<ISinsAgentSurface>();
}
