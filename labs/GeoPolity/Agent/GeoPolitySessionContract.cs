using Novolis.Agent.Core;
using Novolis.Agent.Surface;

namespace GeoPolity.Agent;

[AgentSurface("geopolity",
    HttpPort = 18857,
    TcpPort = 18858,
    EnableEnv = "NOVOLIS_GEOPOLITY_SESSION",
    MarkerPrefix = "novolis-geopolity-session",
    Description = "GeoPolity theatre: pause, speed, step, select system, military build")]
[AgentAction("pause", Summary = "Hard-pause the bridge clock")]
[AgentAction("resume", Summary = "Resume the bridge clock")]
[AgentAction("toggle", Summary = "Toggle run / pause")]
[AgentAction("setspeed", Summary = "Set speed preset", Params = "preset|1-5")]
[AgentAction("step", Summary = "Advance N days (paused or running)", Params = "days|1..3650")]
[AgentAction("advanceyears", Summary = "Burst-advance N years", Params = "years|1..100")]
[AgentAction("selectsystem", Summary = "Focus a system (polity id)", Params = "systemId")]
[AgentAction("setmilshare", Summary = "Player military budget share", Params = "share|0.05-0.7")]
[AgentAction("build", Summary = "Player force build", Params = "domain|land,air,naval;amount|1-500")]
public interface IGeoPolitySession : IAgentHost;

public static class GeoPolitySessionContract
{
    public static AgentSurfaceDefinition Definition { get; } =
        AgentSurfaceDefinition.From<IGeoPolitySession>();
}

public static class GeoPolityActionIds
{
    public const string Pause = "pause";
    public const string Resume = "resume";
    public const string Toggle = "toggle";
    public const string SetSpeed = "setspeed";
    public const string Step = "step";
    public const string AdvanceYears = "advanceyears";
    public const string SelectSystem = "selectsystem";
    public const string SetMilShare = "setmilshare";
    public const string Build = "build";
}
