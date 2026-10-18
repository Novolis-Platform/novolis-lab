using Novolis.Agent.Core;
using Novolis.Agent.Surface;

namespace TopDownDoom.Game.Agent;

[AgentSurface(
    "top-down-doom",
    Description = "Optional runtime control and performance snapshot surface for TopDownDoom.",
    EnableEnv = "NOVOLIS_TOP_DOWN_DOOM_AGENT",
    HttpPort = 18875,
    TcpPort = 18876,
    MarkerPrefix = "novolis-top-down-doom")]
[AgentAction(
    TopDownDoomAgentActions.CaptureDiagnostics,
    Summary = "Read current runtime diagnostics")]
[AgentAction(
    TopDownDoomAgentActions.Start,
    Summary = "Start the current game session")]
internal interface ITopDownDoomAgentSurface : IAgentHost;
