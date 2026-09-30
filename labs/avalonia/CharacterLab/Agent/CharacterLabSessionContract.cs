using Novolis.Agent.Core;
using Novolis.Agent.Surface;

namespace CharacterLab.Agent;

public static class CharacterLabSessionContract
{
    public static AgentSurfaceDefinition Definition { get; } = AgentSurfaceDefinition.From<ICharacterLabSession>();
}
