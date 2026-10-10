using Novolis.Agent.Core;
using Novolis.Agent.Surface;

namespace GeoPolity.Agent;

public static class GeoPolitySessionContract
{
    public static AgentSurfaceDefinition Definition { get; } =
        AgentSurfaceDefinition.From<IGeoPolitySession>();
}
