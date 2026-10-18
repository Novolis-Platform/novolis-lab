using Novolis.Agent.Surface;

namespace TopDownDoom.Game.Agent;

internal static class TopDownDoomAgentContract
{
    public static AgentSurfaceDefinition Definition { get; } =
        AgentSurfaceDefinition.From<ITopDownDoomAgentSurface>();
}
