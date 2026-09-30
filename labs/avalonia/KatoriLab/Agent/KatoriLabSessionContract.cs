using Novolis.Agent.Core;
using Novolis.Agent.Surface;

namespace KatoriLab.Agent;

public static class KatoriLabSessionContract
{
    public static AgentSurfaceDefinition Definition { get; } = AgentSurfaceDefinition.From<IKatoriLabSession>();
}
